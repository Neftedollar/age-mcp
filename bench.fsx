#!/usr/bin/env dotnet fsi
#r "bin/Debug/net10.0/Fyper.dll"
#r "bin/Debug/net10.0/Npgsql.dll"
#r "bin/Debug/net10.0/FsMcp.Core.dll"
#r "bin/Debug/net10.0/age-mcp.dll"

open System
open System.Diagnostics
open AgeMcp.Config
open AgeMcp.Tools
open System.Threading.Tasks

let connStr = "Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp"
configure connStr "default"

// ─── Benchmark helper ───
let bench (name: string) (iterations: int) (fn: unit -> Task<_>) =
    // Warmup
    fn().GetAwaiter().GetResult() |> ignore
    fn().GetAwaiter().GetResult() |> ignore

    let sw = Stopwatch()
    let times = Array.zeroCreate iterations
    for i in 0 .. iterations - 1 do
        sw.Restart()
        fn().GetAwaiter().GetResult() |> ignore
        sw.Stop()
        times.[i] <- sw.Elapsed.TotalMilliseconds

    Array.sort times
    let avg = times |> Array.average
    let p50 = times.[iterations / 2]
    let p95 = times.[int(float iterations * 0.95)]
    let p99 = times.[int(float iterations * 0.99)]
    let min = times.[0]
    let max = times.[iterations - 1]
    printfn "%-30s  avg=%6.1fms  p50=%6.1fms  p95=%6.1fms  p99=%6.1fms  min=%5.1f  max=%5.1f" name avg p50 p95 p99 min max

let N = 30

printfn "=== age-mcp Performance Benchmark (N=%d) ===\n" N
printfn "%-30s  %8s  %8s  %8s  %8s  %7s  %7s" "Operation" "avg" "p50" "p95" "p99" "min" "max"
printfn "%s" (String.replicate 100 "─")

// 1. list_graphs (raw SQL, no cypher)
bench "list_graphs" N (fun () -> listGraphs {| dummy = None |})

// 2. get_or_create_graph (existing)
bench "get_or_create_graph (exists)" N (fun () -> getOrCreateGraph { graph_name = "people" })

// 3. get_schema
bench "get_schema (people)" N (fun () -> getSchema { graph_name = "people" })

// 4. search_vertices (small result)
bench "search_vertices (2 results)" N (fun () ->
    searchVertices { graph_name = "people"; label = Some "Person"; property_key = None; property_value = None; limit = Some 2 })

// 5. search_vertices (all)
bench "search_vertices (all Person)" N (fun () ->
    searchVertices { graph_name = "people"; label = Some "Person"; property_key = None; property_value = None; limit = Some 50 })

// 6. cypher_query (simple)
bench "cypher_query (1 vertex)" N (fun () ->
    cypherQuery { graph_name = "people"; query = "MATCH (n:Person) WHERE n.ident = 'dusan.cvetkovic' RETURN n" })

// 7. get_neighbors
bench "get_neighbors (depth=1)" N (fun () ->
    getNeighbors { graph_name = "people"; vertex_ident = "dusan.cvetkovic"; depth = Some 1; direction = Some "both" })

// 8. export_graph (full)
bench "export_graph (people)" N (fun () -> exportGraph { graph_name = "people" })

// 9. search_edges
bench "search_edges (all)" N (fun () ->
    searchEdges { graph_name = "people"; label = None; limit = Some 50 })

// 10. Concurrent: 10 parallel reads
bench "10x parallel search_vertices" (N / 3) (fun () -> task {
    let tasks = [|
        for _ in 1..10 ->
            searchVertices { graph_name = "people"; label = Some "Person"; property_key = None; property_value = None; limit = Some 5 }
    |]
    let! _ = Task.WhenAll(tasks)
    return ()
})

printfn "\n=== Memory ===\n"
let proc = Process.GetCurrentProcess()
printfn "Working Set:  %d MB" (proc.WorkingSet64 / 1024L / 1024L)
printfn "Private Mem:  %d MB" (proc.PrivateMemorySize64 / 1024L / 1024L)
printfn "GC Gen0: %d  Gen1: %d  Gen2: %d" (GC.CollectionCount 0) (GC.CollectionCount 1) (GC.CollectionCount 2)
