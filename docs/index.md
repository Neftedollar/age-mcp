---
layout: default
title: "age-mcp: MCP Server for Apache AGE Graph Databases"
description: "Connect AI assistants (Claude, ChatGPT, Copilot) to Apache AGE graph databases via Model Context Protocol. 21 tools: Cypher queries, graph CRUD, semantic search, visualization. F#/.NET 10."
nav_order: 1
---

# age-mcp

MCP server for [Apache AGE](https://age.apache.org/) graph databases.
{: .fs-6 .fw-300 }

Lets AI assistants (Claude, ChatGPT, Copilot) query and mutate graph data via the [Model Context Protocol](https://modelcontextprotocol.io/). Built with F# on .NET 10 -- 1,300 lines, 21 tools, zero Python dependencies.

[Get Started]({% link getting-started.md %}){: .btn .btn-primary .fs-5 .mb-4 .mb-md-0 .mr-2 }
[View on GitHub](https://github.com/Neftedollar/age-mcp){: .btn .fs-5 .mb-4 .mb-md-0 }

---

## What is age-mcp?

age-mcp is an [MCP server](https://modelcontextprotocol.io/) that gives AI assistants direct access to [Apache AGE](https://age.apache.org/) graph databases running on PostgreSQL. Instead of writing Cypher queries manually, you describe what you want in natural language and the AI calls the right tools automatically.

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
# Start PostgreSQL + Apache AGE
docker compose up -d

# Install the MCP server
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

## Use Cases

- **Knowledge graphs** -- build and query knowledge bases from AI conversations
- **People & org charts** -- model teams, roles, and relationships
- **Project tracking** -- link tasks, dependencies, and milestones in a graph
- **RAG with graph context** -- semantic search over vertices + N-hop neighbor expansion for richer LLM context
- **Data exploration** -- ask questions about existing graph data in natural language
