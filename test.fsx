#!/usr/bin/env dotnet fsi
#r "bin/Debug/net10.0/Fyper.dll"
#r "bin/Debug/net10.0/Npgsql.dll"
#r "bin/Debug/net10.0/FsMcp.Core.dll"
#r "bin/Debug/net10.0/age-mcp.dll"

open AgeMcp.Config
open AgeMcp.Tools
open System.Threading.Tasks

let connStr = "Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp"
configure connStr "default"

let run (t: Task<Result<FsMcp.Core.Content list, _>>) =
    let result = t.GetAwaiter().GetResult()
    match result with
    | Ok contents ->
        for c in contents do
            match c with
            | FsMcp.Core.Content.Text t ->
                if t.Length > 300 then printfn "%s..." (t.[..296])
                else printfn "%s" t
            | _ -> printfn "(non-text content)"
    | Error e -> printfn "ERROR: %A" e

printfn "=== 1. list_graphs ==="
listGraphs {| dummy = None |} |> run

printfn "\n=== 2. search_vertices (people, Person, limit=2) ==="
searchVertices { graph_name = "people"; label = Some "Person"; property_key = None; property_value = None; limit = Some 2 } |> run

printfn "\n=== 3. get_schema (people) ==="
getSchema { graph_name = "people" } |> run

printfn "\n=== 4. export_graph (people) ==="
exportGraph { graph_name = "people" } |> run

printfn "\n=== 5. get_neighbors (people, dusan.cvetkovic, depth=1) ==="
getNeighbors { graph_name = "people"; vertex_ident = "dusan.cvetkovic"; depth = Some 1; direction = Some "both" } |> run

printfn "\n=== 6. cypher_query ==="
cypherQuery { graph_name = "people"; query = "MATCH (n:Person) WHERE n.ident = 'dusan.cvetkovic' RETURN n" } |> run

printfn "\n=== Done ==="
