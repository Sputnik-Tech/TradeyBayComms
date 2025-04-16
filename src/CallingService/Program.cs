// using CallingService.Services;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using CallingService.Interceptors;
using CallingService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Register DbContext
builder.Logging.AddConsole();
/* builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
    .EnableSensitiveDataLogging()
    ); */

builder.Services.AddScoped<ImageStorageService>();

/* // Configure Kestrel to listen for both HTTP/2 (gRPC) and HTTP/1.1 (REST)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 80, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        Console.WriteLine("Listening on HTTP/1.1 and HTTP/2 (port 80)");
    });


    options.Listen(IPAddress.Any, 443, listenOptions =>
    {
        // listenOptions.Protocols = HttpProtocols.Http2;
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = X509Certificate2.CreateFromPemFile(
                "/app/certificates/tls.crt",
                "/app/certificates/tls.key"
            );
        });
    });
}); */

// Configure Azure AD B2C authentication using JwtBearer and Microsoft.Identity.Web
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options =>
    {
        builder.Configuration.Bind("AzureAdB2C", options);
        options.TokenValidationParameters.NameClaimType = "name"; // Adjust based on your needs
    }, options => { builder.Configuration.Bind("AzureAdB2C", options); });

builder.Services.AddAuthorization();

// Add services to the container.
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<AzureADB2CAuthInterceptor>();
}).AddJsonTranscoding();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting(); // Routing must come first

// --- Authentication/Authorization Middleware ---
app.UseAuthentication(); // Verifies the token
app.UseAuthorization(); // Enforces [Authorize] attributes
// --- End Authentication/Authorization Middleware ---


// Map gRPC Services
// app.MapGrpcService<MessagingServiceImpl>(); // Replace with your actual service implementation classes
// app.MapGrpcService<CallingServiceImpl>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
