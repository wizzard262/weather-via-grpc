using System.Globalization;
using System.Net;
using System.Text;

namespace WeatherApi.Benchmark;

// Builds the benchmark results page: what was run, every setting used and why, then the results
public static class BenchmarkPage
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Render(BenchmarkSettings s, BenchmarkResults r, BenchmarkClients clients)
    {
        var html = new StringBuilder();
        html.Append($$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>gRPC vs REST Benchmark</title>
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.2.3/dist/css/bootstrap.min.css" />
                <style>
                    body { background: #f5f5f5; }
                    .box { background: #fff; }
                    td, th { vertical-align: top; }
                    .num { text-align: right; white-space: nowrap; }
                </style>
            </head>
            <body>
            <div class="p-3" style="max-width: 1100px;">
                <h1>gRPC vs REST Benchmark</h1>
                <p>
                    The <b>WeatherApi - client</b> fetches the same made-up hourly weather forecast from the <b>WeatherService - server</b>
                    over REST (HTTP/1.1 + JSON) and over gRPC (HTTP/2 + Protobuf), and times each call.<br />
                    Both endpoints only return data held in memory <i>(no Open-Meteo call)</i>, and both clients turn the reply into typed C# objects,
                    so the only differences are the protocol and the data format.
                </p>
                <p class="text-muted">
                    REST: <code>{{Enc(clients.RestAddress)}}/forecast-echo?count=N</code><br />
                    gRPC: <code>{{Enc(clients.GrpcAddress)}}</code> &rarr; <code>WeatherService.EchoForecast</code> and <code>WeatherService.StreamForecast</code><br />
                    Run took {{Seconds(r.TotalMs)}}, including {{Ms(r.WakeMs)}} to wake the server.
                </p>
            """);

        if (r.Error is not null)
        {
            html.Append($"""<div class="alert alert-danger"><b>The benchmark stopped:</b> {Enc(r.Error)}</div>""");
        }

        foreach (var note in s.Adjustments)
        {
            html.Append($"""<div class="alert alert-warning">Setting changed: {Enc(note)}</div>""");
        }

        AppendSweep(html, r);
        AppendParallel(html, s, r);
        AppendStream(html, s, r);
        AppendWhy(html);
        AppendSettings(html, s);
        AppendTryLinks(html);

        html.Append("</div></body></html>");
        return html.ToString();
    }

    private static void AppendSweep(StringBuilder html, BenchmarkResults r)
    {
        html.Append("""
            <h2 class="mt-4">1. Reply size sweep <small class="text-muted">(one call at a time)</small></h2>
            <p>Each row fetches a forecast of that many points. Times are per call, from sending the request to having typed C# objects. The result compares the medians.</p>
            <div class="table-responsive"><table class="table table-sm table-bordered box">
                <tr><th>Points</th><th class="num">JSON size</th><th class="num">Protobuf size</th><th class="num">Size ratio</th>
                    <th class="num">REST median</th><th class="num">REST p95</th><th class="num">gRPC median</th><th class="num">gRPC p95</th><th>Result</th></tr>
            """);

        foreach (var row in r.Sizes)
        {
            var ratio = row.ProtobufBytes > 0 ? (double)row.JsonBytes / row.ProtobufBytes : 0;
            html.Append($"""
                <tr>
                    <td class="num">{row.Points:N0}</td>
                    <td class="num">{Bytes(row.JsonBytes)}</td>
                    <td class="num">{Bytes(row.ProtobufBytes)}</td>
                    <td class="num">{ratio.ToString("0.0", Invariant)}&times;</td>
                    <td class="num">{Ms(row.Rest.P50)}</td>
                    <td class="num">{Ms(row.Rest.P95)}</td>
                    <td class="num">{Ms(row.Grpc.P50)}</td>
                    <td class="num">{Ms(row.Grpc.P95)}</td>
                    {Verdict(row.Rest.P50, row.Grpc.P50)}
                </tr>
                """);
        }

        html.Append("</table></div>");
    }

    private static void AppendParallel(StringBuilder html, BenchmarkSettings s, BenchmarkResults r)
    {
        html.Append($"""
            <h2 class="mt-4">2. Parallel burst <small class="text-muted">({s.Parallel} calls at once, {s.ParallelSize:N0} points each)</small></h2>
            <p>
                All {s.Parallel} calls are started together and the time is until the last one finishes.<br />
                REST over HTTP/1.1 can only carry one call at a time per connection, so it opens up to {s.Parallel} connections, each with its own TLS handshake.
                gRPC over HTTP/2 sends every call down <b>one</b> connection.
                <i>Cold</i> uses brand new clients, <i>warm</i> runs again with the connections already open.
            </p>
            <div class="table-responsive"><table class="table table-sm table-bordered box">
                <tr><th>Round</th><th class="num">REST total</th><th class="num">gRPC total</th><th>Result</th></tr>
            """);

        foreach (var row in r.Parallel)
        {
            html.Append($"""
                <tr><td>{row.Round}</td><td class="num">{Ms(row.RestMs)}</td><td class="num">{Ms(row.GrpcMs)}</td>{Verdict(row.RestMs, row.GrpcMs)}</tr>
                """);
        }

        html.Append("</table></div>");
    }

    private static void AppendStream(StringBuilder html, BenchmarkSettings s, BenchmarkResults r)
    {
        html.Append($"""
            <h2 class="mt-4">3. Streaming <small class="text-muted">({s.StreamSize:N0} points, median of {BenchmarkSettings.StreamRuns} runs)</small></h2>
            <p>
                REST sends the forecast as one JSON document, so nothing can be used until all of it has arrived and been parsed.
                The gRPC <code>StreamForecast</code> call sends one point per message (the server flushes every {BenchmarkSettings.StreamFlushEvery} points),
                so the client can start using points almost straight away.
            </p>
            """);

        if (r.Stream is { } st)
        {
            html.Append($"""
                <div class="table-responsive"><table class="table table-sm table-bordered box">
                    <tr><th>Protocol</th><th class="num">First point usable</th><th class="num">All points received</th></tr>
                    <tr><td>REST (one JSON document)</td><td class="num">{Ms(st.RestTotal.P50)}</td><td class="num">{Ms(st.RestTotal.P50)}</td></tr>
                    <tr><td>gRPC (server stream)</td><td class="num table-success">{Ms(st.GrpcFirstPoint.P50)}</td><td class="num">{Ms(st.GrpcTotal.P50)}</td></tr>
                </table></div>
                <p class="text-muted">Streaming isn't about the total time (sending {s.StreamSize:N0} small messages has its own overhead); it's about how soon the first data arrives.</p>
                """);
        }
    }

    private static void AppendWhy(StringBuilder html)
    {
        html.Append("""
            <h2 class="mt-4">What to look for</h2>
            <ul>
                <li><b>Tiny replies: no real winner.</b> With 1 point the data takes microseconds either way, and a call is almost all network round trip.
                    REST can even edge ahead, as gRPC does a little more work per call (HTTP/2 frames, headers and trailers).
                    This is why the first version of this benchmark (a 3-field echo) showed no difference.</li>
                <li><b>Big replies favour gRPC.</b> JSON repeats every field name in every point and writes each number as text, so it is about 3&times; bigger
                    and slower to write and parse. Protobuf uses a 1-byte field number and 8 fixed bytes per number. On the small 0.25 vCPU server the extra JSON work shows up clearly.</li>
                <li><b>Cold bursts favour gRPC.</b> REST over HTTP/1.1 opens a new connection, with its own TLS handshake, for every call in flight; gRPC opens one.
                    Once those connections are open (the warm round), HTTP/1.1 can keep up, so HTTP/2's real win here is needing one connection instead of one per call.</li>
                <li><b>Streams arrive early.</b> The first gRPC streamed point is usable long before a REST client has the whole JSON document.</li>
                <li><b>Timings are noisy.</b> Results are over a real network on shared cloud machines, so run it a few times. The byte sizes never change.</li>
            </ul>
            """);
    }

    private static void AppendSettings(StringBuilder html, BenchmarkSettings s)
    {
        html.Append($"""
            <h2 class="mt-4">Settings used</h2>
            <p>These can be changed in the address bar, i.e. <code>/weatherbenchmark?sizes=1,100,1000,10000&amp;iterations=20</code>. Values are kept within limits so a run finishes inside App Service's 230 second request timeout.</p>
            <div class="table-responsive"><table class="table table-sm table-bordered box">
                <tr><th>Setting</th><th>This run</th><th>What it does</th><th>Why this value</th></tr>
                <tr><td><code>sizes</code></td><td>{string.Join(", ", s.Sizes.Select(n => n.ToString("N0", Invariant)))}</td>
                    <td>Forecast points per reply. One row in table 1 per size (up to 6 sizes, each 1 to {BenchmarkSettings.MaxPoints:N0}).</td>
                    <td>From 1 point (about 0.3 KB of JSON) to 10,000 points (about 2.7 MB of JSON), to show where gRPC starts to win.</td></tr>
                <tr><td><code>iterations</code></td><td>{s.Iterations}</td>
                    <td>Timed calls per protocol for each size (1 to 200).</td>
                    <td>Enough calls for a steady median (the middle call) and p95 (the time 95% of calls beat) without a long wait. The median is used, not the average, because the odd slow call on a shared cloud machine would skew an average.</td></tr>
                <tr><td><code>warmup</code></td><td>{s.Warmup}</td>
                    <td>Untimed calls per protocol before each size (0 to 20).</td>
                    <td>Opens the connections, does the TLS handshakes, compiles the serializers and fills the server's data cache, so only steady-state calls are timed.</td></tr>
                <tr><td><code>parallel</code></td><td>{s.Parallel}</td>
                    <td>Calls started at once in table 2 (1 to 200).</td>
                    <td>100 is enough to need many HTTP/1.1 connections, and is the usual HTTP/2 limit of streams on one connection.</td></tr>
                <tr><td><code>parallelSize</code></td><td>{s.ParallelSize:N0}</td>
                    <td>Points per reply in table 2 (1 to 10,000).</td>
                    <td>A medium reply (about 0.3 MB of JSON), so 100 at once doesn't overload the small server.</td></tr>
                <tr><td><code>streamSize</code></td><td>{s.StreamSize:N0}</td>
                    <td>Points in table 3 (1 to {BenchmarkSettings.MaxPoints:N0}).</td>
                    <td>Big enough that waiting for the whole REST reply is noticeable.</td></tr>
            </table></div>

            <h3 class="mt-3 h5">Fixed setup</h3>
            <div class="table-responsive"><table class="table table-sm table-bordered box">
                <tr><th>Item</th><th>Set to</th><th>Why</th></tr>
                <tr><td>Data</td>
                    <td>12 fields per point: a time (<code>int64</code>), 10 decimal numbers (<code>double</code>, rounded to 2 places, like sensor readings) and a weather code (<code>int32</code>). Field names match Open-Meteo's hourly forecast.</td>
                    <td>Real weather data is mostly numbers with long field names, which is where JSON is weakest. Made-up but seeded, so every run gets the same data.</td></tr>
                <tr><td>Server data</td>
                    <td>Built once per size and kept in memory. REST and gRPC return the same values.</td>
                    <td>Times only cover writing, sending and parsing the data, not making it.</td></tr>
                <tr><td>REST client</td>
                    <td>HTTP/1.1, JSON with <code>System.Text.Json</code>. Downloads the whole body and parses it into typed C# objects.</td>
                    <td>Like-for-like with gRPC, which always parses into typed objects. (The first version only read the text and skipped parsing.)</td></tr>
                <tr><td>gRPC client</td>
                    <td>HTTP/2, Protobuf. One channel (one connection). Largest reply allowed: 64 MB.</td>
                    <td>The default limit is 4 MB, which 50,000 points goes over.</td></tr>
                <tr><td>Connections</td>
                    <td>One shared <code>HttpClient</code> and one shared <code>GrpcChannel</code> for tables 1 and 3. Table 2 makes new ones.</td>
                    <td>Reusing connections is how real apps work; a new client shows the cost of opening connections.</td></tr>
                <tr><td>Order</td>
                    <td>REST and gRPC take turns, call by call.</td>
                    <td>Both see the same network conditions, rather than one running in a quiet minute and the other in a busy one.</td></tr>
                <tr><td>Compression</td>
                    <td>Off for both.</td>
                    <td>Gzip would shrink JSON a lot (and Protobuf a little) but costs CPU at both ends. Left off to compare the formats directly.</td></tr>
                <tr><td>Network path</td>
                    <td>Both use HTTPS to the same Container App address. Azure's ingress ends TLS and passes both to the container on port 5000 over HTTP/2.</td>
                    <td>Same route for both. The HTTP/1.1 vs HTTP/2 difference is on the client to Azure leg.</td></tr>
                <tr><td>Server size</td>
                    <td>Azure Container App: 0.25 vCPU, 0.5 GiB, scales to zero when idle.</td>
                    <td>The smallest size. On a small CPU the cost of writing JSON shows clearly.</td></tr>
                <tr><td>Stream flushing</td>
                    <td>Server sends streamed points in batches of {BenchmarkSettings.StreamFlushEvery}.</td>
                    <td>Flushing every single point costs a network write each; batching keeps the first points fast without that cost.</td></tr>
                <tr><td>Wake-up</td>
                    <td>An HTTP GET to <code>/ping</code> before anything else.</td>
                    <td>A sleeping (scaled to zero) container is woken by HTTP but not by gRPC, and its 1-3 s start-up would spoil the first timings.</td></tr>
                <tr><td>Sizes shown</td>
                    <td>JSON: response body bytes. Protobuf: message bytes.</td>
                    <td>Data only, without HTTP headers or gRPC's 5-byte message header.</td></tr>
            </table></div>
            """);
    }

    private static void AppendTryLinks(StringBuilder html)
    {
        html.Append("""
            <h2 class="mt-4">Try other settings</h2>
            <ul>
                <li><a href="/weatherbenchmark">Default run</a></li>
                <li><a href="/weatherbenchmark?sizes=1,10,50&amp;iterations=50">Tiny replies only</a> <i>(expect a tie: this is what the first version measured)</i></li>
                <li><a href="/weatherbenchmark?sizes=1000,10000,50000&amp;iterations=10">Big replies</a> <i>(up to 50,000 points, about 13 MB of JSON)</i></li>
                <li><a href="/weatherbenchmark?parallel=200&amp;parallelSize=500">Bigger burst</a> <i>(200 calls at once)</i></li>
                <li><a href="/weatherbenchmark?streamSize=50000">Long stream</a> <i>(50,000 streamed points)</i></li>
            </ul>
            <p><a href="/">Back to the client home page</a></p>
            """);
    }

    // a table cell saying which protocol won, and by how much
    private static string Verdict(double restMs, double grpcMs)
    {
        if (restMs <= 0 || grpcMs <= 0) return "<td></td>";
        var ratio = restMs / grpcMs;
        if (ratio >= 1.1) return $"""<td class="table-success">gRPC {ratio.ToString("0.0", Invariant)}&times; faster</td>""";
        if (ratio <= 1 / 1.1) return $"""<td class="table-warning">REST {(1 / ratio).ToString("0.0", Invariant)}&times; faster</td>""";
        return "<td>About the same</td>";
    }

    private static string Ms(double ms) => ms.ToString(ms < 10 ? "0.00" : "0.0", Invariant) + " ms";

    private static string Seconds(double ms) => (ms / 1000).ToString("0.0", Invariant) + " s";

    private static string Bytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => (bytes / 1024d / 1024).ToString("0.00", Invariant) + " MB",
        >= 1024 => (bytes / 1024d).ToString("0.0", Invariant) + " KB",
        _ => bytes + " B"
    };

    private static string Enc(string text) => WebUtility.HtmlEncode(text);
}
