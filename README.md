# age-mcp

MCP server for [Apache AGE](https://age.apache.org/) graph databases. Lets AI assistants (Claude, ChatGPT, Copilot, etc.) query and mutate graph data via the [Model Context Protocol](https://modelcontextprotocol.io/).

Built with F# on .NET 10 -- 1,300 lines, 21 tools, zero Python dependencies.

## Quick Start

```bash
# 1. Start the database
docker compose up -d

# 2. Install the tool
dotnet tool install --global --add-source bin/Release AgeMcp

# 3. Run
AGE_CONNECTION_STRING="Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp" age-mcp
```

### Claude Desktop / Claude Code

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

## Tools (21)

### Graph Management
| Tool | Description |
|------|-------------|
| `get_or_create_graph` | Get or create a graph by name |
| `list_graphs` | List all graphs (tenant-scoped) |
| `drop_graphs` | Drop one or more graphs |

### Vertices & Edges
| Tool | Description |
|------|-------------|
| `upsert_vertex` | Insert or update a vertex (merge on ident) |
| `upsert_edge` | Insert or update a directed edge |
| `upsert_graph` | Batch upsert vertices + edges (transactional) |
| `drop_vertex` | Remove a vertex and all its edges |
| `drop_edge` | Remove an edge by ident |

### Query
| Tool | Description |
|------|-------------|
| `cypher_query` | Execute a read Cypher query |
| `cypher_write` | Execute a write Cypher query, returns affected count |
| `search_vertices` | Search by label and/or property |
| `search_edges` | Search edges by label |
| `get_neighbors` | N-hop traversal (1-5 hops, directional) |
| `get_schema` | All node labels and counts |

### Export / Import
| Tool | Description |
|------|-------------|
| `export_graph` | Export graph as JSON |
| `import_graph` | Import from JSON (creates graph if needed) |

### Visualization & Search
| Tool | Description |
|------|-------------|
| `generate_visualization` | Interactive vis.js HTML graph |
| `semantic_search` | Vector similarity search (pgvector) |
| `graph_context` | Graph RAG: semantic seeds + N-hop expansion |

### OpenBrain Bridge
| Tool | Description |
|------|-------------|
| `sync_to_openbrain` | Export vertices as OpenBrain memories |
| `import_from_openbrain` | Build graph from OpenBrain memories |

## Configuration

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `AGE_CONNECTION_STRING` | yes | localhost test DB | Npgsql connection string |
| `TENANT_ID` | no | `default` | Tenant prefix for graph names |
| `EMBEDDING_API_URL` | no | -- | OpenAI-compatible embedding API |
| `EMBEDDING_API_KEY` | no | -- | API key for embeddings |
| `EMBEDDING_MODEL` | no | `text-embedding-3-small` | Embedding model name |
| `EMBEDDING_DIMENSIONS` | no | `384` | Vector dimensions |

## Docker

The included Docker setup runs PostgreSQL 17 + Apache AGE 1.6.0 + pgvector:

```bash
docker compose up -d
```

Versions and credentials are configurable via `.env` (see `.env.example`):

```bash
PG_MAJOR=17  AGE_VERSION=1.6.0  DB_PORT=5435  docker compose up -d
```

## Data Compatibility

Designed as a drop-in replacement for [agemcp](https://github.com/Neftedollar/agemcp) (Python). Same tenant prefix (`t_{TENANT_ID}__`), same vertex `ident` property, same edge `start_ident`/`end_ident` properties. Existing data works without migration.

## Performance

BenchmarkDotNet on Apple M1 Pro, .NET 10.0.5:

| Operation | Latency | Allocated |
|-----------|---------|-----------|
| list_graphs | 62 ns | 216 B |
| get_schema | 117 ns | 344 B |
| cypher_query (1 vertex) | 1.0 ms | 58 KB |
| search_vertices | 1.0 ms | 36 KB |
| get_neighbors (depth=1) | 92 ms | 233 KB |
| export_graph (40 entities) | 71 ms | 472 KB |

## Building from Source

```bash
git clone https://github.com/Neftedollar/age-mcp.git
cd age-mcp
dotnet build

# Run directly
dotnet run

# Or install as tool
dotnet pack -c Release
dotnet tool install --global --add-source bin/Release AgeMcp
```

Dependencies ([FsMcp.Core](https://www.nuget.org/packages/FsMcp.Core), [FsMcp.Server](https://www.nuget.org/packages/FsMcp.Server), [Fyper](https://www.nuget.org/packages/Fyper)) are restored from NuGet automatically.

## License

MIT
