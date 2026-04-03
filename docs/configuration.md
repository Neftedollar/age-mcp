---
layout: default
title: Configuration
nav_order: 4
---

# Configuration

All configuration is via environment variables.

## MCP Server

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `AGE_CONNECTION_STRING` | yes | `Host=localhost;Port=5432;...` | [Npgsql connection string](https://www.npgsql.org/doc/connection-string-parameters.html) |
| `TENANT_ID` | no | `default` | Tenant prefix for multi-tenancy. All graph names are stored as `t_{TENANT_ID}__{graph_name}` |

### Connection String Examples

```bash
# Local Docker (default setup)
AGE_CONNECTION_STRING="Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp"

# Remote server with SSL
AGE_CONNECTION_STRING="Host=db.example.com;Port=5432;Database=graphs;Username=app;Password=secret;SSL Mode=Require"

# Connection pooling
AGE_CONNECTION_STRING="Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp;Minimum Pool Size=2;Maximum Pool Size=10"
```

## Embeddings (Optional)

Required only for `semantic_search` and `graph_context` tools.

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `EMBEDDING_API_URL` | for search | -- | OpenAI-compatible embedding endpoint |
| `EMBEDDING_API_KEY` | no | -- | Bearer token for the API |
| `EMBEDDING_MODEL` | no | `text-embedding-3-small` | Model name sent in the request |
| `EMBEDDING_DIMENSIONS` | no | `384` | Vector dimensions (must match model output) |

### Embedding API Examples

```bash
# OpenAI
EMBEDDING_API_URL="https://api.openai.com/v1/embeddings"
EMBEDDING_API_KEY="sk-..."
EMBEDDING_MODEL="text-embedding-3-small"
EMBEDDING_DIMENSIONS=1536

# Local Ollama
EMBEDDING_API_URL="http://localhost:11434/v1/embeddings"
EMBEDDING_MODEL="nomic-embed-text"
EMBEDDING_DIMENSIONS=768

# Azure OpenAI
EMBEDDING_API_URL="https://myinstance.openai.azure.com/openai/deployments/embedding/embeddings?api-version=2024-02-01"
EMBEDDING_API_KEY="..."
EMBEDDING_MODEL="text-embedding-3-small"
```

## Docker

The `docker-compose.yml` accepts these variables (via `.env` file or environment):

| Variable | Default | Description |
|----------|---------|-------------|
| `PG_MAJOR` | `17` | PostgreSQL major version |
| `AGE_VERSION` | `1.6.0` | Apache AGE version |
| `DB_PORT` | `5435` | Host port mapped to PostgreSQL |
| `POSTGRES_USER` | `agemcp` | Database user |
| `POSTGRES_PASSWORD` | `agemcp` | Database password |
| `POSTGRES_DB` | `agemcp` | Database name |

Copy `.env.example` to `.env` and edit as needed:

```bash
cp .env.example .env
```

## Multi-Tenancy

The `TENANT_ID` variable controls graph namespace isolation. All graphs are prefixed with `t_{TENANT_ID}__`:

| TENANT_ID | Graph name passed | Actual AGE graph |
|-----------|-------------------|------------------|
| `default` | `people` | `t_default__people` |
| `acme` | `people` | `t_acme__people` |
| `dev` | `test` | `t_dev__test` |

This is transparent -- tools accept and return unprefixed names. Multiple age-mcp instances with different `TENANT_ID` values share the same database safely.
