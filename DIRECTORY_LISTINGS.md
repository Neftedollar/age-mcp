# MCP Directory Submission Texts

Prepared listings for submitting age-mcp to MCP directories.

---

## Common Fields

- **Name**: age-mcp
- **Author**: Roman Melnikov (@Neftedollar)
- **Repository**: https://github.com/Neftedollar/age-mcp
- **Documentation**: https://neftedollar.github.io/age-mcp
- **NuGet**: https://www.nuget.org/packages/AgeMcp
- **License**: MIT
- **Language**: F# (.NET 10)
- **Transport**: stdio
- **Category**: Database / Graph Database

---

## Short Description (one line)

MCP server for Apache AGE graph databases — 21 tools for Cypher queries, graph CRUD, semantic search, Graph RAG, and visualization. F#/.NET.

## Medium Description (2-3 sentences)

age-mcp connects AI assistants (Claude, ChatGPT, Copilot) to Apache AGE graph databases on PostgreSQL via Model Context Protocol. 21 tools: graph management, vertex/edge CRUD, Cypher queries, batch transactions, semantic vector search (pgvector), Graph RAG, vis.js visualization, and export/import. Install with `dotnet tool install --global AgeMcp`.

## Long Description (paragraph)

age-mcp is an open-source MCP server that gives AI assistants full access to Apache AGE graph databases running on PostgreSQL. It exposes 21 tools covering the complete graph workflow: create and manage graphs, upsert vertices and edges (with transactional batch support), execute Cypher queries, search by label/property, traverse N-hop neighbors, export/import as JSON, generate interactive vis.js visualizations, and perform semantic vector search with Graph RAG (retrieval-augmented generation using pgvector). Built with F# on .NET 10, it delivers BenchmarkDotNet-verified performance: cached metadata queries in 62 ns, Cypher execution in 1 ms. Supports multi-tenancy, any OpenAI-compatible embedding API, and is data-compatible with the Python agemcp — existing graphs work without migration. Install as a dotnet tool: `dotnet tool install --global AgeMcp`.

---

## Tool List (for directories that show tools)

| Tool | Description |
|------|-------------|
| get_or_create_graph | Create or get a graph by name |
| list_graphs | List all graphs (tenant-scoped, cached) |
| drop_graphs | Drop one or more graphs |
| upsert_vertex | Insert or update vertex (merge on ident) |
| upsert_edge | Insert or update directed edge |
| upsert_graph | Batch upsert vertices + edges (transactional) |
| drop_vertex | Remove vertex and connected edges |
| drop_edge | Remove edge by ident |
| cypher_query | Execute read Cypher query |
| cypher_write | Execute write Cypher, returns affected count |
| search_vertices | Search by label and/or property |
| search_edges | Search edges by label |
| get_neighbors | N-hop traversal (1-5, directional) |
| get_schema | Node labels and counts (cached) |
| export_graph | Export graph as JSON |
| import_graph | Import from JSON |
| generate_visualization | Interactive vis.js HTML |
| semantic_search | Vector similarity search (pgvector) |
| graph_context | Graph RAG: semantic seeds + neighbor expansion |
| sync_to_openbrain | Export as OpenBrain memories |
| import_from_openbrain | Build graph from OpenBrain memories |

---

## Install / Config Block

```bash
dotnet tool install --global AgeMcp
```

```json
{
  "mcpServers": {
    "age-mcp": {
      "type": "stdio",
      "command": "age-mcp",
      "env": {
        "AGE_CONNECTION_STRING": "Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp",
        "TENANT_ID": "default"
      }
    }
  }
}
```

---

## Tags / Keywords

mcp, apache-age, graph-database, postgresql, cypher, fsharp, dotnet, knowledge-graph, graph-rag, semantic-search, pgvector, visualization, ai-tools, claude, chatgpt

---

## awesome-mcp-servers PR Text

### Section: Database

**[age-mcp](https://github.com/Neftedollar/age-mcp)** - Apache AGE graph database server with 21 tools: Cypher queries, graph CRUD, batch transactions, semantic search (pgvector), Graph RAG, and vis.js visualization. F#/.NET.

---

## LobeHub PR Text (for their MCP plugins list)

```json
{
  "identifier": "age-mcp",
  "title": "Apache AGE Graph Database",
  "description": "MCP server for Apache AGE graph databases on PostgreSQL. 21 tools: Cypher queries, graph CRUD, batch transactions, semantic search, Graph RAG, visualization.",
  "author": "Neftedollar",
  "homepage": "https://github.com/Neftedollar/age-mcp",
  "category": "database"
}
```
