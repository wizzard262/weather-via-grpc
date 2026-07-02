using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

//========= BUILDER =========
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

//========= WEB APPLICATION =========
var app = builder.Build();

// IMPORTANT: Remove HTTPS redirection for gRPC in Production
// gRPC over HTTPS already works automatically.
// But UseHttpsRedirection breaks gRPC because it downgrades HTTP/2 to HTTP/1.1.
// In ACA, you do NOT need this — ACA terminates TLS at the front door.
// Comment it out or delete it.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Serve wwwroot
app.UseStaticFiles();

// Add MIME type for .proto
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".proto"] = "text/plain";

// get proto path
var protosPath = Path.Combine(Directory.GetCurrentDirectory(), "Protos");

// Serve the Protos folder
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(protosPath),
    RequestPath = "/protos",
    ContentTypeProvider = provider
});

app.UseAuthorization();
app.MapControllers();
app.Run();
