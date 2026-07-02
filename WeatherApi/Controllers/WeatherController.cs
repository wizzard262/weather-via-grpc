using Google.Protobuf;
using Grpc;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using static Grpc.WeatherService;

namespace WeatherApi.Controllers;

[ApiController]
[Route("")]
public class WeatherController(IConfiguration config) : ControllerBase
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
        // Note that we must specify the port number because the WeatherService is running on that port in Azure Container Apps.
        // If we don't specify the port number, the request will use the HTTPS defualt of 443, and fail with a 404 error unless ACA ingress is mapped to 443.
        // The port to be used for gRPC is 5000 by convention in .NET/ASP.NET Core (Most languages use 50051 by convention).
        //  e.g. "https://grpc-weather-server-xxx.uksouth-01.azurewebsites.net:50000" or "https://localhost:50000"
        var url = config["GrpcServerAddress"];

        // Create client
        var channel = GrpcChannel.ForAddress(url);
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