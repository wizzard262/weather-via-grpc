using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

// Redirect HTTP → HTTPS
app.UseHttpsRedirection();

// Serve wwwroot
app.UseStaticFiles();

// ABSOLUTE PATH to your proto folder
var protosPath = @"C:\DEV\Repositories\GitHub\weather-via-grpc\weatherApi\Protos";

// Add MIME type for .proto
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".proto"] = "text/plain";

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
