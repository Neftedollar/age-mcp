---
layout: default
title: Tools Reference
nav_order: 3
---

# Tools Reference

age-mcp exposes 21 MCP tools. All tools that operate on a graph take `graph_name` as a required parameter. Graph names are automatically scoped with the tenant prefix (`t_{TENANT_ID}__`).

---

## Graph Management

### get_or_create_graph

Get or create a graph by name.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Name of the graph |

Returns `{"name": "...", "status": "created"}` or `{"name": "...", "status": "exists"}`.

### list_graphs

List all available graphs for the current tenant. Results are cached for 10 seconds.

No required parameters.

### drop_graphs

Drop one or more graphs and all their data.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_names` | string | yes | JSON array of graph names: `["g1", "g2"]` |

---

## Vertices

### upsert_vertex

Insert or update a vertex. Merges on `ident` -- existing properties are preserved, new ones are added or updated.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `vertex_ident` | string | yes | Unique vertex identifier |
| `label` | string | no | Vertex label (default: `"Node"`) |
| `properties` | string | no | JSON object of properties |

**Example:**
```json
{
  "graph_name": "team",
  "vertex_ident": "alice",
  "label": "Person",
  "properties": "{\"name\": \"Alice\", \"role\": \"engineer\"}"
}
```

### drop_vertex

Remove a vertex and all its connected edges.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `vertex_ident` | string | yes | Vertex identifier to delete |

---

## Edges

### upsert_edge

Insert or update a directed edge between two vertices (matched by ident). The edge `ident`, `start_ident`, and `end_ident` properties are set automatically.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `label` | string | yes | Edge type / relationship label |
| `edge_start_ident` | string | yes | Start vertex ident |
| `edge_end_ident` | string | yes | End vertex ident |
| `properties` | string | no | JSON object of additional properties |

### drop_edge

Remove an edge by its ident.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `edge_ident` | string | yes | Edge identifier |

---

## Batch Operations

### upsert_graph

Deep-merge vertices and edges into a graph in a single transaction.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `vertices` | string | yes | JSON array of vertex objects |
| `edges` | string | yes | JSON array of edge objects |

**Vertex format:** `{"ident": "...", "label": "...", "properties": {...}}` or flat `{"ident": "...", "label": "...", "name": "..."}`.

**Edge format:** `{"label": "...", "start_ident": "...", "end_ident": "...", "properties": {...}}`.

---

## Query

### cypher_query

Execute a read Cypher query with RETURN clause. Results are returned as JSON.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `query` | string | yes | Cypher query |

**Example:**
```
MATCH (p:Person)-[r:WORKS_WITH]->(c:Person) RETURN p, r, c LIMIT 10
```

### cypher_write

Execute a write Cypher query (CREATE, MERGE, SET, DELETE). Returns `{"affected": N}`.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `query` | string | yes | Cypher mutation |

### search_vertices

Search vertices by label and/or property value.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `label` | string | no | Filter by vertex label |
| `property_key` | string | no | Property name to match |
| `property_value` | string | no | Property value to match |
| `limit` | int | no | Max results (default: 50) |

### search_edges

Search edges by label.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `label` | string | no | Filter by edge label |
| `limit` | int | no | Max results (default: 50) |

### get_neighbors

Get N-hop neighbors of a vertex.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `vertex_ident` | string | yes | Start vertex ident |
| `depth` | int | no | Hops 1-5 (default: 1) |
| `direction` | string | no | `"out"`, `"in"`, or `"both"` (default) |

Returns `{"vertices": [...], "edges": [...]}`.

### get_schema

Get all node labels and their counts. Results are cached for 10 seconds.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |

---

## Export / Import

### export_graph

Export entire graph as JSON.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |

Returns `{"name": "...", "vertices": [...], "edges": [...]}`.

### import_graph

Import a graph from JSON. Creates the graph if it doesn't exist, then upserts all vertices and edges.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `vertices` | string | yes | JSON array of vertex objects |
| `edges` | string | yes | JSON array of edge objects |

---

## Visualization

### generate_visualization

Generate an interactive HTML visualization using [vis.js](https://visjs.github.io/vis-network/). Save the returned HTML to a file and open in a browser.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |

---

## Semantic Search

Requires `EMBEDDING_API_URL` to be configured (any OpenAI-compatible embedding API). Embeddings are synced lazily on the first search call.

### semantic_search

Vector similarity search over vertex content.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `query` | string | yes | Search text |
| `limit` | int | no | Max results (default: 10) |

### graph_context

Graph RAG: finds seed vertices via semantic search, then expands each seed with N-hop neighbors.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `query` | string | yes | Search text |
| `top_k` | int | no | Number of seeds (default: 5) |
| `depth` | int | no | Expansion hops (default: 1) |

Returns `{"seeds": [...], "context": [...]}`.

---

## OpenBrain Bridge

### sync_to_openbrain

Export graph vertices as an OpenBrain memories payload. Returns `{"memories": [...]}` for use with `openbrain.store_batch`.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `category` | string | no | Memory category (default: `"observation"`) |
| `tag_prefix` | string | no | Tag prefix (default: `"graph"`) |

### import_from_openbrain

Build a graph from OpenBrain memories. Creates vertices and optionally connects them by shared tags.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `graph_name` | string | yes | Graph name |
| `memories` | string | yes | JSON array of memory objects |
| `connect_by_tags` | string | no | `"true"` (default) or `"false"` |
