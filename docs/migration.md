---
layout: default
title: Migration from Python
nav_order: 5
---

# Migration from Python agemcp

age-mcp is a drop-in replacement for the [Python agemcp](https://github.com/nicobailon/agemcp). Existing data works without migration.

## What's Compatible

| Aspect | Python agemcp | F# age-mcp |
|--------|--------------|-------------|
| Tenant prefix | `t_{TENANT_ID}__` | `t_{TENANT_ID}__` |
| Vertex identifier | `ident` property | `ident` property |
| Edge start | `start_ident` property | `start_ident` property |
| Edge end | `end_ident` property | `end_ident` property |
| Edge identifier | `ident` property | `ident` property |
| Properties | Arbitrary JSON in agtype | Arbitrary JSON in agtype |
| Embeddings table | `vertex_embeddings` | `vertex_embeddings` |
| Vector dimensions | 384 (BAAI/bge-small-en) | Configurable (default 384) |

## Switching

1. Stop the Python agemcp server
2. Point age-mcp at the same database:

```bash
# Python used:
# DB__DSN=postgresql+asyncpg://user:pass@localhost:5435/agemcp

# F# equivalent:
AGE_CONNECTION_STRING="Host=localhost;Port=5435;Database=agemcp;Username=user;Password=pass"
TENANT_ID="default"
```

3. Update your MCP client config:

```json
{
  "mcpServers": {
    "age-mcp": {
      "type": "stdio",
      "command": "age-mcp",
      "env": {
        "AGE_CONNECTION_STRING": "Host=localhost;Port=5435;Database=agemcp;Username=user;Password=pass",
        "TENANT_ID": "default"
      }
    }
  }
}
```

4. Verify: ask your assistant to `list_graphs` -- you should see all your existing graphs.

## Tool Mapping

| Python agemcp | F# age-mcp | Notes |
|---------------|------------|-------|
| `get_or_create_graph` | `get_or_create_graph` | Same |
| `list_graphs` | `list_graphs` | Same |
| `drop_graphs` | `drop_graphs` | Same |
| `upsert_vertex` | `upsert_vertex` | Same |
| `upsert_edge` | `upsert_edge` | Same |
| `upsert_graph` | `upsert_graph` | Same |
| `drop_vertex` | `drop_vertex` | Same |
| `drop_edge` | `drop_edge` | Same |
| `cypher_query` | `cypher_query` | Same |
| `search_vertices` | `search_vertices` | Same |
| `search_edges` | `search_edges` | Same |
| `get_neighbors` | `get_neighbors` | Same |
| `export_graph` | `export_graph` | Same |
| `import_graph` | `import_graph` | Same |
| `generate_visualization` | `generate_visualization` | Same |
| `semantic_search` | `semantic_search` | Needs `EMBEDDING_API_URL` |
| `graph_context` | `graph_context` | Needs `EMBEDDING_API_URL` |
| `sync_to_openbrain` | `sync_to_openbrain` | Same |
| `import_from_openbrain` | `import_from_openbrain` | Same |
| -- | `cypher_write` | New: dedicated write tool |
| -- | `get_schema` | New: label counts |

## Embedding Differences

The Python version uses [fastembed](https://github.com/qdrant/fastembed) (local ONNX model, BAAI/bge-small-en-v1.5, 384 dimensions). The F# version uses an HTTP embedding API (OpenAI-compatible).

If you have existing embeddings in the `vertex_embeddings` table from the Python version, the F# version can query them directly. For new vertices, you need to configure `EMBEDDING_API_URL`.

To use the same model dimensions:

```bash
EMBEDDING_API_URL="http://localhost:11434/v1/embeddings"  # Ollama
EMBEDDING_MODEL="bge-small-en-v1.5"
EMBEDDING_DIMENSIONS=384
```

## What's Not Supported

- **SSE / HTTP transport** -- age-mcp supports stdio only. For Claude Desktop and Claude Code, this is the standard transport.
- **Configurable ident property names** -- Python supports `AGE__IDENT_PROPERTY` etc. age-mcp hardcodes `ident`, `start_ident`, `end_ident` (the defaults).
