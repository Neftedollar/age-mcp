module AgeMcp.Embeddings

open System
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Npgsql
open Fyper.GraphValue
open AgeMcp.Config

// ─── Configuration ───

let mutable private apiUrl: string option = None
let mutable private apiKey: string option = None
let mutable private model = "text-embedding-3-small"
let mutable private dimensions = 384

let configureEmbeddings (url: string option) (key: string option) (m: string) (d: int) =
    apiUrl <- url; apiKey <- key; model <- m; dimensions <- d

let isConfigured () = apiUrl.IsSome

// ─── Content extraction ───

let nodeToContent (node: GraphNode) =
    node.Properties
    |> Map.toList
    |> List.map (fun (k, v) -> sprintf "%s: %s" k (gvToStr v))
    |> String.concat ", "

// ─── HTTP embedding client (OpenAI-compatible) ───

let private client = lazy (new HttpClient())

let private embToStr (emb: float[]) =
    emb |> Array.map (sprintf "%.8f") |> String.concat "," |> sprintf "[%s]"

let embed (texts: string list) : Task<float[][]> =
    task {
        match apiUrl with
        | None -> return failwith "Embedding API not configured (set EMBEDDING_API_URL)"
        | Some url ->
            let body = JsonSerializer.Serialize({| model = model; input = texts |})
            use req = new HttpRequestMessage(HttpMethod.Post, url)
            req.Content <- new StringContent(body, Encoding.UTF8, "application/json")
            apiKey |> Option.iter (fun k -> req.Headers.Add("Authorization", sprintf "Bearer %s" k))
            let! resp = client.Value.SendAsync(req)
            let! json = resp.Content.ReadAsStringAsync()
            resp.EnsureSuccessStatusCode() |> ignore
            use doc = JsonDocument.Parse(json)
            return
                doc.RootElement.GetProperty("data").EnumerateArray()
                |> Seq.map (fun item ->
                    item.GetProperty("embedding").EnumerateArray()
                    |> Seq.map (fun v -> v.GetDouble())
                    |> Seq.toArray)
                |> Seq.toArray
    }

// ─── Table management ───

let ensureTable () =
    withConnection (fun conn -> task {
        try
            use extCmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", conn)
            do! extCmd.ExecuteNonQueryAsync() :> Task
        with _ -> ()
        use cmd = new NpgsqlCommand(sprintf """
            CREATE TABLE IF NOT EXISTS vertex_embeddings (
                graph_name TEXT NOT NULL,
                vertex_ident TEXT NOT NULL,
                content TEXT,
                embedding vector(%d),
                updated_at TIMESTAMP DEFAULT NOW(),
                PRIMARY KEY (graph_name, vertex_ident)
            )""" dimensions, conn)
        do! cmd.ExecuteNonQueryAsync() :> Task
    })

// ─── Sync embeddings for vertices that don't have them yet ───

let sync (graphName: string) : Task<int> =
    task {
        if not (isConfigured ()) then return 0
        else
            do! ensureTable ()
            let sn = scoped graphName

            // Get all vertices from graph
            let! records = executeCypherRead graphName "MATCH (n) RETURN n"
            let vertices =
                records |> List.choose (fun r ->
                    r.Values |> Map.values |> Seq.tryPick (function
                        | GNode n ->
                            n.Properties |> Map.tryFind "ident" |> Option.bind (function
                                | GString id -> Some(id, nodeToContent n)
                                | _ -> None)
                        | _ -> None))

            // Get existing embeddings
            let! existing = withConnection (fun conn -> task {
                use cmd = new NpgsqlCommand(
                    "SELECT vertex_ident FROM vertex_embeddings WHERE graph_name = @g", conn)
                cmd.Parameters.AddWithValue("g", sn) |> ignore
                use! reader = cmd.ExecuteReaderAsync()
                let ids = ResizeArray<string>()
                while reader.Read() do ids.Add(reader.GetString 0)
                return set ids
            })

            let missing = vertices |> List.filter (fun (id, _) -> not (existing.Contains id))
            if List.isEmpty missing then return 0
            else
                // Batch embed and store
                for batch in missing |> List.chunkBySize 100 do
                    let! embeddings = embed (batch |> List.map snd)
                    do! withConnection (fun conn -> task {
                        for i in 0 .. batch.Length - 1 do
                            let id, content = batch.[i]
                            use cmd = new NpgsqlCommand("""
                                INSERT INTO vertex_embeddings (graph_name, vertex_ident, content, embedding, updated_at)
                                VALUES (@g, @id, @c, @e::vector, NOW())
                                ON CONFLICT (graph_name, vertex_ident)
                                DO UPDATE SET content = @c, embedding = @e::vector, updated_at = NOW()""", conn)
                            cmd.Parameters.AddWithValue("g", sn) |> ignore
                            cmd.Parameters.AddWithValue("id", id) |> ignore
                            cmd.Parameters.AddWithValue("c", content) |> ignore
                            cmd.Parameters.AddWithValue("e", embToStr embeddings.[i]) |> ignore
                            do! cmd.ExecuteNonQueryAsync() :> Task
                    })
                return missing.Length
    }

// ─── Vector similarity search ───

let search (graphName: string) (queryText: string) (limit: int) : Task<(string * string * float) list> =
    task {
        do! ensureTable ()
        let! _ = sync graphName

        // Embed query
        let! qEmb = embed [ queryText ]
        let sn = scoped graphName

        return! withConnection (fun conn -> task {
            use cmd = new NpgsqlCommand(sprintf """
                SELECT vertex_ident, content, 1 - (embedding <=> @e::vector) AS similarity
                FROM vertex_embeddings WHERE graph_name = @g
                ORDER BY embedding <=> @e::vector
                LIMIT %d""" limit, conn)
            cmd.Parameters.AddWithValue("g", sn) |> ignore
            cmd.Parameters.AddWithValue("e", embToStr qEmb.[0]) |> ignore
            use! reader = cmd.ExecuteReaderAsync()
            let results = ResizeArray<string * string * float>()
            while reader.Read() do
                results.Add(reader.GetString 0, reader.GetString 1, reader.GetDouble 2)
            return results |> Seq.toList
        })
    }
