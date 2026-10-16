using Microsoft.AspNetCore.Mvc;
using WeatherApi.Benchmark;

namespace WeatherApi.Controllers
{
    // GET /weatherbenchmark (i.e. https://localhost:7034/weatherbenchmark?sizes=1,100,1000,10000&iterations=20)
    // Runs the gRPC vs REST benchmark against the WeatherService and returns an HTML results page.
    // See Benchmark/BenchmarkPage.cs (or the page itself) for what each setting does and why.
    [ApiController]
    [Route("[controller]")]
    public class WeatherBenchmarkController(BenchmarkClients clients) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Run(
            string? sizes = BenchmarkSettings.DefaultSizes,
            int iterations = BenchmarkSettings.DefaultIterations,
            int warmup = BenchmarkSettings.DefaultWarmup,
            int parallel = BenchmarkSettings.DefaultParallel,
            int parallelSize = BenchmarkSettings.DefaultParallelSize,
            int streamSize = BenchmarkSettings.DefaultStreamSize)
        {
            var settings = BenchmarkSettings.Create(sizes, iterations, warmup, parallel, parallelSize, streamSize);
            var results = await new BenchmarkRunner(clients, settings).RunAsync(HttpContext.RequestAborted);
            return Content(BenchmarkPage.Render(settings, results, clients), "text/html");
        }
    }
}
