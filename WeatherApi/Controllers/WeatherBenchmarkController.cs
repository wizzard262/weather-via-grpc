using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Grpc.Net.Client;
using Grpc;
using Google.Protobuf.WellKnownTypes;

namespace WeatherApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherBenchmarkController(IConfiguration config) : ControllerBase
    {
        private readonly string _restBaseUrl = config["HttpRestServerAddress"];  // REST server (https) //ste:todo: move to 
        private readonly string _grpcBaseUrl = config["GrpcServerAddress"];    // gRPC server (http)

        [HttpGet]
        public async Task<IActionResult> Run()
        {
            // -------------------------
            // REST BENCHMARK
            // -------------------------
            var http = new HttpClient();
            var swRest = Stopwatch.StartNew();

            for (int i = 0; i < 100; i++)
            {
                var result = await http.GetAsync($"{_restBaseUrl}/weather-echo?latitude=53.0&longitude=-2.0");
                await result.Content.ReadAsStringAsync();
            }

            swRest.Stop();
            var restMs = swRest.ElapsedMilliseconds;

            // -------------------------
            // gRPC BENCHMARK
            // -------------------------
            var channel = GrpcChannel.ForAddress(_grpcBaseUrl);
            var grpc = new WeatherService.WeatherServiceClient(channel);

            var swGrpc = Stopwatch.StartNew();

            for (int i = 0; i < 100; i++)
            {
                await grpc.EchoWeatherAsync(new Empty());
            }

            swGrpc.Stop();
            var grpcMs = swGrpc.ElapsedMilliseconds;

            // -------------------------
            // HTML OUTPUT
            // -------------------------
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Benchmark Results</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 40px;
            background: #f5f5f5;
        }}
        h1 {{
            margin-bottom: 20px;
        }}
        table {{
            border-collapse: collapse;
            width: 400px;
            background: white;
        }}
        th, td {{
            border: 1px solid #ccc;
            padding: 10px;
            text-align: left;
        }}
        th {{
            background: #eee;
        }}
        .fast {{
            color: green;
            font-weight: bold;
        }}
        .slow {{
            color: red;
            font-weight: bold;
        }}
    </style>
</head>
<body>
    <h1>gRPC vs REST Benchmark</h1>
    This hits empty endpoints that echo back the Weather JSON object (so as to remove time calling the open-meteo API)<br/>
    Each is hit 100 times.</br>
    <table>
        <tr>
            <th>Protocol</th>
            <th>Total Time (ms)</th>
        </tr>
        <tr>
            <td>REST (HTTP/1.1 + JSON)</td>
            <td class=""slow"">{restMs} ms</td>
        </tr>
        <tr>
            <td>gRPC (HTTP/2 + Protobuf)</td>
            <td class=""fast"">{grpcMs} ms</td>
        </tr>
    </table>
    BUT Both locally in Visual Studio and on Azure the differnce between HTTP REST and gRPC is tiny and either can be faster.<br/>
    It seems were just measuring network latency, the workload and DTO are so small the gRPC overhead is not significant.<BR/>
    gRPC is faster because:
    <ul>
        <li>Protobuf is smaller</li>
        <li>Protobuf is faster to serialize</li>
        <li>HTTP/2 multiplexes</li>
        <li>No JSON parsing</li>
        <li>No HTTP/1.1 head‑of‑line blocking</li>
    </ul>
    
    To see a bigger difference, we need to benchmark a more complex workload with larger payloads and more CPU work.<br\>
    For example, if the server is doing some CPU intensive work (e.g. image processing) or the payload is large (e.g. 1MB JSON vs 100KB Protobuf),<br\>
    then gRPC will be significantly faster than REST.<br/>
    STE:TODO: Mimic a more complex workload and benchmark it. Adding parallelism would be most significant.
    <ul>
        <li>JSON serialization cost</li>
        <li>Protobuf serialization cost</li>
        <li>HTTP/1.1 head‑of‑line blocking</li>
        <li>HTTP/2 multiplexing</li>
        <li>CPU work</li>
        <li>payload size</li>
        <li>parallelism (many requests over one connection)</li>
    </ul>
</body>
</html>";

            return Content(html, "text/html");
        }
    }
}
