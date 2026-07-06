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
        private readonly string _restBaseUrl = config["HttpRestServerAddress"];  // REST server (https)
        private readonly string _grpcBaseUrl = config["GrpcServerAddress"];      // gRPC server (http)

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
            // gRPC PARALLEL THROUGHPUT BENCHMARK
            // -------------------------
            var channelParallel = GrpcChannel.ForAddress(_grpcBaseUrl);
            var grpcParallel = new WeatherService.WeatherServiceClient(channelParallel);

            var swGrpcParallel = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, 100)
                .Select(_ => grpc.EchoWeatherAsync(new Empty()).ResponseAsync)
                .ToArray();

            await Task.WhenAll(tasks);

            swGrpcParallel.Stop();
            var grpcParallelMs = swGrpcParallel.ElapsedMilliseconds;

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
                            border-collapse: collapse;f
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
                    </style>
                </head>
                <body>
                    <h1>gRPC vs REST Benchmark</h1>
                    <ul>
                        <li>WeatherClient calls WeatherServer HTTP REST endpoint: \weather-echo 
                        <li>WeatherClient calls WeatherServer gRPC endpoint: EchoWeather
                        <li>Both endpoits echo back Weather JSON object. <i>(excludes time spent calling the Open-Meteo external API)</i>
                    </ul>
                    </br>
                    </br>
                    <table>
                        <tr>
                            <th>Protocol</th>
                            <th>Execution Count</th>
                            <th>Execution Mode</th>
                            <th>Total Time (ms)</th>
                        </tr>
                        <tr>
                            <td>REST (HTTP/1.1 + JSON)</td>                            
                            <td>100</td>
                            <td>Sequential loop</td>
                            <td><b>{restMs} ms</b></td>
                        </tr>
                        <tr>
                            <td>gRPC (HTTP/2 + Protobuf)</td>
                            <td>100</td>
                            <td>Sequential loop</td>
                            <td><b>{grpcMs} ms</b></td>
                        </tr>
                        <tr>
                            <td>gRPC (HTTP/2 + Protobuf)</td>
                            <td>100</td>
                            <td>Parallel</td>
                            <td><b>{grpcParallelMs} ms</b></td>
                        </tr>
                        <tr>
                            <td>gRPC (HTTP/2 + Protobuf)</td>
                            <td>100</td>
                            <td>Streaming</td>
                            <td><b>todo</b></td>
                        </tr>
                    </table>
                    <br/>
                    Sequential requests show little difference between REST and gRPC.<br/>
                    <i>(It's measuring network latency. Workload and DTO are to small to demonstrate gRPC's advantages.)</i></br>
                    Parallel requests show gRPC is faster.
                    <br/>
                    <br/>
                    gRPC protocol is faster because:
                    <ul>
                        <li>Protobuf binary is smaller than JSON</li>
                        <li>Protobuf is faster to serialize than JSON</li>
                        <li>HTTP/2 multiplexes (many parallel requests)</li>
                        <li>No JSON parsing</li>
                        <li>No HTTP/1.1 head-of-line blocking</li>
                    </ul>
                </body>
                </html>";

            return Content(html, "text/html");
        }
    }
}
