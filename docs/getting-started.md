---
layout: default
title: "Getting Started — Install and Connect age-mcp"
description: "Step-by-step guide to install age-mcp, start Apache AGE with Docker, and connect to Claude Desktop or Claude Code via MCP."
nav_order: 2
---

# Getting Started

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL with [Apache AGE](https://age.apache.org/) extension
- Optional: [pgvector](https://github.com/pgvector/pgvector) extension (for semantic search)

## 1. Start the Database

The included Docker setup runs PostgreSQL 17 + AGE 1.6.0 + pgvector:

```bash
git clone https://github.com/Neftedollar/age-mcp.git
cd age-mcp
docker compose up -d
```

This creates a database on port `5435` with user `agemcp` / password `agemcp`.

To customize versions or port:

```bash
PG_MAJOR=16 AGE_VERSION=1.5.0 DB_PORT=5440 docker compose up -d
```

See [Configuration]({% link configuration.md %}) for all options.

## 2. Install age-mcp

### As a global dotnet tool (recommended)

```bash
dotnet tool install --global AgeMcp
```

Then run:

```bash
export AGE_CONNECTION_STRING="Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp"
age-mcp
```

### From source

```bash
cd age-mcp
dotnet run
```

### As a local tool

```bash
dotnet new tool-manifest
dotnet tool install --local AgeMcp
dotnet age-mcp
```

## 3. Connect to Claude

### Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

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

### Claude Code

Add to `.claude/settings.json` or configure via `/mcp`:

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

## 4. Try It

Once connected, ask your AI assistant:

> "Create a graph called 'team' and add three people: Alice (engineer), Bob (designer), Carol (PM). Connect Alice and Bob with a WORKS_WITH edge."

The assistant will call `get_or_create_graph`, `upsert_vertex` (3x), and `upsert_edge` automatically.

> "Show me the schema of the team graph"

Calls `get_schema` -- returns label counts.

> "Export the team graph as JSON"

Calls `export_graph` -- returns all vertices and edges.

> "Generate a visualization of the team graph"

Calls `generate_visualization` -- returns standalone HTML with an interactive vis.js network diagram.
