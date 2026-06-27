using Microsoft.AspNetCore.Mvc;

namespace WeatherService.Controllers;

/* This controller implements RESTful API an endpoint to get weather data using the weatherService.GetWeatherAsync(lat,lng) method. 
   It is only used to test the underlying service, which with e used by the gRPC endpoint. */

[ApiController]
[Route("")]
public class WeatherController(HttpClient http) : ControllerBase
{
    private readonly HttpClient _http = http;

    // GET / (i.e. localhost:7110/ )
    [HttpGet("")]
    public IActionResult Index()
    {
        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/html");
    }


    // GET /weather?latitude=53.41&longitude=-2.15
    [HttpGet("weather")]
    public async Task<IActionResult> GetWeather([FromQuery] double latitude, [FromQuery] double longitude)
    {
        var weatherService = new Services.OpenMeteoService(_http);
        var result = await weatherService.GetWeatherAsync(latitude, longitude);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result.Current);
    }
}