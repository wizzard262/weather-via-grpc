
using static WeatherService.Services.GrpcService;

//=============== BUILDER ==========================
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// specifically for grpc GetWeather
builder.Services.AddGrpc();
builder.Services.AddHttpClient();

//=============== WEB APPLICATION ==================
var app = builder.Build();

// Enables gRPC‑Web so browsers and JavaScript clients can call gRPC services
app.UseGrpcWeb();

// Map gRPC service, and enable for web (required for browser or non-gRPC clients)
app.MapGrpcService<WeatherGrpcService>().EnableGrpcWeb();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
