# gRPC vs REST Benchmark Plan

## Goal
Prove gRPC is faster than REST by isolating transport + serialization 
overhead using two minimal echo endpoints.

----

# 1. Create Two Endpoints (No External Calls)


### REST (HTTP/1.1 + JSON)
```
app.MapGet("/echo-int", (int value) => new { value });
```

### gRPC (HTTP/2 + Protobuf)

```
message EchoIntRequest { int32 value = 1; }
message EchoIntResponse { int32 value = 1; }

service EchoService {
  rpc EchoInt(EchoIntRequest) returns (EchoIntResponse);
}

Server handler:
public override Task<EchoIntResponse> EchoInt(EchoIntRequest request, ServerCallContext context)
{
    return Task.FromResult(new EchoIntResponse { Value = request.Value });
}
```

Both endpoints isolate pure protocol overhead :
- no external API calls  
- no logging  
- no EF Core  
- minimal serialization

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
    var result = await client.GetAsync($"{baseUrl}/echo-int?value=123");
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
    await grpc.EchoIntAsync(new EchoIntRequest { Value = 123 });
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
