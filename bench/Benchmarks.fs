module AgeMcp.Bench.Benchmarks

open System.Threading.Tasks
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Jobs
open AgeMcp.Config
open AgeMcp.Tools

[<MemoryDiagnoser>]
[<SimpleJob(iterationCount = 20, warmupCount = 5)>]
type ToolBenchmarks() =

    [<GlobalSetup>]
    member _.Setup() =
        let connStr =
            System.Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
            |> Option.ofObj
            |> Option.defaultValue "Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp"
        let tenant =
            System.Environment.GetEnvironmentVariable("TENANT_ID")
            |> Option.ofObj
            |> Option.defaultValue "default"
        configure connStr tenant

    // ─── Graph management ───

    [<Benchmark(Description = "list_graphs")>]
    member _.ListGraphs() =
        listGraphs {| dummy = None |} |> fun t -> t.GetAwaiter().GetResult() |> ignore

    [<Benchmark(Description = "get_or_create_graph (exists)")>]
    member _.GetOrCreateGraph() =
        getOrCreateGraph { graph_name = "people" } |> fun t -> t.GetAwaiter().GetResult() |> ignore

    // ─── Cypher queries ───

    [<Benchmark(Description = "cypher_query (1 vertex)")>]
    member _.CypherQuerySingle() =
        cypherQuery { graph_name = "people"; query = "MATCH (n:Person) WHERE n.ident = 'dusan.cvetkovic' RETURN n" }
        |> fun t -> t.GetAwaiter().GetResult() |> ignore

    [<Benchmark(Description = "get_schema")>]
    member _.GetSchema() =
        getSchema { graph_name = "people" } |> fun t -> t.GetAwaiter().GetResult() |> ignore

    // ─── Search ───

    [<Benchmark(Description = "search_vertices (limit=2)")>]
    member _.SearchVerticesSmall() =
        searchVertices { graph_name = "people"; label = Some "Person"; property_key = None; property_value = None; limit = Some 2 }
        |> fun t -> t.GetAwaiter().GetResult() |> ignore

    [<Benchmark(Description = "search_vertices (all Person)")>]
    member _.SearchVerticesAll() =
        searchVertices { graph_name = "people"; label = Some "Person"; property_key = None; property_value = None; limit = Some 50 }
        |> fun t -> t.GetAwaiter().GetResult() |> ignore

    [<Benchmark(Description = "search_edges (all)")>]
    member _.SearchEdges() =
        searchEdges { graph_name = "people"; label = None; limit = Some 50 }
        |> fun t -> t.GetAwaiter().GetResult() |> ignore

    // ─── Heavy operations ───

    [<Benchmark(Description = "get_neighbors (depth=1)")>]
    member _.GetNeighbors() =
        getNeighbors { graph_name = "people"; vertex_ident = "dusan.cvetkovic"; depth = Some 1; direction = Some "both" }
        |> fun t -> t.GetAwaiter().GetResult() |> ignore

    [<Benchmark(Description = "export_graph")>]
    member _.ExportGraph() =
        exportGraph { graph_name = "people" } |> fun t -> t.GetAwaiter().GetResult() |> ignore

[<EntryPoint>]
let main args =
    BenchmarkRunner.Run<ToolBenchmarks>(args = args) |> ignore
    0
