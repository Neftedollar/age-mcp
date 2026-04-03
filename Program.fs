module AgeMcp.Program

open FsMcp.Core
open FsMcp.Core.Validation
open FsMcp.Server
open AgeMcp.Tools

[<EntryPoint>]
let main argv =
    // Configure from environment or defaults
    let connStr =
        System.Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        |> Option.ofObj
        |> Option.defaultValue "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test"
    let graph =
        System.Environment.GetEnvironmentVariable("AGE_GRAPH_NAME")
        |> Option.ofObj
        |> Option.defaultValue "default"

    configure connStr graph

    let server = mcpServer {
        name "age-mcp"
        version "1.0.0"

        tool (TypedTool.define<QueryArgs>
            "cypher_query"
            "Execute a read-only Cypher query against the Apache AGE graph database. Returns results as text."
            executeCypher |> unwrapResult)

        tool (TypedTool.define<QueryArgs>
            "cypher_write"
            "Execute a write Cypher query (CREATE, SET, DELETE, MERGE) against Apache AGE. Returns affected count."
            executeWrite |> unwrapResult)

        tool (TypedTool.define<SearchArgs>
            "search_nodes"
            "Search for nodes by label and property value. Example: label=Person, property=name, value=Tom"
            searchNodes |> unwrapResult)

        tool (TypedTool.define<{| dummy: string option |}>
            "get_schema"
            "Get the graph schema: all node labels and their counts."
            getSchema |> unwrapResult)

        tool (TypedTool.define<{| dummy: string option |}>
            "list_graphs"
            "List all available graphs in the AGE database."
            listGraphs |> unwrapResult)

        useStdio
    }

    Server.run server |> fun t -> t.GetAwaiter().GetResult()
    0
