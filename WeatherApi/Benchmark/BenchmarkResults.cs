namespace WeatherApi.Benchmark;

// Timings in milliseconds for one set of calls
public sealed record Stats(double Average, double P50, double P95, double Min, double Max)
{
    public static Stats From(IReadOnlyCollection<double> ms)
    {
        var sorted = ms.Order().ToArray();
        // nearest-rank percentile
        double Percentile(double p) => sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)];
        return new Stats(sorted.Average(), Percentile(0.50), Percentile(0.95), sorted[0], sorted[^1]);
    }
}

// One row of the size sweep: the same forecast fetched over REST and gRPC, one call at a time
public sealed record SizeResult(int Points, long JsonBytes, long ProtobufBytes, Stats Rest, Stats Grpc);

// Many calls fired at once. Cold = brand new clients (connections opened during the test), warm = the same clients again
public sealed record ParallelResult(string Round, double RestMs, double GrpcMs);

// REST must download and parse the whole reply before using any of it; a gRPC stream hands over points as they arrive
public sealed record StreamResult(Stats RestTotal, Stats GrpcFirstPoint, Stats GrpcTotal);

public sealed class BenchmarkResults
{
    public double WakeMs { get; set; }
    public double TotalMs { get; set; }
    public List<SizeResult> Sizes { get; } = [];
    public List<ParallelResult> Parallel { get; } = [];
    public StreamResult? Stream { get; set; }
    public string? Error { get; set; }
}
