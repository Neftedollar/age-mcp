module AgeMcp.Tools

open System
open System.Text.Json
open System.Threading.Tasks
open Npgsql
open Fyper.GraphValue
open FsMcp.Core
open AgeMcp.Config

// ─── Argument types ───

type GraphNameArgs = { graph_name: string }
type DropGraphsArgs = { graph_names: string }

type UpsertVertexArgs = {
    graph_name: string
    vertex_ident: string
    label: string option
    properties: string option
}

type UpsertEdgeArgs = {
    graph_name: string
    label: string
    edge_start_ident: string
    edge_end_ident: string
    properties: string option
}

type UpsertGraphArgs = {
    graph_name: string
    vertices: string
    edges: string
}

type DropVertexArgs = { graph_name: string; vertex_ident: string }
type DropEdgeArgs = { graph_name: string; edge_ident: string }
type CypherQueryArgs = { graph_name: string; query: string }

type SearchVerticesArgs = {
    graph_name: string
    label: string option
    property_key: string option
    property_value: string option
    limit: int option
}

type SearchEdgesArgs = {
    graph_name: string
    label: string option
    limit: int option
}

type GetNeighborsArgs = {
    graph_name: string
    vertex_ident: string
    depth: int option
    direction: string option
}

type ImportGraphArgs = {
    graph_name: string
    vertices: string
    edges: string
}

type SemanticSearchArgs = {
    graph_name: string
    query: string
    limit: int option
}

type GraphContextArgs = {
    graph_name: string
    query: string
    top_k: int option
    depth: int option
}

type SyncToOpenBrainArgs = {
    graph_name: string
    category: string option
    tag_prefix: string option
}

type ImportFromOpenBrainArgs = {
    graph_name: string
    memories: string
    connect_by_tags: string option
}

// ─── Helpers ───

let private ok text = Ok [ Content.text text ]

let private buildVertexSetClause (ident: string) (props: JsonElement) =
    let parts = ResizeArray<string>()
    parts.Add(sprintf "n.ident = %s" (quoteStr ident))
    if props.ValueKind = JsonValueKind.Object then
        for p in props.EnumerateObject() do
            if p.Name <> "ident" && p.Name <> "label" then
                parts.Add(sprintf "n.%s = %s" p.Name (cypherValue p.Value))
    parts |> Seq.toList |> String.concat ", "

let private buildEdgeSetClause (edgeIdent: string) (startIdent: string) (endIdent: string) (props: JsonElement) =
    let parts = ResizeArray<string>()
    parts.Add(sprintf "e.ident = %s" (quoteStr edgeIdent))
    parts.Add(sprintf "e.start_ident = %s" (quoteStr startIdent))
    parts.Add(sprintf "e.end_ident = %s" (quoteStr endIdent))
    if props.ValueKind = JsonValueKind.Object then
        let skip = set ["ident"; "label"; "start_ident"; "end_ident"; "edge_start_ident"; "edge_end_ident"]
        for p in props.EnumerateObject() do
            if not (skip.Contains p.Name) then
                parts.Add(sprintf "e.%s = %s" p.Name (cypherValue p.Value))
    parts |> Seq.toList |> String.concat ", "

let private getJsonStr (elem: JsonElement) (name: string) =
    match elem.TryGetProperty(name) with
    | true, v when v.ValueKind = JsonValueKind.String -> Some(v.GetString())
    | _ -> None

let private getJsonStrRequired (elem: JsonElement) (name: string) =
    match elem.TryGetProperty(name) with
    | true, v -> v.GetString()
    | _ -> failwithf "Missing required field '%s'" name

let private ensureGraphExists (graphName: string) =
    let scopedName = scoped graphName
    withConnection (fun conn -> task {
        use checkCmd = new NpgsqlCommand(
            "SELECT count(*) FROM ag_catalog.ag_graph WHERE name = @name", conn)
        checkCmd.Parameters.AddWithValue("name", scopedName) |> ignore
        let! count = checkCmd.ExecuteScalarAsync()
        if (unbox<int64> count) = 0L then
            use createCmd = new NpgsqlCommand(
                sprintf "SELECT create_graph('%s')" (escapeStr scopedName), conn)
            do! createCmd.ExecuteNonQueryAsync() :> Task
    })

// ─── Graph management ───

let getOrCreateGraph (args: GraphNameArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let scopedName = scoped args.graph_name
            let! status = withConnection (fun conn -> task {
                use checkCmd = new NpgsqlCommand(
                    "SELECT count(*) FROM ag_catalog.ag_graph WHERE name = @name", conn)
                checkCmd.Parameters.AddWithValue("name", scopedName) |> ignore
                let! count = checkCmd.ExecuteScalarAsync()
                if (unbox<int64> count) > 0L then
                    return "exists"
                else
                    use createCmd = new NpgsqlCommand(
                        sprintf "SELECT create_graph('%s')" (escapeStr scopedName), conn)
                    do! createCmd.ExecuteNonQueryAsync() :> Task
                    return "created"
            })
            return ok (sprintf """{"name": "%s", "status": "%s"}""" args.graph_name status)
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let listGraphs (_args: {| dummy: string option |}) : Task<Result<Content list, McpError>> =
    task {
        try
            let prefix = sprintf "t_%s__" (getTenantId ())
            let! graphs = withConnection (fun conn -> task {
                use cmd = new NpgsqlCommand(
                    "SELECT name FROM ag_catalog.ag_graph WHERE name LIKE @prefix ORDER BY name", conn)
                cmd.Parameters.AddWithValue("prefix", prefix + "%") |> ignore
                use! reader = cmd.ExecuteReaderAsync()
                let results = ResizeArray<string>()
                while reader.Read() do
                    results.Add(unscoped (reader.GetString(0)))
                return results |> Seq.toList
            })
            let json = graphs |> List.map (sprintf "\"%s\"") |> String.concat ", " |> sprintf "[%s]"
            return ok json
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let dropGraphs (args: DropGraphsArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let names = parseJsonArr args.graph_names
            let dropped = ResizeArray<string>()
            do! withConnection (fun conn -> task {
                for elem in names.EnumerateArray() do
                    let name = elem.GetString()
                    try
                        use cmd = new NpgsqlCommand(
                            sprintf "SELECT drop_graph('%s', true)" (escapeStr (scoped name)), conn)
                        do! cmd.ExecuteNonQueryAsync() :> Task
                        dropped.Add(name)
                    with _ -> ()
            })
            let json = dropped |> Seq.map (sprintf "\"%s\"") |> String.concat ", " |> sprintf """{"dropped": [%s]}"""
            return ok json
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── Vertex operations ───

let upsertVertex (args: UpsertVertexArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let label = args.label |> Option.defaultValue "Node"
            let props = parseJsonObj args.properties
            let setClause = buildVertexSetClause args.vertex_ident props
            let cypher =
                sprintf "MERGE (n:%s {ident: %s}) SET %s RETURN n"
                    label (quoteStr args.vertex_ident) setClause
            let! records = executeCypherRead args.graph_name cypher
            return ok (recordsToJson records)
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let dropVertex (args: DropVertexArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let cypher = sprintf "MATCH (n {ident: %s}) DETACH DELETE n" (quoteStr args.vertex_ident)
            let! count = executeCypherWrite args.graph_name cypher
            return ok (sprintf "Deleted vertex '%s' (affected: %d)" args.vertex_ident count)
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── Edge operations ───

let upsertEdge (args: UpsertEdgeArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let props = parseJsonObj args.properties
            let edgeIdent =
                match getJsonStr props "ident" with
                | Some id -> id
                | None -> sprintf "%s__%s__%s" args.edge_start_ident args.label args.edge_end_ident
            let setClause = buildEdgeSetClause edgeIdent args.edge_start_ident args.edge_end_ident props
            let cypher =
                sprintf "MATCH (a {ident: %s}) MATCH (b {ident: %s}) MERGE (a)-[e:%s]->(b) SET %s RETURN e"
                    (quoteStr args.edge_start_ident) (quoteStr args.edge_end_ident) args.label setClause
            let! records = executeCypherRead args.graph_name cypher
            return ok (recordsToJson records)
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let dropEdge (args: DropEdgeArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let cypher = sprintf "MATCH ()-[e {ident: %s}]->() DELETE e" (quoteStr args.edge_ident)
            let! count = executeCypherWrite args.graph_name cypher
            return ok (sprintf "Deleted edge '%s' (affected: %d)" args.edge_ident count)
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── Batch operations ───

let upsertGraph (args: UpsertGraphArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let! result = withCypherTransaction args.graph_name (fun tx -> task {
                let vertices = parseJsonArr args.vertices
                let edges = parseJsonArr args.edges
                let mutable vCount = 0
                let mutable eCount = 0

                for v in vertices.EnumerateArray() do
                    let ident = getJsonStrRequired v "ident"
                    let label = getJsonStr v "label" |> Option.defaultValue "Node"
                    let props = match v.TryGetProperty("properties") with true, p -> p | _ -> v
                    let setClause = buildVertexSetClause ident props
                    let cypher = sprintf "MERGE (n:%s {ident: %s}) SET %s RETURN n" label (quoteStr ident) setClause
                    let! _ = tx.Read cypher
                    vCount <- vCount + 1

                for e in edges.EnumerateArray() do
                    let label = getJsonStrRequired e "label"
                    let startIdent =
                        getJsonStr e "start_ident"
                        |> Option.orElseWith (fun () -> getJsonStr e "edge_start_ident")
                        |> Option.defaultWith (fun () -> failwith "Edge missing start_ident")
                    let endIdent =
                        getJsonStr e "end_ident"
                        |> Option.orElseWith (fun () -> getJsonStr e "edge_end_ident")
                        |> Option.defaultWith (fun () -> failwith "Edge missing end_ident")
                    let edgeIdent = getJsonStr e "ident" |> Option.defaultValue (sprintf "%s__%s__%s" startIdent label endIdent)
                    let props = match e.TryGetProperty("properties") with true, p -> p | _ -> e
                    let setClause = buildEdgeSetClause edgeIdent startIdent endIdent props
                    let cypher =
                        sprintf "MATCH (a {ident: %s}) MATCH (b {ident: %s}) MERGE (a)-[e:%s]->(b) SET %s RETURN e"
                            (quoteStr startIdent) (quoteStr endIdent) label setClause
                    let! _ = tx.Read cypher
                    eCount <- eCount + 1

                return sprintf """{"vertices_affected": %d, "edges_affected": %d}""" vCount eCount
            })
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── Query operations ───

let cypherQuery (args: CypherQueryArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let! records = executeCypherRead args.graph_name args.query
            let result = if List.isEmpty records then "No results" else recordsToJson records
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let cypherWrite (args: CypherQueryArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let! count = executeCypherWrite args.graph_name args.query
            return ok (sprintf """{"affected": %d}""" count)
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let searchVertices (args: SearchVerticesArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let limit = args.limit |> Option.defaultValue 50
            let matchClause = match args.label with Some l -> sprintf "MATCH (n:%s)" l | None -> "MATCH (n)"
            let whereClause =
                match args.property_key, args.property_value with
                | Some k, Some v -> sprintf " WHERE n.%s = %s" k (quoteStr v)
                | _ -> ""
            let cypher = sprintf "%s%s RETURN n LIMIT %d" matchClause whereClause limit
            let! records = executeCypherRead args.graph_name cypher
            let result = if List.isEmpty records then "No matching vertices" else recordsToJson records
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let searchEdges (args: SearchEdgesArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let limit = args.limit |> Option.defaultValue 50
            let cypher =
                match args.label with
                | Some l -> sprintf "MATCH ()-[e:%s]->() RETURN e LIMIT %d" l limit
                | None -> sprintf "MATCH ()-[e]->() RETURN e LIMIT %d" limit
            let! records = executeCypherRead args.graph_name cypher
            let result = if List.isEmpty records then "No matching edges" else recordsToJson records
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let getNeighbors (args: GetNeighborsArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let depth = args.depth |> Option.defaultValue 1 |> max 1 |> min 5
            let direction = args.direction |> Option.defaultValue "both"
            let pattern =
                match direction with
                | "out" -> sprintf "(start)-[*1..%d]->(neighbor)" depth
                | "in" -> sprintf "(start)<-[*1..%d]-(neighbor)" depth
                | _ -> sprintf "(start)-[*1..%d]-(neighbor)" depth
            let cypherV = sprintf "MATCH %s WHERE start.ident = %s RETURN DISTINCT neighbor" pattern (quoteStr args.vertex_ident)
            let cypherE = sprintf "MATCH (start {ident: %s})-[e]-(neighbor) RETURN e" (quoteStr args.vertex_ident)
            let! vertices = executeCypherRead args.graph_name cypherV
            let! edges = executeCypherRead args.graph_name cypherE
            let result = toJson (fun w ->
                w.WriteStartObject()
                w.WritePropertyName("vertices")
                w.WriteStartArray()
                for r in vertices do for kv in r.Values do writeGv w kv.Value
                w.WriteEndArray()
                w.WritePropertyName("edges")
                w.WriteStartArray()
                for r in edges do for kv in r.Values do writeGv w kv.Value
                w.WriteEndArray()
                w.WriteEndObject())
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── Export / Import ───

let exportGraph (args: GraphNameArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let! vertexRecords = executeCypherRead args.graph_name "MATCH (n) RETURN n"
            let! edgeRecords = executeCypherRead args.graph_name "MATCH ()-[e]->() RETURN e"
            let result = toJson (fun w ->
                w.WriteStartObject()
                w.WriteString("name", args.graph_name)
                w.WritePropertyName("vertices")
                w.WriteStartArray()
                for r in vertexRecords do for kv in r.Values do writeGv w kv.Value
                w.WriteEndArray()
                w.WritePropertyName("edges")
                w.WriteStartArray()
                for r in edgeRecords do for kv in r.Values do writeGv w kv.Value
                w.WriteEndArray()
                w.WriteEndObject())
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let importGraph (args: ImportGraphArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            do! ensureGraphExists args.graph_name
            return! upsertGraph { graph_name = args.graph_name; vertices = args.vertices; edges = args.edges }
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── Schema ───

let getSchema (args: GraphNameArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let! records = executeCypherRead args.graph_name "MATCH (n) RETURN label(n) AS label, count(*) AS cnt"
            let result = if List.isEmpty records then "Empty graph" else recordsToJson records
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── Visualization ───

let generateVisualization (args: GraphNameArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let! vRecords = executeCypherRead args.graph_name "MATCH (n) RETURN n"
            let! eRecords = executeCypherRead args.graph_name "MATCH ()-[e]->() RETURN e"

            let idMap = Collections.Generic.Dictionary<int64, string>()
            let nodes = ResizeArray<string>()
            for r in vRecords do
                for kv in r.Values do
                    match kv.Value with
                    | GNode node ->
                        let ident = match node.Properties |> Map.tryFind "ident" with Some(GString s) -> s | _ -> string node.Id
                        let label = node.Labels |> List.tryHead |> Option.defaultValue "Node"
                        let title = node.Properties |> Map.toList |> List.map (fun (k, v) -> sprintf "%s: %s" k (gvToStr v)) |> String.concat "<br>"
                        idMap.[node.Id] <- ident
                        nodes.Add(sprintf """{"id":"%s","label":"%s\n(%s)","group":"%s","title":"%s"}"""
                            (escapeJson ident) (escapeJson ident) (escapeJson label) (escapeJson label) (escapeJson title))
                    | _ -> ()

            let edges = ResizeArray<string>()
            for r in eRecords do
                for kv in r.Values do
                    match kv.Value with
                    | GRel rel ->
                        let fromId =
                            match rel.Properties |> Map.tryFind "start_ident" with
                            | Some(GString s) -> s
                            | _ -> match idMap.TryGetValue(rel.StartNodeId) with true, id -> id | _ -> string rel.StartNodeId
                        let toId =
                            match rel.Properties |> Map.tryFind "end_ident" with
                            | Some(GString s) -> s
                            | _ -> match idMap.TryGetValue(rel.EndNodeId) with true, id -> id | _ -> string rel.EndNodeId
                        edges.Add(sprintf """{"from":"%s","to":"%s","label":"%s","arrows":"to"}"""
                            (escapeJson fromId) (escapeJson toId) (escapeJson rel.RelType))
                    | _ -> ()

            let html = String.concat "" [
                "<!DOCTYPE html><html><head>"
                sprintf "<title>Graph: %s</title>" (escapeJson args.graph_name)
                "<script src=\"https://unpkg.com/vis-network/standalone/umd/vis-network.min.js\"></script>"
                "<style>body{margin:0;font-family:sans-serif}#graph{width:100%;height:100vh}</style>"
                "</head><body><div id=\"graph\"></div><script>"
                sprintf "var nodes=new vis.DataSet([%s]);" (nodes |> String.concat ",")
                sprintf "var edges=new vis.DataSet([%s]);" (edges |> String.concat ",")
                "new vis.Network(document.getElementById('graph'),{nodes:nodes,edges:edges},"
                "{nodes:{shape:'dot',size:16,font:{size:14}},edges:{arrows:'to',font:{size:12,align:'middle'}},"
                "physics:{stabilization:{iterations:150}}});</script></body></html>"
            ]
            return ok html
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── Semantic search ───

let semanticSearch (args: SemanticSearchArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let limit = args.limit |> Option.defaultValue 10
            let! results = AgeMcp.Embeddings.search args.graph_name args.query limit
            let json = toJson (fun w ->
                w.WriteStartArray()
                for (ident, content, similarity) in results do
                    w.WriteStartObject()
                    w.WriteString("vertex_ident", ident)
                    w.WriteString("content", content)
                    w.WriteNumber("similarity", Math.Round(similarity, 4))
                    w.WriteEndObject()
                w.WriteEndArray())
            return ok (if List.isEmpty results then "No results (is EMBEDDING_API_URL configured?)" else json)
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── Graph context (RAG) ───

let graphContext (args: GraphContextArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let topK = args.top_k |> Option.defaultValue 5
            let depth = args.depth |> Option.defaultValue 1 |> max 1 |> min 5
            let! seeds = AgeMcp.Embeddings.search args.graph_name args.query topK

            let context = ResizeArray<GraphRecord>()
            for (ident, _, _) in seeds do
                let cypher = sprintf "MATCH (start {ident: %s})-[*1..%d]-(neighbor) RETURN DISTINCT neighbor" (quoteStr ident) depth
                let! records = executeCypherRead args.graph_name cypher
                context.AddRange(records)

            let result = toJson (fun w ->
                w.WriteStartObject()
                w.WritePropertyName("seeds")
                w.WriteStartArray()
                for (ident, content, similarity) in seeds do
                    w.WriteStartObject()
                    w.WriteString("vertex_ident", ident)
                    w.WriteString("content", content)
                    w.WriteNumber("similarity", Math.Round(similarity, 4))
                    w.WriteEndObject()
                w.WriteEndArray()
                w.WritePropertyName("context")
                w.WriteStartArray()
                for r in context do for kv in r.Values do writeGv w kv.Value
                w.WriteEndArray()
                w.WriteEndObject())
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

// ─── OpenBrain bridge ───

let syncToOpenbrain (args: SyncToOpenBrainArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let category = args.category |> Option.defaultValue "observation"
            let tagPrefix = args.tag_prefix |> Option.defaultValue "graph"
            let! records = executeCypherRead args.graph_name "MATCH (n) RETURN n"

            let result = toJson (fun w ->
                w.WriteStartObject()
                w.WritePropertyName("memories")
                w.WriteStartArray()
                for r in records do
                    for kv in r.Values do
                        match kv.Value with
                        | GNode node ->
                            let ident = match node.Properties |> Map.tryFind "ident" with Some(GString s) -> s | _ -> string node.Id
                            let label = node.Labels |> List.tryHead |> Option.defaultValue "Node"
                            let content = AgeMcp.Embeddings.nodeToContent node
                            w.WriteStartObject()
                            w.WriteString("id", ident)
                            w.WriteString("content", content)
                            w.WriteString("category", category)
                            w.WritePropertyName("tags")
                            w.WriteStartArray()
                            w.WriteStringValue(sprintf "%s:%s" tagPrefix args.graph_name)
                            w.WriteStringValue(sprintf "%s:label:%s" tagPrefix label)
                            w.WriteEndArray()
                            w.WritePropertyName("metadata")
                            w.WriteStartObject()
                            w.WriteString("graph_name", args.graph_name)
                            w.WriteString("vertex_ident", ident)
                            w.WriteString("label", label)
                            w.WritePropertyName("properties")
                            w.WriteStartObject()
                            for p in node.Properties do w.WritePropertyName(p.Key); writeGv w p.Value
                            w.WriteEndObject()
                            w.WriteEndObject()
                            w.WriteEndObject()
                        | _ -> ()
                w.WriteEndArray()
                w.WriteEndObject())
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }

let importFromOpenbrain (args: ImportFromOpenBrainArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            let connectByTags = match args.connect_by_tags with Some "false" | Some "0" | Some "no" -> false | _ -> true
            let memories = parseJsonArr args.memories
            do! ensureGraphExists args.graph_name

            let! result = withCypherTransaction args.graph_name (fun tx -> task {
                let mutable vCount = 0
                let mutable eCount = 0
                let tagMap = Collections.Generic.Dictionary<string, ResizeArray<string>>()

                for mem in memories.EnumerateArray() do
                    let ident =
                        getJsonStr mem "id"
                        |> Option.orElseWith (fun () ->
                            match mem.TryGetProperty("metadata") with
                            | true, meta -> getJsonStr meta "vertex_ident"
                            | _ -> None)
                        |> Option.defaultWith (fun () -> Guid.NewGuid().ToString())
                    let label =
                        match mem.TryGetProperty("metadata") with
                        | true, meta -> getJsonStr meta "label" |> Option.defaultValue "Memory"
                        | _ -> "Memory"
                    let content = getJsonStr mem "content" |> Option.defaultValue ""

                    let parts = ResizeArray<string>()
                    parts.Add(sprintf "n.ident = %s" (quoteStr ident))
                    parts.Add(sprintf "n.content = %s" (quoteStr content))
                    match mem.TryGetProperty("metadata") with
                    | true, meta ->
                        match meta.TryGetProperty("properties") with
                        | true, props when props.ValueKind = JsonValueKind.Object ->
                            for p in props.EnumerateObject() do
                                if p.Name <> "ident" && p.Name <> "label" then
                                    parts.Add(sprintf "n.%s = %s" p.Name (cypherValue p.Value))
                        | _ -> ()
                    | _ -> ()

                    let cypher = sprintf "MERGE (n:%s {ident: %s}) SET %s RETURN n" label (quoteStr ident) (parts |> Seq.toList |> String.concat ", ")
                    let! _ = tx.Read cypher
                    vCount <- vCount + 1

                    if connectByTags then
                        match mem.TryGetProperty("tags") with
                        | true, tags when tags.ValueKind = JsonValueKind.Array ->
                            for tag in tags.EnumerateArray() do
                                let t = tag.GetString()
                                if not (tagMap.ContainsKey t) then tagMap.[t] <- ResizeArray<string>()
                                tagMap.[t].Add(ident)
                        | _ -> ()

                if connectByTags then
                    for kv in tagMap do
                        let idents = kv.Value |> Seq.distinct |> Seq.toArray
                        if idents.Length > 1 then
                            for i in 0 .. idents.Length - 2 do
                                for j in i + 1 .. idents.Length - 1 do
                                    let a, b = idents.[i], idents.[j]
                                    let edgeIdent = sprintf "%s__shared_tag__%s" a b
                                    let setClauses =
                                        sprintf "e.ident = %s, e.start_ident = %s, e.end_ident = %s, e.tag = %s"
                                            (quoteStr edgeIdent) (quoteStr a) (quoteStr b) (quoteStr kv.Key)
                                    let cypher =
                                        sprintf "MATCH (a {ident: %s}) MATCH (b {ident: %s}) MERGE (a)-[e:shared_tag]->(b) SET %s RETURN e"
                                            (quoteStr a) (quoteStr b) setClauses
                                    let! _ = tx.Read cypher
                                    eCount <- eCount + 1

                return sprintf """{"vertices_imported": %d, "edges_created": %d}""" vCount eCount
            })
            return ok result
        with ex ->
            return ok (sprintf "Error: %s" ex.Message)
    }
