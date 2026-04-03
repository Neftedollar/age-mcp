module AgeMcp.Tools

open System.Threading.Tasks
open Npgsql
open Fyper
open Fyper.Ast
open Fyper.Age
open FsMcp.Core
open FsMcp.Core.Validation

// ─── Connection ───

let mutable private dataSource : NpgsqlDataSource option = None
let mutable private graphName = "default"

let configure (connStr: string) (graph: string) =
    dataSource <- Some (NpgsqlDataSource.Create connStr)
    graphName <- graph

let private getDriver () =
    match dataSource with
    | Some ds -> new AgeDriver(ds, graphName) :> IGraphDriver
    | None -> failwith "AGE not configured. Call configure first."

// ─── Tool argument types ───

type QueryArgs = { cypher: string }
type CreateGraphArgs = { name: string }
type NodeArgs = { label: string; properties: string }
type SearchArgs = { label: string; property: string; value: string }
type CypherWithParamsArgs = { cypher: string; parameters: string }

// ─── Tools ───

let executeCypher (args: QueryArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            use driver = getDriver ()
            let! records = driver.ExecuteReadAsync(args.cypher, Map.empty)
            let result =
                records
                |> List.map (fun r ->
                    r.Values
                    |> Map.toList
                    |> List.map (fun (k, v) -> sprintf "%s: %A" k v)
                    |> String.concat ", ")
                |> String.concat "\n"
            return Ok [ Content.text (if result = "" then "No results" else result) ]
        with ex ->
            return Ok [ Content.text (sprintf "Error: %s" ex.Message) ]
    }

let executeWrite (args: QueryArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            use driver = getDriver ()
            let! count = driver.ExecuteWriteAsync(args.cypher, Map.empty)
            return Ok [ Content.text (sprintf "Affected: %d" count) ]
        with ex ->
            return Ok [ Content.text (sprintf "Error: %s" ex.Message) ]
    }

let searchNodes (args: SearchArgs) : Task<Result<Content list, McpError>> =
    task {
        try
            use driver = getDriver ()
            let cypher = sprintf "MATCH (n:%s) WHERE n.%s = $value RETURN n" args.label args.property
            let! records = driver.ExecuteReadAsync(cypher, Map.ofList ["value", box args.value])
            let result =
                records
                |> List.map (fun r ->
                    r.Values
                    |> Map.toList
                    |> List.map (fun (k, v) -> sprintf "%s: %A" k v)
                    |> String.concat ", ")
                |> String.concat "\n"
            return Ok [ Content.text (if result = "" then "No matching nodes" else result) ]
        with ex ->
            return Ok [ Content.text (sprintf "Error: %s" ex.Message) ]
    }

let getSchema (_args: {| dummy: string option |}) : Task<Result<Content list, McpError>> =
    task {
        try
            use driver = getDriver ()
            let! labels = driver.ExecuteReadAsync(
                "MATCH (n) RETURN DISTINCT labels(n) AS labels, count(n) AS count",
                Map.empty)
            let result =
                labels
                |> List.map (fun r ->
                    r.Values
                    |> Map.toList
                    |> List.map (fun (k, v) -> sprintf "%s: %A" k v)
                    |> String.concat ", ")
                |> String.concat "\n"
            return Ok [ Content.text (if result = "" then "Empty graph" else result) ]
        with ex ->
            return Ok [ Content.text (sprintf "Error: %s" ex.Message) ]
    }

let listGraphs (_args: {| dummy: string option |}) : Task<Result<Content list, McpError>> =
    task {
        try
            match dataSource with
            | None -> return Ok [ Content.text "Not connected" ]
            | Some ds ->
                let! conn = ds.OpenConnectionAsync()
                use cmd = new NpgsqlCommand(
                    "SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog', 'information_schema', 'ag_catalog', 'public') ORDER BY nspname",
                    conn)
                use! reader = cmd.ExecuteReaderAsync()
                let graphs = System.Collections.Generic.List<string>()
                while reader.Read() do
                    graphs.Add(reader.GetString(0))
                conn.Dispose()
                let result = if graphs.Count = 0 then "No graphs found" else graphs |> Seq.toList |> String.concat "\n"
                return Ok [ Content.text result ]
        with ex ->
            return Ok [ Content.text (sprintf "Error: %s" ex.Message) ]
    }
