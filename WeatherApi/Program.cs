using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;

//========= BUILDER =========
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

//========= WEB APPLICATION =========
var app = builder.Build();

// Redirect HTTP --> HTTPS
app.UseHttpsRedirection();

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
