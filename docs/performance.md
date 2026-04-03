---
layout: default
title: Performance
nav_order: 6
---

# Performance

Benchmarked with [BenchmarkDotNet](https://benchmarkdotnet.org/) on Apple M1 Pro, .NET 10.0.5, Release build.

## Results

| Operation | Mean | Allocated | Notes |
|-----------|------|-----------|-------|
| `list_graphs` | **62 ns** | 216 B | TTL cached (10s) |
| `get_schema` | **117 ns** | 344 B | TTL cached (10s) |
| `get_or_create_graph` (exists) | 775 us | 5.6 KB | SQL check |
| `cypher_query` (1 vertex) | 1.0 ms | 58 KB | Single MATCH + RETURN |
| `search_vertices` (limit=2) | 1.0 ms | 36 KB | |
| `search_vertices` (all, 16) | 1.3 ms | 194 KB | |
| `search_edges` (all, 22) | 1.6 ms | 153 KB | |
| `get_neighbors` (depth=1) | **92 ms** | 233 KB | Directed UNION + batch connection |
| `export_graph` (40 entities) | **71 ms** | 472 KB | Batch connection |

## Optimizations Applied

### TTL Cache (list_graphs, get_schema)

Metadata queries are cached for 10 seconds. Cache is automatically invalidated on write operations (`upsert_vertex`, `drop_graphs`, etc.). This reduces repeated calls from ~1ms to ~100ns.

### Connection Batching (get_neighbors, export_graph, generate_visualization)

Operations that run multiple Cypher queries reuse a single database connection. This eliminates the per-query overhead of `LOAD 'age'` + `SET search_path` (~0.5ms per connection).

### Directed Edge Queries (get_neighbors)

Bidirectional edge patterns (`-[e]-`) are slow in Apache AGE. `get_neighbors` uses directed `UNION ALL` queries instead:

```cypher
-- Before: ~260ms (bidirectional scan)
MATCH (start {ident: 'x'})-[e]-(neighbor) RETURN e

-- After: ~92ms (directed UNION)
MATCH (start {ident: 'x'})-[e]->(neighbor) RETURN e
UNION ALL
MATCH (start {ident: 'x'})<-[e]-(neighbor) RETURN e
```

### ReadyToRun

The tool is published with `PublishReadyToRun=true` for faster cold start via pre-JIT compilation.

## Running Benchmarks

```bash
cd bench
AGE_CONNECTION_STRING="Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp" \
  dotnet run -c Release
```

Results are written to `bench/BenchmarkDotNet.Artifacts/`.
