namespace WeatherService.Services;

using Google.Protobuf.WellKnownTypes;
using Grpc;
using Grpc.Core;

/*   gRPC SERVICE IMPLEMENTATION
N.B. Before this class can compile, you must: 
* Comment out the class below: GrpcService
* In Program.cs comment out: app.MapGrpcService<WeatherGrpcService>().EnableGrpcWeb();
* Add these NuGet packages to this project:
        <PackageReference Include = "Grpc.Core" Version="2.46.6" />
        <PackageReference Include = "Grpc.AspNetCore" Version="2.61.0" />
* Add the Protos/weather.proto file to this project
* Build once to generate the C# file that represents what is defined in: weather.proto
  This first build creates: 
    * ~\weather-via-grpc\weather-via-grpc\WeatherService\obj\Debug\net10.0\Protos\Weather.cs
            Auto-generated from weather.proto.
            Contains the message types (WeatherRequest, WeatherReply, enums, serialization logic).
            These are the DTOs used by both the gRPC server and client.

    * ~\weather-via-grpc\weather-via-grpc\WeatherService\obj\Debug\net10.0\Protos\WeatherGprc.cs
            Auto-generated from weather.proto.
            Contains the gRPC service plumbing: WeatherServiceBase (server) and WeatherServiceClient (client).
            Defines RPC method bindings and descriptors for GetWeather.
 */

public class GrpcService
{
    public class WeatherGrpcService : WeatherService.WeatherServiceBase
    {
        private readonly HttpClient _http;

        // Dont allow this to refactor to use primary constructor, potentially its an issue
        public WeatherGrpcService(HttpClient http)
        {
            _http = http;
        }

        // endpoint : /WeatherService/GetWeather
        public override async Task<WeatherResponse> GetWeather(WeatherRequest request, ServerCallContext context)
        {
            Console.WriteLine("1. gRPC WeatherRequest received");
            var weatherService = new OpenMeteoService(_http);
            var result = await weatherService.GetWeatherAsync(request.Latitude, request.Longitude);
            Console.WriteLine("2. gRPC WeatherRequest result received");
            if (result is null)
            {
                Console.WriteLine("3. gRPC WeatherRequest result unsuccessful");
                throw new RpcException(new Status(StatusCode.NotFound, "Weather data not found"));
            }
            Console.WriteLine("3. gRPC WeatherRequest result successful");

            return new WeatherResponse
            {
                Temperature2M = result.Current.Temperature_2m,
                WeatherCode = result.Current.Weather_Code,
                WeatherDescription = result.Current.Weather_Description
            };
        }

        // endpoint : /WeatherService/EchoWeather
        public override Task<WeatherResponse> EchoWeather(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new WeatherResponse
            {
                Temperature2M = 0,
                WeatherCode = 0,
                WeatherDescription = "OK"
            });
        }

        // endpoint : /WeatherService/EchoForecast
        // Large payload for speed test: returns `count` forecast points (same data as REST /forecast-echo)
        public override Task<ForecastResponse> EchoForecast(ForecastRequest request, ServerCallContext context)
        {
            return Task.FromResult(ForecastData.GetGrpc(request.Count));
        }

        // endpoint : /WeatherService/StreamForecast
        // Same forecast as EchoForecast, but sent as a server stream, one point per message.
        // Points are flushed to the network in batches of StreamFlushEvery, so the client gets its first
        // points straight away without paying for a network flush on every single point.
        public const int StreamFlushEvery = 100;
        private static readonly WriteOptions BufferedWrite = new(WriteFlags.BufferHint);

        public override async Task StreamForecast(ForecastRequest request, IServerStreamWriter<ForecastPoint> responseStream, ServerCallContext context)
        {
            var points = ForecastData.GetGrpc(request.Count).Points;
            for (int i = 0; i < points.Count; i++)
            {
                // null = normal write (flushes), BufferHint = hold back until the next flush
                responseStream.WriteOptions = (i + 1) % StreamFlushEvery == 0 ? null : BufferedWrite;
                await responseStream.WriteAsync(points[i]);
            }
        }
    }
}
