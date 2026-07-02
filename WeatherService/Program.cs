using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using static WeatherService.Services.GrpcService;

//=============== BUILDER ==========================
var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 5000
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP/1.1 (REST endpoints) on port 
    options.Listen(IPAddress.Any, 7010, listenOptions =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Local dev: HTTPS
            listenOptions.UseHttps();
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        }
        else
        {
            // Docker: HTTP only
            listenOptions.Protocols = HttpProtocols.Http1;
        }
    });

    //  gRPC always HTTP/2
    options.Listen(IPAddress.Any, 5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddHttpClient();
builder.Services.AddControllers();

//=============== WEB APPLICATION ==================
var app = builder.Build();

// Enables gRPC‑Web so browsers and JavaScript clients can call gRPC services
app.UseGrpcWeb();

//do before MapGet()
app.MapControllers();

// REST endpoint (HTTP/1.1) - ping endpoint to check if the server is alive
app.MapGet("/ping", () => "Server is alive");

// Map gRPC ensure it stays on HTTP/2
app.MapGrpcService<WeatherGrpcService>().EnableGrpcWeb();

app.UseAuthorization();
app.Run();
