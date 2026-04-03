---
layout: default
title: Home
nav_order: 1
---

# age-mcp

MCP server for [Apache AGE](https://age.apache.org/) graph databases.
{: .fs-6 .fw-300 }

Lets AI assistants (Claude, ChatGPT, Copilot) query and mutate graph data via the [Model Context Protocol](https://modelcontextprotocol.io/). Built with F# on .NET 10 -- 1,300 lines, 21 tools, zero Python dependencies.

[Get Started]({% link getting-started.md %}){: .btn .btn-primary .fs-5 .mb-4 .mb-md-0 .mr-2 }
[View on GitHub](https://github.com/Neftedollar/age-mcp){: .btn .fs-5 .mb-4 .mb-md-0 }

---

## Features

- **21 MCP tools** -- graph CRUD, Cypher queries, search, export/import, visualization, semantic search, Graph RAG
- **Data-compatible** with [agemcp](https://github.com/nicobailon/agemcp) (Python) -- same tenant prefix, same property schema, no migration needed
- **Sub-millisecond metadata queries** -- cached `list_graphs` and `get_schema` respond in ~100 ns
- **Transactional batch operations** -- `upsert_graph` runs all mutations in a single transaction
- **Vector search** -- pgvector-backed `semantic_search` and `graph_context` (Graph RAG) with any OpenAI-compatible embedding API
- **Interactive visualization** -- `generate_visualization` produces standalone vis.js HTML
- **dotnet tool** -- install globally with `dotnet tool install`, no build required

## Quick Start

```bash
# Start the database
docker compose up -d

# Install
dotnet tool install --global AgeMcp

# Run
export AGE_CONNECTION_STRING="Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp"
age-mcp
```

Then add to your Claude Desktop or Claude Code config:

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
