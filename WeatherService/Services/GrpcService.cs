namespace WeatherService.Services;

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
    public class WeatherGrpcService(HttpClient http) : WeatherService.WeatherServiceBase
    {
        private readonly HttpClient _http = http;

        public override async Task<WeatherResponse> GetWeather(WeatherRequest request, ServerCallContext context)
        {
            var weatherService = new OpenMeteoService(_http);
            var result = await weatherService.GetWeatherAsync(request.Latitude, request.Longitude);

            if (result is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Weather data not found"));
            }

            //string description = Enum.IsDefined(typeof(WeatherCode), result.Current.Weather_Code)
            //    ? Enum.GetName(typeof(WeatherCode), result.Current.Weather_Code)!
            //    : "Unknown";

            return new WeatherResponse
            {
                Temperature2M = result.Current.Temperature_2m,
                WeatherCode = result.Current.Weather_Code,
                WeatherDescription = result.Current.Weather_Description
            };
        }
    }
}
