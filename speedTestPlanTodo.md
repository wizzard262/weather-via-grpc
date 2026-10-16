# gRPC vs REST Benchmark Plan

## Goal
Prove gRPC is faster than REST by isolating transport + serialization 
overhead using two minimal echo endpoints.

----

# 1. Create Two Endpoints (No External Calls) in WeatherService


### REST (HTTP/1.1 + JSON)

A trivial echo endpoint using existing Weather model:
```
app.MapGet("/weather-echo", (double latitude, double longitude) =>
{
    return new
    {
        temperature_2m = 0.0,
        weather_code = 0,
        weather_description = "OK",
        latitude,
        longitude
    };
});
```

### gRPC (HTTP/2 + Protobuf)

Add a new service definition into 'weather.proto' (on Client and Server)
```
 // New minimal echo endpoint
  rpc EchoWeather (google.protobuf.Empty) returns (WeatherResponse);
```

Add a new endpoint to the **GrpcService**
```
public override Task<WeatherResponse> EchoWeather(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
{
    return Task.FromResult(new WeatherResponse
    {
        Temperature2M = 0,
        WeatherCode = 0,
        WeatherDescription = "OK"
    });
}
```

Both endpoints isolate pure protocol overhead:  
No external API calls, logging, EF Core  

---

# 2. Benchmark Method

Run each endpoint c.**100 times**:
- sequential  
- same payload  
- same container  
- same ACA revision  
- same Kestrel port  

Measure:
- total time  
- average  
- min/max  
- p95  (95% latency)

Important:
- do NOT parallelize  
- warm the container first (ignore first run)  
- use same HttpClient lifetime  

---

# 3. Benchmark Code

### REST

```
var client = new HttpClient();
var sw = new Stopwatch();

sw.Start();
for (int i = 0; i < 100; i++)
{
    var result = await client.GetAsync($"{baseUrl}/weather-echo?latitude=53.0&longitude=-2.0");
    await result.Content.ReadAsStringAsync();
}
sw.Stop();

Console.WriteLine($"REST total: {sw.ElapsedMilliseconds} ms");
```

### gRPC

```
var channel = GrpcChannel.ForAddress(baseUrl);
var grpc = new EchoService.EchoServiceClient(channel);

var sw2 = new Stopwatch();

sw2.Start();
for (int i = 0; i < 100; i++)
{
     await grpc.GetWeatherAsync(new WeatherRequest
    {
        Latitude = 53.0,
        Longitude = -2.0
    });
}
sw2.Stop();

Console.WriteLine($"gRPC total: {sw2.ElapsedMilliseconds} ms");
```

---

# 4. Expected Results

## REST (HTTP/1.1 + JSON)
- ~3–8 ms typical  
- JSON overhead  
- HTTP/1.1 handshake  

## gRPC (HTTP/2 + Protobuf)
- ~0.5–2 ms typical  
- binary serialization  
- multiplexed HTTP/2 stream  

gRPC is usually **2–10× faster** for tiny payloads.

---

# 5. Avoid Cold‑Start Pollution
Run benchmark twice:
1. warm container  
2. measure real numbers  

ACA cold start = 1–3 seconds.

---

# 6. What This Test Proves
- protocol difference  
- serialization difference  
- Kestrel pipeline difference  
- ACA ingress behaviour  
- container performance  

No external API noise.

---

# Summary
Create echo endpoints → hit each 100 times → measure → compare → gRPC wins clearly.

----

# Round 2: a workload big enough for gRPC to win

## Why round 1 tied
- The echo reply was 3 fields (about 50 bytes), so each call was almost all network round trip between Azure services.
- The REST loop only read the response text and never parsed the JSON, while gRPC always parses its reply.
- Only gRPC had a parallel test, so there was nothing to compare it with.
- No warm-up, so the first protocol paid for waking the container and opening connections.

## What changed
- New endpoints returning the same made-up hourly forecast of N points (12 fields per point, named like Open-Meteo's hourly variables):
  - REST: `GET /forecast-echo?count=N` (JSON)
  - gRPC: `EchoForecast(ForecastRequest)` and `StreamForecast(ForecastRequest)` (server stream)
- Server builds the data once per N and keeps it in memory (`WeatherService/Services/ForecastData.cs`).
- Client benchmark (`WeatherApi/Benchmark/`): shared HttpClient and GrpcChannel, warm-up calls, REST and gRPC taking turns,
  REST parses JSON into typed objects, avg / p95 per call, and bytes per reply.
- Three tests: size sweep (1, 100, 1,000, 10,000 points), parallel burst for both protocols (cold and warm), and streaming (time to first point).
- Settings can be changed in the query string, i.e. `/weatherbenchmark?sizes=1000,10000,50000&iterations=10`.
  The results page lists every setting, its value and why.

## Local results (laptop, no network, 2026-10-16)
| Points | JSON | Protobuf | REST avg | gRPC avg |
|---|---|---|---|---|
| 1 | 291 B | 98 B | 0.28 ms | 0.36 ms |
| 100 | 27.4 KB | 9.8 KB | 1.71 ms | 0.48 ms |
| 1,000 | 273 KB | 97 KB | 10.9 ms | 1.90 ms |
| 10,000 | 2.67 MB | 975 KB | 52.9 ms | 11.3 ms |

Streaming 10,000 points: first gRPC point after 0.6 ms, whole REST document after 27 ms.

## Server in Docker capped like Azure (`--cpus 0.25 --memory 512m`, 2026-10-16)
| Points | REST median | gRPC median | Result |
|---|---|---|---|
| 1 | 0.77 ms | 0.89 ms | REST 1.2x faster |
| 100 | 1.96 ms | 1.05 ms | gRPC 1.9x faster |
| 1,000 | 11.5 ms | 4.18 ms | gRPC 2.8x faster |
| 10,000 | 110 ms | 87 ms | gRPC 1.3x faster (2.3x on another run) |
| 50,000 | 341 ms | 65 ms | gRPC 5.2x faster |

Parallel burst (100 x 1,000 points): REST 972 ms vs gRPC 342 ms cold, 963 ms vs 400 ms warm.
Streaming 10,000 points: first gRPC point after 1.5 ms, whole REST document after 105 ms.
Timings jump about between runs because the 0.25 CPU cap throttles in bursts; the byte sizes never change.

## Live on Azure (App Service client to Container App server, 2026-10-16)
| Points | REST median | gRPC median | Result |
|---|---|---|---|
| 1 | 2.88 ms | 2.99 ms | About the same |
| 100 | 4.36 ms | 3.76 ms | gRPC 1.2x faster |
| 1,000 | 93.1 ms | 42.3 ms | gRPC 2.2x faster |
| 10,000 | 518 ms | 82.5 ms | gRPC 6.3x faster (8.9x on another run) |
| 50,000 | 1,779 ms | 625 ms | gRPC 2.8x faster |

Parallel burst (100 x 1,000 points): REST 4,246 ms vs gRPC 1,610 ms cold, 1,510 ms vs 495 ms warm.
Streaming 10,000 points: first gRPC point after 3.3 ms, whole REST document after 415 ms.
