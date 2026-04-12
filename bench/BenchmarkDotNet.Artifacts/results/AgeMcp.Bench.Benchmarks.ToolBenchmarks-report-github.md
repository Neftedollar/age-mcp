```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD DEBUG
  Job-QAJHKF : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD

IterationCount=20  WarmupCount=5  

```
| Method                         | Mean             | Error            | StdDev           | Gen0    | Gen1   | Allocated |
|------------------------------- |-----------------:|-----------------:|-----------------:|--------:|-------:|----------:|
| list_graphs                    |         62.15 ns |         1.199 ns |         1.283 ns |  0.0343 |      - |     216 B |
| &#39;get_or_create_graph (exists)&#39; |    774,622.03 ns |    38,559.638 ns |    42,858.936 ns |       - |      - |    5591 B |
| &#39;cypher_query (1 vertex)&#39;      |  1,030,959.14 ns |    41,663.399 ns |    47,979.632 ns |  7.8125 |      - |   57771 B |
| get_schema                     |        116.69 ns |         0.262 ns |         0.281 ns |  0.0548 |      - |     344 B |
| &#39;search_vertices (limit=2)&#39;    |  2,049,397.50 ns | 1,214,090.568 ns | 1,299,063.307 ns |  3.9063 |      - |   36043 B |
| &#39;search_vertices (all Person)&#39; |  1,917,625.66 ns |   529,848.784 ns |   610,174.648 ns | 27.3438 | 3.9063 |  194441 B |
| &#39;search_edges (all)&#39;           |  1,640,050.94 ns |    66,859.770 ns |    74,314.457 ns | 23.4375 |      - |  153329 B |
| &#39;get_neighbors (depth=1)&#39;      | 91,614,511.79 ns | 1,780,261.546 ns | 2,050,151.844 ns |       - |      - |  233251 B |
| export_graph                   | 71,350,147.14 ns | 1,004,406.177 ns | 1,074,703.357 ns |       - |      - |  472477 B |
