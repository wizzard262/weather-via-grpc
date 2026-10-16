using System.Net;
using Grpc.Net.Client;
using static Grpc.WeatherService;

namespace WeatherApi.Benchmark;

// One shared REST HttpClient and one shared gRPC channel for the benchmark (registered as a singleton),
// so connections are opened once and reused between calls, as a real app would.
public sealed class BenchmarkClients
{
    // gRPC refuses replies over 4 MB by default; 50,000 forecast points is about 5 MB
    public const int MaxReceiveMessageSize = 64 * 1024 * 1024;

    public string RestAddress { get; }
    public string GrpcAddress { get; }
    public HttpClient Rest { get; }
    public WeatherServiceClient Grpc { get; }

    public BenchmarkClients(IConfiguration config)
    {
        RestAddress = config["HttpRestServerAddress"] ?? throw new InvalidOperationException("HttpRestServerAddress is not set");
        GrpcAddress = config["GrpcServerAddress"] ?? throw new InvalidOperationException("GrpcServerAddress is not set");
        Rest = NewRestClient();
        Grpc = new WeatherServiceClient(NewGrpcChannel());
    }

    // REST is pinned to HTTP/1.1, the classic REST setup (and .NET's HttpClient default)
    public HttpClient NewRestClient() => new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        BaseAddress = new Uri(RestAddress),
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        Timeout = TimeSpan.FromSeconds(100)
    };

    // gRPC always uses HTTP/2: one channel = one connection, with many calls multiplexed over it
    public GrpcChannel NewGrpcChannel() => GrpcChannel.ForAddress(GrpcAddress, new GrpcChannelOptions
    {
        MaxReceiveMessageSize = MaxReceiveMessageSize
    });
}
