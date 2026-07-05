```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26200.8655)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 9.0.311
  [Host]     : .NET 9.0.17 (9.0.1726.26416), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.17 (9.0.1726.26416), X64 RyuJIT AVX2


```
| Method           | Mean     | Error   | StdDev  | Allocated |
|----------------- |---------:|--------:|--------:|----------:|
| ProjectTrainList | 224.6 ms | 4.47 ms | 6.69 ms | 507.75 KB |
