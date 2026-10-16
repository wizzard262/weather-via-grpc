using System.Globalization;
using System.Net;
using System.Text;

namespace WeatherApi.Benchmark;

// Builds the benchmark results page: what was run, the results, then every setting used and why.
// Styled by wwwroot/styles.css, shared with the home page.
public static class BenchmarkPage
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Render(BenchmarkSettings s, BenchmarkResults r, BenchmarkClients clients)
    {
        var html = new StringBuilder();
        html.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>gRPC vs REST Benchmark</title>
                <meta name="description" content="Times the same weather forecast fetched over REST (HTTP/1.1 + JSON) and gRPC (HTTP/2 + Protobuf).">
                <link rel="icon" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E%3Crect width='32' height='32' rx='7' fill='%230b7f78'/%3E%3Ctext x='16' y='21' font-family='Arial' font-weight='700' font-size='11' fill='white' text-anchor='middle'%3EgRPC%3C/text%3E%3C/svg%3E">
                <link rel="stylesheet" href="/styles.css">
            </head>
            <body>
                <header class="hero">
                    <div class="wrap">
                        <p class="eyebrow"><a href="/">gRPC Weather Client</a> &middot; Benchmark</p>
                        <h1>gRPC vs REST Benchmark</h1>
                        <p class="lead">The client fetches the same made-up hourly weather forecast from the server over REST (HTTP/1.1 + JSON)
                            and over gRPC (HTTP/2 + Protobuf), and times each call. Both parse the reply into typed C# objects,
                            so the only differences are the protocol and the data format.</p>
                    </div>
                </header>
                <main class="wrap grid">
            """);

        AppendRun(html, r, clients, s);
        AppendWhy(html);
        AppendSweep(html, r);
        AppendParallel(html, s, r);
        AppendStream(html, s, r);
        AppendSettings(html, s);
        AppendTryLinks(html);

        html.Append("""
                </main>
                <footer class="wrap footer">
                    <p><a href="/">Back to the gRPC Weather Client</a> &middot; Code on <a href="https://github.com/wizzard262/weather-via-grpc" target="_blank" rel="noopener">GitHub</a></p>
                </footer>
            </body>
            </html>
            """);
        return html.ToString();
    }

    private static void AppendRun(StringBuilder html, BenchmarkResults r, BenchmarkClients clients, BenchmarkSettings s)
    {
        html.Append($"""
            <section class="card" aria-labelledby="run-title">
                <h2 id="run-title">This run</h2>
            """);

        if (r.Error is not null)
        {
            html.Append($"""<div class="notice notice-bad"><b>The benchmark stopped:</b> {Enc(r.Error)}</div>""");
        }

        foreach (var note in s.Adjustments)
        {
            html.Append($"""<div class="notice notice-warn"><b>Setting changed:</b> {Enc(note)}</div>""");
        }

        html.Append($"""
                <dl class="facts">
                    <div><dt>REST endpoint</dt><dd><code>{Enc(clients.RestAddress)}/forecast-echo?count=N</code></dd></div>
                    <div><dt>gRPC endpoints</dt><dd><code>{Enc(clients.GrpcAddress)}</code><br><code>WeatherService.EchoForecast</code> and <code>WeatherService.StreamForecast</code></dd></div>
                    <div><dt>Time taken</dt><dd>{Seconds(r.TotalMs)}, including {Ms(r.WakeMs)} to wake the server</dd></div>
                </dl>
                <p class="muted small">Both endpoints return data held in memory (no Open-Meteo call). Run it a few times: timings vary, byte sizes never do.</p>
            </section>
            """);
    }

    private static void AppendWhy(StringBuilder html)
    {
        html.Append("""
            <section class="card" aria-labelledby="why-title">
                <h2 id="why-title">What to look for</h2>
                <ul>
                    <li><b>Tiny replies: no real winner.</b> With 1 point the data takes microseconds either way, and a call is almost all network round trip.
                        REST can even edge ahead, as gRPC does a little more work per call (HTTP/2 frames, headers and trailers).
                        This is why the first version of this benchmark (a 3-field echo) showed no difference.</li>
                    <li><b>Big replies favour gRPC.</b> JSON repeats every field name in every point and writes each number as text, so it is about 3&times; bigger
                        and slower to write and parse. Protobuf uses a 1-byte field number and 8 fixed bytes per number.</li>
                    <li><b>Bursts favour gRPC.</b> REST over HTTP/1.1 opens a connection, with its own TLS handshake, for every call in flight; gRPC sends them all down one.
                        On the small server, writing many JSON replies at once also takes far more CPU than the same replies in Protobuf.</li>
                    <li><b>Streams arrive early.</b> The first streamed gRPC point is usable long before a REST client has the whole JSON document.</li>
                </ul>
            </section>
            """);
    }

    private static void AppendSweep(StringBuilder html, BenchmarkResults r)
    {
        html.Append("""
            <section class="card card-wide" aria-labelledby="sweep-title">
                <h2 id="sweep-title">1. Reply size sweep <small>one call at a time</small></h2>
                <p class="muted">Each row fetches a forecast of that many points. A <b>point</b> is one hour of forecast with 12 fields, so 10,000 points is about 14 months of hours.
                    Times are per call, from sending the request to having typed C# objects. The result compares the medians.</p>
                <details class="small">
                    <summary>See one point</summary>
                    <pre class="result">{
              "unix_time": 1767225600,
              "temperature_2m": 20.84,
              "apparent_temperature": 18.82,
              "relative_humidity_2m": 33.28,
              "dew_point_2m": 10.99,
              "precipitation": 1.31,
              "pressure_msl": 984.53,
              "cloud_cover": 80.21,
              "wind_speed_10m": 26.73,
              "wind_direction_10m": 80.99,
              "wind_gusts_10m": 1.01,
              "weather_code": 71
            }</pre>
                    <p class="muted">About 290 bytes as JSON: every field name is repeated in every point, and numbers are written as text.
                        The same point in Protobuf is about 98 bytes: each field name becomes a 1-byte field number and each decimal is 8 fixed bytes.</p>
                </details>
                <div class="table-wrap"><table>
                    <tr><th class="num">Points</th><th class="num">JSON size</th><th class="num">Protobuf size</th><th class="num">Size ratio</th>
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

        html.Append("""
                </table></div>
            </section>
            """);
    }

    private static void AppendParallel(StringBuilder html, BenchmarkSettings s, BenchmarkResults r)
    {
        html.Append($"""
            <section class="card" aria-labelledby="parallel-title">
                <h2 id="parallel-title">2. Parallel burst <small>{s.Parallel} calls at once, {s.ParallelSize:N0} points each</small></h2>
                <p class="muted">All the calls start together; the time is until the last one finishes.
                    REST over HTTP/1.1 carries one call at a time per connection, so it opens up to {s.Parallel} connections.
                    gRPC over HTTP/2 sends every call down <b>one</b> connection.
                    <i>Cold</i> uses brand new clients, <i>warm</i> runs again with the connections already open.</p>
                <div class="table-wrap"><table>
                    <tr><th>Round</th><th class="num">REST total</th><th class="num">gRPC total</th><th>Result</th></tr>
            """);

        foreach (var row in r.Parallel)
        {
            html.Append($"""
                    <tr><td>{row.Round}</td><td class="num">{Ms(row.RestMs)}</td><td class="num">{Ms(row.GrpcMs)}</td>{Verdict(row.RestMs, row.GrpcMs)}</tr>
                """);
        }

        html.Append("""
                </table></div>
            </section>
            """);
    }

    private static void AppendStream(StringBuilder html, BenchmarkSettings s, BenchmarkResults r)
    {
        html.Append($"""
            <section class="card" aria-labelledby="stream-title">
                <h2 id="stream-title">3. Streaming <small>{s.StreamSize:N0} points, median of {BenchmarkSettings.StreamRuns} runs</small></h2>
                <p class="muted">REST sends the forecast as one JSON document, so nothing can be used until all of it has arrived and been parsed.
                    The gRPC <code>StreamForecast</code> call sends one point per message (the server flushes every {BenchmarkSettings.StreamFlushEvery} points),
                    so the client can start using points almost straight away.</p>
            """);

        if (r.Stream is { } st)
        {
            html.Append($"""
                <div class="table-wrap"><table>
                    <tr><th>Protocol</th><th class="num">First point usable</th><th class="num">All points received</th></tr>
                    <tr><td>REST (one JSON document)</td><td class="num">{Ms(st.RestTotal.P50)}</td><td class="num">{Ms(st.RestTotal.P50)}</td></tr>
                    <tr><td>gRPC (server stream)</td><td class="num win">{Ms(st.GrpcFirstPoint.P50)}</td><td class="num">{Ms(st.GrpcTotal.P50)}</td></tr>
                </table></div>
                <p class="muted small">Streaming isn't about the total time (sending {s.StreamSize:N0} small messages has its own overhead); it's about how soon the first data arrives.</p>
                """);
        }

        html.Append("</section>");
    }

    private static void AppendSettings(StringBuilder html, BenchmarkSettings s)
    {
        html.Append($"""
            <section class="card card-wide" aria-labelledby="settings-title">
                <h2 id="settings-title">Settings used</h2>
                <p class="muted">Change these in the address bar, i.e. <code>/weatherbenchmark?sizes=1,100,1000,10000&amp;iterations=20</code>.
                    Values are kept within limits so a run finishes inside App Service's 230 second request timeout.</p>
                <div class="table-wrap"><table class="stack settings">
                    <tr><th>Setting</th><th>This run</th><th>What it does</th><th>Why this value</th></tr>
                    <tr><td class="stack-title"><code>sizes</code></td><td data-label="This run">{string.Join(", ", s.Sizes.Select(n => n.ToString("N0", Invariant)))}</td>
                        <td data-label="What it does">Forecast points per reply. One row in table 1 per size (up to 6 sizes, each 1 to {BenchmarkSettings.MaxPoints:N0}).</td>
                        <td data-label="Why this value">From 1 point (about 0.3 KB of JSON) to 10,000 points (about 2.7 MB of JSON), to show where gRPC starts to win.</td></tr>
                    <tr><td class="stack-title"><code>iterations</code></td><td data-label="This run">{s.Iterations}</td>
                        <td data-label="What it does">Timed calls per protocol for each size (1 to 200).</td>
                        <td data-label="Why this value">Enough calls for a steady median (the middle call) and p95 (the time 95% of calls beat) without a long wait.
                            The median is used, not the average, because the odd slow call on a shared cloud machine would skew an average.</td></tr>
                    <tr><td class="stack-title"><code>warmup</code></td><td data-label="This run">{s.Warmup}</td>
                        <td data-label="What it does">Untimed calls per protocol before each size (0 to 20).</td>
                        <td data-label="Why this value">Opens the connections, does the TLS handshakes, compiles the serializers and fills the server's data cache, so only steady-state calls are timed.</td></tr>
                    <tr><td class="stack-title"><code>parallel</code></td><td data-label="This run">{s.Parallel}</td>
                        <td data-label="What it does">Calls started at once in table 2 (1 to 200).</td>
                        <td data-label="Why this value">100 is enough to need many HTTP/1.1 connections, and is the usual HTTP/2 limit of streams on one connection.</td></tr>
                    <tr><td class="stack-title"><code>parallelSize</code></td><td data-label="This run">{s.ParallelSize:N0}</td>
                        <td data-label="What it does">Points per reply in table 2 (1 to 10,000).</td>
                        <td data-label="Why this value">A medium reply (about 0.3 MB of JSON), so 100 at once doesn't overload the small server.</td></tr>
                    <tr><td class="stack-title"><code>streamSize</code></td><td data-label="This run">{s.StreamSize:N0}</td>
                        <td data-label="What it does">Points in table 3 (1 to {BenchmarkSettings.MaxPoints:N0}).</td>
                        <td data-label="Why this value">Big enough that waiting for the whole REST reply is noticeable.</td></tr>
                </table></div>

                <h3>Fixed setup</h3>
                <div class="table-wrap"><table class="stack setup">
                    <tr><th>Item</th><th>Set to</th><th>Why</th></tr>
                    <tr><td class="stack-title">Data</td>
                        <td data-label="Set to">12 fields per point: a time (<code>int64</code>), 10 decimal numbers (<code>double</code>, rounded to 2 places, like sensor readings) and a weather code (<code>int32</code>). Field names match Open-Meteo's hourly forecast.</td>
                        <td data-label="Why">Real weather data is mostly numbers with long field names, which is where JSON is weakest. Made-up but seeded, so every run gets the same data.</td></tr>
                    <tr><td class="stack-title">Server data</td>
                        <td data-label="Set to">Built once per size and kept in memory. REST and gRPC return the same values.</td>
                        <td data-label="Why">Times only cover writing, sending and parsing the data, not making it.</td></tr>
                    <tr><td class="stack-title">REST client</td>
                        <td data-label="Set to">HTTP/1.1, JSON with <code>System.Text.Json</code>. Downloads the whole body and parses it into typed C# objects.</td>
                        <td data-label="Why">Like-for-like with gRPC, which always parses into typed objects. (The first version only read the text and skipped parsing.)</td></tr>
                    <tr><td class="stack-title">gRPC client</td>
                        <td data-label="Set to">HTTP/2, Protobuf. One channel (one connection). Largest reply allowed: 64 MB.</td>
                        <td data-label="Why">The default limit is 4 MB, which 50,000 points goes over.</td></tr>
                    <tr><td class="stack-title">Connections</td>
                        <td data-label="Set to">One shared <code>HttpClient</code> and one shared <code>GrpcChannel</code> for tables 1 and 3. Table 2 makes new ones.</td>
                        <td data-label="Why">Reusing connections is how real apps work; a new client shows the cost of opening connections.</td></tr>
                    <tr><td class="stack-title">Order</td>
                        <td data-label="Set to">REST and gRPC take turns, call by call.</td>
                        <td data-label="Why">Both see the same network conditions, rather than one running in a quiet minute and the other in a busy one.</td></tr>
                    <tr><td class="stack-title">Compression</td>
                        <td data-label="Set to">Off for both.</td>
                        <td data-label="Why">Gzip would shrink JSON a lot (and Protobuf a little) but costs CPU at both ends. Left off to compare the formats directly.</td></tr>
                    <tr><td class="stack-title">Network path</td>
                        <td data-label="Set to">Both use HTTPS to the same Container App address. Azure's ingress ends TLS and passes both to the container on port 5000 over HTTP/2.</td>
                        <td data-label="Why">Same route for both. The HTTP/1.1 vs HTTP/2 difference is on the client to Azure leg.</td></tr>
                    <tr><td class="stack-title">Server size</td>
                        <td data-label="Set to">Azure Container App: 0.25 vCPU, 0.5 GiB, scales to zero when idle.</td>
                        <td data-label="Why">The smallest size. On a small CPU the cost of writing JSON shows clearly.</td></tr>
                    <tr><td class="stack-title">Stream flushing</td>
                        <td data-label="Set to">Server sends streamed points in batches of {BenchmarkSettings.StreamFlushEvery}.</td>
                        <td data-label="Why">Flushing every single point costs a network write each; batching keeps the first points fast without that cost.</td></tr>
                    <tr><td class="stack-title">Wake-up</td>
                        <td data-label="Set to">An HTTP GET to <code>/ping</code> before anything else.</td>
                        <td data-label="Why">A sleeping (scaled to zero) container is woken by HTTP but not by gRPC, and its 1-3 s start-up would spoil the first timings.</td></tr>
                    <tr><td class="stack-title">Sizes shown</td>
                        <td data-label="Set to">JSON: response body bytes. Protobuf: message bytes.</td>
                        <td data-label="Why">Data only, without HTTP headers or gRPC's 5-byte message header.</td></tr>
                </table></div>
            </section>
            """);
    }

    private static void AppendTryLinks(StringBuilder html)
    {
        html.Append("""
            <section class="card card-wide" aria-labelledby="try-title">
                <h2 id="try-title">Try other settings</h2>
                <div class="presets">
                    <a class="chip" href="/weatherbenchmark">Default run</a>
                    <a class="chip" href="/weatherbenchmark?sizes=1,10,50&amp;iterations=50">Tiny replies</a>
                    <a class="chip" href="/weatherbenchmark?sizes=1000,10000,50000&amp;iterations=10">Big replies</a>
                    <a class="chip" href="/weatherbenchmark?parallel=200&amp;parallelSize=500">Bigger burst</a>
                    <a class="chip" href="/weatherbenchmark?streamSize=50000">Long stream</a>
                </div>
                <ul class="muted small">
                    <li><b>Tiny replies:</b> 1 to 50 points; expect a tie, as the first version of this benchmark found.</li>
                    <li><b>Big replies:</b> up to 50,000 points, about 13 MB of JSON.</li>
                    <li><b>Bigger burst:</b> 200 calls at once, 500 points each.</li>
                    <li><b>Long stream:</b> 50,000 streamed points.</li>
                </ul>
            </section>
            """);
    }

    // a table cell saying which protocol won, and by how much
    private static string Verdict(double restMs, double grpcMs)
    {
        if (restMs <= 0 || grpcMs <= 0) return "<td></td>";
        var ratio = restMs / grpcMs;
        if (ratio >= 1.1) return $"""<td class="win">gRPC {ratio.ToString("0.0", Invariant)}&times; faster</td>""";
        if (ratio <= 1 / 1.1) return $"""<td class="lose">REST {(1 / ratio).ToString("0.0", Invariant)}&times; faster</td>""";
        return """<td class="tie">About the same</td>""";
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
