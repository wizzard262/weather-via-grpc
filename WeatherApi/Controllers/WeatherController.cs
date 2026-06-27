using Google.Protobuf;
using Grpc;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using static Grpc.WeatherService;

namespace WeatherApi.Controllers;

[ApiController]
[Route("")]
public class WeatherController : ControllerBase
{
    // shows the index.html page describing the project and how to use it
    // also has a form that send a GET request with LAT & LNG to /weather endpoint below

    // GET / (i.e. https://localhost:7034/)
    [HttpGet("")]
    public IActionResult Index()
    {
        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/html");
    }

    // GET /weather (i.e. https://localhost:7034/weather?latitude=53.41&longitude=-2.15)
    [HttpGet("weather")]
    public async Task<IActionResult> GetWeather(double latitude, double longitude)
    {
        // Create channel
#if DEBUG
        using var channel = GrpcChannel.ForAddress("https://localhost:7110"); // the local path of the WeatherService ste:todo: make this configurable
#else
        using var channel = GrpcChannel.ForAddress("grpc-weather-e0efcad4b6afc6g2.ukwest-01.azurewebsites.net"); // the local path of the WeatherService ste:todo: make this configurable
#endif

        // Create client
        var client = new WeatherServiceClient(channel);

        // Build request
        var request = new WeatherRequest
        {
            Latitude = latitude,
            Longitude = longitude
        };

        // Call gRPC
        var reply = await client.GetWeatherAsync(request);

        // Convert the reply to a byte array (raw protobuf)
        byte[] bytes = reply.ToByteArray();
        // Convert the byte array to a hex string
        var hex = BitConverter.ToString(bytes).Replace("-", " ");

        return Ok(new
        {
            temperature_2m = reply.Temperature2M,
            weather_code = reply.WeatherCode,
            weather_description = reply.WeatherDescription,
            raw_protobuf = hex
        });
    }
}