namespace WeatherApi.Benchmark;

// The benchmark settings, read from the query string (i.e. /weatherbenchmark?sizes=1,100,1000,10000&iterations=20)
// and kept within limits so one run finishes well inside App Service's 230 second request timeout.
public sealed class BenchmarkSettings
{
    public const string DefaultSizes = "1,100,1000,10000";
    public const int DefaultIterations = 20;
    public const int DefaultWarmup = 3;
    public const int DefaultParallel = 100;
    public const int DefaultParallelSize = 1000;
    public const int DefaultStreamSize = 10000;

    public const int MaxPoints = 50_000;                 // same limit as the server
    public const int StreamRuns = 5;                     // timed streaming runs (after 1 warm-up)
    public const int StreamFlushEvery = 100;             // server flushes the stream every 100 points (see WeatherService GrpcService)
    private const long MaxSequentialPoints = 2_000_000;  // points per protocol across the whole size sweep
    private const long MaxParallelPoints = 200_000;      // points per protocol in one parallel round

    public int[] Sizes { get; private init; } = [];
    public int Iterations { get; private set; }
    public int Warmup { get; private init; }
    public int Parallel { get; private init; }
    public int ParallelSize { get; private set; }
    public int StreamSize { get; private init; }

    // notes about any values that were changed to stay within the limits
    public List<string> Adjustments { get; } = [];

    public static BenchmarkSettings Create(string? sizes, int iterations, int warmup, int parallel, int parallelSize, int streamSize)
    {
        var settings = new BenchmarkSettings
        {
            Sizes = ParseSizes(sizes),
            Iterations = Math.Clamp(iterations, 1, 200),
            Warmup = Math.Clamp(warmup, 0, 20),
            Parallel = Math.Clamp(parallel, 1, 200),
            ParallelSize = Math.Clamp(parallelSize, 1, 10_000),
            StreamSize = Math.Clamp(streamSize, 1, MaxPoints)
        };

        if (settings.Iterations != iterations) settings.Adjustments.Add($"iterations kept between 1 and 200 (asked for {iterations})");
        if (settings.Warmup != warmup) settings.Adjustments.Add($"warmup kept between 0 and 20 (asked for {warmup})");
        if (settings.Parallel != parallel) settings.Adjustments.Add($"parallel kept between 1 and 200 (asked for {parallel})");
        if (settings.ParallelSize != parallelSize) settings.Adjustments.Add($"parallelSize kept between 1 and 10,000 (asked for {parallelSize})");
        if (settings.StreamSize != streamSize) settings.Adjustments.Add($"streamSize kept between 1 and {MaxPoints:N0} (asked for {streamSize})");

        var sweepPoints = (long)settings.Sizes.Sum();
        if (sweepPoints * (settings.Iterations + settings.Warmup) > MaxSequentialPoints)
        {
            var fits = (int)Math.Max(1, MaxSequentialPoints / sweepPoints - settings.Warmup);
            settings.Adjustments.Add($"iterations cut from {settings.Iterations} to {fits} so the run finishes in time");
            settings.Iterations = fits;
        }

        if ((long)settings.Parallel * settings.ParallelSize > MaxParallelPoints)
        {
            var fits = (int)Math.Max(1, MaxParallelPoints / settings.Parallel);
            settings.Adjustments.Add($"parallelSize cut from {settings.ParallelSize:N0} to {fits:N0} so the run finishes in time");
            settings.ParallelSize = fits;
        }

        return settings;
    }

    private static int[] ParseSizes(string? sizes)
    {
        var parsed = (sizes ?? DefaultSizes)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? Math.Clamp(n, 1, MaxPoints) : 0)
            .Where(n => n > 0)
            .Distinct()
            .Order()
            .Take(6)
            .ToArray();

        return parsed.Length > 0 ? parsed : ParseSizes(DefaultSizes);
    }
}
