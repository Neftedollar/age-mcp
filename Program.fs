module AgeMcp.Program

open FsMcp.Core
open FsMcp.Core.Validation
open FsMcp.Server
open AgeMcp.Tools

[<EntryPoint>]
let main _argv =
    let connStr =
        System.Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        |> Option.ofObj
        |> Option.defaultValue "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test"
    let tenant =
        System.Environment.GetEnvironmentVariable("TENANT_ID")
        |> Option.ofObj
        |> Option.defaultValue "default"

    AgeMcp.Config.configure connStr tenant

    // Embedding configuration (optional — needed for semantic_search and graph_context)
    let embUrl = System.Environment.GetEnvironmentVariable("EMBEDDING_API_URL") |> Option.ofObj
    let embKey = System.Environment.GetEnvironmentVariable("EMBEDDING_API_KEY") |> Option.ofObj
    let embModel =
        System.Environment.GetEnvironmentVariable("EMBEDDING_MODEL")
        |> Option.ofObj |> Option.defaultValue "text-embedding-3-small"
    let embDim =
        System.Environment.GetEnvironmentVariable("EMBEDDING_DIMENSIONS")
        |> Option.ofObj |> Option.bind (fun s -> match System.Int32.TryParse(s) with true, v -> Some v | _ -> None)
        |> Option.defaultValue 384

    AgeMcp.Embeddings.configureEmbeddings embUrl embKey embModel embDim

    let server = mcpServer {
        name "age-mcp"
        version "2.0.0"

        // ─── Graph management ───

        tool (TypedTool.define<GraphNameArgs>
            "get_or_create_graph"
            "Get or create a graph by name. Returns {name, status} where status is 'created' or 'exists'."
            getOrCreateGraph |> unwrapResult)

        tool (TypedTool.define<{| dummy: string option |}>
            "list_graphs"
            "List all available graphs in the database (tenant-scoped)."
            listGraphs |> unwrapResult)

        tool (TypedTool.define<DropGraphsArgs>
            "drop_graphs"
            "Drop one or more graphs. Provide graph_names as a JSON array: [\"g1\", \"g2\"]."
            dropGraphs |> unwrapResult)

        // ─── Vertex operations ───

        tool (TypedTool.define<UpsertVertexArgs>
            "upsert_vertex"
            "Insert or update a vertex. Merges on ident — existing properties are preserved, new ones are added/updated. Properties: optional JSON object string."
            upsertVertex |> unwrapResult)

        tool (TypedTool.define<DropVertexArgs>
            "drop_vertex"
            "Remove a vertex and all its connected edges by ident."
            dropVertex |> unwrapResult)

        // ─── Edge operations ───

        tool (TypedTool.define<UpsertEdgeArgs>
            "upsert_edge"
            "Insert or update a directed edge between two vertices (matched by ident). Edge ident, start_ident, end_ident are set automatically. Properties: optional JSON object string."
            upsertEdge |> unwrapResult)

        tool (TypedTool.define<DropEdgeArgs>
            "drop_edge"
            "Remove an edge by its ident."
            dropEdge |> unwrapResult)

        // ─── Batch operations ───

        tool (TypedTool.define<UpsertGraphArgs>
            "upsert_graph"
            "Deep-merge vertices and edges into a graph (transactional). vertices: JSON array — each needs 'ident' and 'label'. edges: JSON array — each needs 'label', 'start_ident', 'end_ident'."
            upsertGraph |> unwrapResult)

        // ─── Query operations ───

        tool (TypedTool.define<CypherQueryArgs>
            "cypher_query"
            "Execute a read Cypher query (with RETURN). Results returned as JSON."
            cypherQuery |> unwrapResult)

        tool (TypedTool.define<CypherQueryArgs>
            "cypher_write"
            "Execute a write Cypher query (CREATE, SET, DELETE, MERGE). Returns {affected: N}. Use cypher_query if you need results back."
            cypherWrite |> unwrapResult)

        tool (TypedTool.define<SearchVerticesArgs>
            "search_vertices"
            "Search vertices by label and/or property. All filters optional. Default limit: 50."
            searchVertices |> unwrapResult)

        tool (TypedTool.define<SearchEdgesArgs>
            "search_edges"
            "Search edges by label. Default limit: 50."
            searchEdges |> unwrapResult)

        tool (TypedTool.define<GetNeighborsArgs>
            "get_neighbors"
            "Get N-hop neighbors of a vertex. direction: 'out', 'in', or 'both' (default). depth: 1-5 (default 1). Returns {vertices, edges}."
            getNeighbors |> unwrapResult)

        // ─── Export / Import ───

        tool (TypedTool.define<GraphNameArgs>
            "export_graph"
            "Export a graph as JSON: {name, vertices, edges}."
            exportGraph |> unwrapResult)

        tool (TypedTool.define<ImportGraphArgs>
            "import_graph"
            "Import a graph from JSON. Creates the graph if it doesn't exist, then upserts vertices and edges. Same format as upsert_graph."
            importGraph |> unwrapResult)

        // ─── Schema ───

        tool (TypedTool.define<GraphNameArgs>
            "get_schema"
            "Get graph schema: all node labels and their counts."
            getSchema |> unwrapResult)

        // ─── Visualization ───

        tool (TypedTool.define<GraphNameArgs>
            "generate_visualization"
            "Generate an interactive vis.js HTML visualization of the graph. Returns HTML string — save to a file and open in browser."
            generateVisualization |> unwrapResult)

        // ─── Semantic search (requires EMBEDDING_API_URL) ───

        tool (TypedTool.define<SemanticSearchArgs>
            "semantic_search"
            "Vector similarity search over vertex content. Requires EMBEDDING_API_URL. Syncs embeddings lazily on first call."
            semanticSearch |> unwrapResult)

        tool (TypedTool.define<GraphContextArgs>
            "graph_context"
            "Graph RAG: semantic search for seed vertices, then expand with N-hop neighbors. Returns {seeds, context}."
            graphContext |> unwrapResult)

        // ─── OpenBrain bridge ───

        tool (TypedTool.define<SyncToOpenBrainArgs>
            "sync_to_openbrain"
            "Export graph vertices as OpenBrain memories payload. Returns {memories: [...]} for use with openbrain.store_batch."
            syncToOpenbrain |> unwrapResult)

        tool (TypedTool.define<ImportFromOpenBrainArgs>
            "import_from_openbrain"
            "Build graph from OpenBrain memories. Creates vertices and optionally connects by shared tags. memories: JSON array string."
            importFromOpenbrain |> unwrapResult)

        useStdio
    }

    Server.run server |> fun t -> t.GetAwaiter().GetResult()
    0
