# age-mcp

MCP server for Apache AGE graph databases. Query and mutate graph data from any AI assistant (Claude, ChatGPT, etc.) via the Model Context Protocol.

Built with **F#**, **Fyper** (type-safe Cypher), and **FsMcp** (F# MCP framework).

## Tools

| Tool | Description |
|------|-------------|
| `cypher_query` | Execute a read-only Cypher query, return results |
| `cypher_write` | Execute a write Cypher query (CREATE, SET, DELETE) |
| `search_nodes` | Search nodes by label + property + value |
| `get_schema` | Get all node labels and counts |
| `list_graphs` | List available graphs in the database |

## Setup

### Prerequisites

- PostgreSQL with [Apache AGE](https://age.apache.org/) extension
- .NET 10.0 SDK

### Run

```bash
export AGE_CONNECTION_STRING="Host=localhost;Port=5432;Database=testdb;Username=test;Password=test"
export AGE_GRAPH_NAME="my_graph"
dotnet run
```

### Claude Desktop config

```json
{
  "mcpServers": {
    "age": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/age-mcp"],
      "env": {
        "AGE_CONNECTION_STRING": "Host=localhost;Database=mydb;Username=user;Password=pass",
        "AGE_GRAPH_NAME": "my_graph"
      }
    }
  }
}
```

## Example Usage

Once connected, the AI assistant can:

```
"Show me all Person nodes in the graph"
→ calls cypher_query { cypher: "MATCH (p:Person) RETURN p" }

"Create a new person named Alice, age 30"
→ calls cypher_write { cypher: "CREATE (:Person {name: 'Alice', age: 30})" }

"Find everyone named Tom"
→ calls search_nodes { label: "Person", property: "name", value: "Tom" }

"What's the schema of this graph?"
→ calls get_schema {}
```

## License

MIT
