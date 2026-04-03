---
layout: default
title: "FAQ"
description: "Frequently asked questions about age-mcp: compatibility, setup, multi-tenancy, semantic search, and differences from Python agemcp."
nav_order: 7
---

# Frequently Asked Questions

## General

### What is age-mcp?

age-mcp is an open-source MCP server that connects AI assistants (Claude, ChatGPT, Copilot) to Apache AGE graph databases on PostgreSQL. It exposes 21 tools for graph CRUD, Cypher queries, semantic search, and visualization.

### What AI assistants does age-mcp work with?

Any MCP-compatible client: Claude Desktop, Claude Code, ChatGPT (with MCP plugin), GitHub Copilot, and custom MCP clients. The server uses the stdio transport which is the MCP standard.

### Is age-mcp free?

Yes. MIT license, fully open source. The source code is on [GitHub](https://github.com/Neftedollar/age-mcp).

### What language is age-mcp written in?

F# on .NET 10. The entire server is about 1,300 lines of code.

## Database

### Does age-mcp work with Neo4j?

No. age-mcp is specifically for [Apache AGE](https://age.apache.org/), which runs as a PostgreSQL extension and uses Cypher for graph queries. For Neo4j, use a Neo4j-specific MCP server.

### Can I use age-mcp without Docker?

Yes. Any PostgreSQL instance with the Apache AGE extension installed will work. Docker is provided for convenience — it bundles PostgreSQL 17, Apache AGE 1.6.0, and pgvector.

### What version of PostgreSQL is required?

PostgreSQL 14 or later with the Apache AGE extension. The included Docker setup uses PostgreSQL 17, but you can configure any version that AGE supports.

## Setup

### How do I install age-mcp?

```bash
dotnet tool install --global AgeMcp
```

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Then configure the connection string and run `age-mcp`.

### How do I connect age-mcp to Claude?

Add this to your Claude Desktop or Claude Code MCP configuration:

```json
{
  "mcpServers": {
    "age-mcp": {
      "type": "stdio",
      "command": "age-mcp",
      "env": {
        "AGE_CONNECTION_STRING": "Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp"
      }
    }
  }
}
```

See [Getting Started]({% link getting-started.md %}) for the full setup guide.

## Multi-Tenancy

### How does multi-tenancy work?

Set the `TENANT_ID` environment variable. All graph names are automatically prefixed with `t_{TENANT_ID}__`. For example, with `TENANT_ID=acme`, a graph called `people` is stored as `t_acme__people`. Different tenants share the same database but cannot see each other's graphs.

### Can multiple age-mcp instances share a database?

Yes. Each instance with a different `TENANT_ID` sees only its own graphs. This is how multi-tenancy works — graph names are namespaced by tenant prefix.

## Semantic Search

### Does semantic search require OpenAI?

No. Any OpenAI-compatible embedding API works: OpenAI, Azure OpenAI, Ollama (local), or any provider with the `/v1/embeddings` endpoint. Set `EMBEDDING_API_URL` to your provider's endpoint.

### What is Graph RAG?

The `graph_context` tool implements Graph RAG (Retrieval-Augmented Generation with graph context). It finds relevant vertices via vector similarity search, then expands each result with N-hop graph neighbors. This gives the AI assistant richer context than flat document search.

### Can I use existing embeddings from the Python version?

Yes. If the Python agemcp already generated embeddings in the `vertex_embeddings` table, the F# age-mcp can query them directly. For new vertices, configure `EMBEDDING_API_URL`.

## Migration

### Can I switch from the Python agemcp to age-mcp?

Yes, it's a drop-in replacement. Same database schema, same tenant prefix (`t_{TENANT_ID}__`), same property names (`ident`, `start_ident`, `end_ident`). Point age-mcp at the same database and your existing graphs work without migration.

See [Migration from Python]({% link migration.md %}) for the full guide.

### What's different from the Python agemcp?

- Written in F# instead of Python
- Installed as a .NET dotnet tool instead of pip
- Uses HTTP embedding API instead of local fastembed
- Stdio transport only (no SSE/HTTP)
- Adds `cypher_write` and `get_schema` tools
- Sub-millisecond cached metadata queries
