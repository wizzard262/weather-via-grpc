using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Google.Protobuf;
using Grpc;
using Grpc.Core;
using WeatherApi.Dto;
using static Grpc.WeatherService;

namespace WeatherApi.Benchmark;

// Runs the gRPC vs REST benchmark. Both protocols fetch the same made-up forecast from the WeatherService,
// and both turn the reply into typed C# objects, so the only differences are the protocol and the data format.
public sealed class BenchmarkRunner(BenchmarkClients clients, BenchmarkSettings settings)
{
    public async Task<BenchmarkResults> RunAsync(CancellationToken ct)
    {
        var results = new BenchmarkResults();
        var total = Stopwatch.StartNew();
        try
        {
            results.WakeMs = await WakeServerAsync(ct);

            foreach (var size in settings.Sizes)
            {
                results.Sizes.Add(await RunSizeAsync(size, ct));
            }

            results.Parallel.AddRange(await RunParallelAsync(ct));
            results.Stream = await RunStreamAsync(ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            results.Error = "The WeatherService server doesn't have the gRPC benchmark endpoints yet. It needs its container image rebuilt and redeployed (see README2.md).";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            results.Error = "The WeatherService server doesn't have the REST /forecast-echo endpoint yet. It needs its container image rebuilt and redeployed (see README2.md).";
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            results.Error = $"{ex.GetType().Name}: {ex.Message}";
        }

        results.TotalMs = total.Elapsed.TotalMilliseconds;
        return results;
    }

    // A scaled-to-zero Container App is woken by an HTTP GET, but not by a gRPC call, so ping it first
    private async Task<double> WakeServerAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using var reply = await clients.Rest.GetAsync("ping", ct);
                reply.EnsureSuccessStatusCode();
                return sw.Elapsed.TotalMilliseconds;
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    // One call at a time, REST and gRPC taking turns, so both see the same network conditions
    private async Task<SizeResult> RunSizeAsync(int points, CancellationToken ct)
    {
        long jsonBytes = 0, protobufBytes = 0;

        for (int i = 0; i < settings.Warmup; i++)
        {
            await RestCallAsync(clients.Rest, points, ct);
            await GrpcCallAsync(clients.Grpc, points, ct);
        }

        var rest = new List<double>(settings.Iterations);
        var grpc = new List<double>(settings.Iterations);
        for (int i = 0; i < settings.Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            jsonBytes = await RestCallAsync(clients.Rest, points, ct);
            rest.Add(sw.Elapsed.TotalMilliseconds);

            sw.Restart();
            var reply = await GrpcCallAsync(clients.Grpc, points, ct);
            grpc.Add(sw.Elapsed.TotalMilliseconds);
            protobufBytes = reply.CalculateSize();
        }

        return new SizeResult(points, jsonBytes, protobufBytes, Stats.From(rest), Stats.From(grpc));
    }

    // Fire `parallel` calls at once. REST over HTTP/1.1 needs one connection per call in flight,
    // gRPC over HTTP/2 sends them all down one connection.
    private async Task<List<ParallelResult>> RunParallelAsync(CancellationToken ct)
    {
        using var rest = clients.NewRestClient();
        using var channel = clients.NewGrpcChannel();
        var grpc = new WeatherServiceClient(channel);

        // cold: brand new clients, so connections (and their TLS handshakes) are opened during the test
        var cold = new ParallelResult("Cold (new connections)",
            await TimeAllAsync(() => RestCallAsync(rest, settings.ParallelSize, ct)),
            await TimeAllAsync(() => GrpcCallAsync(grpc, settings.ParallelSize, ct)));

        // warm: same clients again, connections already open
        var warm = new ParallelResult("Warm (connections reused)",
            await TimeAllAsync(() => RestCallAsync(rest, settings.ParallelSize, ct)),
            await TimeAllAsync(() => GrpcCallAsync(grpc, settings.ParallelSize, ct)));

        return [cold, warm];
    }

    private async Task<double> TimeAllAsync(Func<Task> call)
    {
        var sw = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, settings.Parallel).Select(_ => call()));
        return sw.Elapsed.TotalMilliseconds;
    }

    // REST whole reply vs gRPC server stream: time until the first point can be used, and until all have arrived
    private async Task<StreamResult> RunStreamAsync(CancellationToken ct)
    {
        var restTotal = new List<double>();
        var grpcFirst = new List<double>();
        var grpcTotal = new List<double>();

        for (int run = 0; run <= BenchmarkSettings.StreamRuns; run++)
        {
            var sw = Stopwatch.StartNew();
            await RestCallAsync(clients.Rest, settings.StreamSize, ct);
            var restMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            double firstMs = 0;
            int received = 0;
            using (var call = clients.Grpc.StreamForecast(new ForecastRequest { Count = settings.StreamSize }, cancellationToken: ct))
            {
                await foreach (var _ in call.ResponseStream.ReadAllAsync(ct))
                {
                    if (received++ == 0) firstMs = sw.Elapsed.TotalMilliseconds;
                }
            }
            var grpcMs = sw.Elapsed.TotalMilliseconds;
            CheckCount("gRPC stream", received, settings.StreamSize);

            // run 0 is the warm-up
            if (run == 0) continue;
            restTotal.Add(restMs);
            grpcFirst.Add(firstMs);
            grpcTotal.Add(grpcMs);
        }

        return new StreamResult(Stats.From(restTotal), Stats.From(grpcFirst), Stats.From(grpcTotal));
    }

    // REST: download the JSON and parse it into typed objects (gRPC does the same with protobuf). Returns the JSON size.
    private static async Task<long> RestCallAsync(HttpClient http, int points, CancellationToken ct)
    {
        var json = await http.GetByteArrayAsync($"forecast-echo?count={points}", ct);
        var forecast = JsonSerializer.Deserialize<ForecastDto>(json);
        CheckCount("REST", forecast?.Points.Count ?? 0, points);
        return json.Length;
    }

    private static async Task<ForecastResponse> GrpcCallAsync(WeatherServiceClient grpc, int points, CancellationToken ct)
    {
        var reply = await grpc.EchoForecastAsync(new ForecastRequest { Count = points }, cancellationToken: ct);
        CheckCount("gRPC", reply.Points.Count, points);
        return reply;
    }

    private static void CheckCount(string protocol, int received, int expected)
    {
        if (received != expected)
        {
            throw new InvalidOperationException($"{protocol} returned {received} points, expected {expected}");
        }
    }
}
