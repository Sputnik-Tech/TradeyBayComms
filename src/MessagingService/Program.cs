// using MessagingService.Services;

using System.Net;
using System.Security.Cryptography.X509Certificates;
using MessagingService.Data;
using MessagingService.Interceptors;
using MessagingService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Register DbContext
builder.Logging.AddConsole();
builder.Services.AddDbContext<MessagingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
    .EnableSensitiveDataLogging()
    );

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

// --- Blob Storage ---
// Assuming BlobStorageService implements IBlobStorageService from previous example
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

// Use AddSingleton for the in-memory manager.
builder.Services.AddSingleton<IConnectionManager, InMemoryConnectionManager>();

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
    // options.Interceptors.Add<AzureADB2CAuthInterceptor>();
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddHttpContextAccessor();

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




/* 
// using MessagingService.Services; // Already likely included via ImplicitUsings or namespace

using MessagingService.Data;
using MessagingService.Services; // Add this if BlobStorageService/ConnectionManager are here
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using TradeyBay.Messaging.Grpc; // Your generated gRPC namespace
// TODO: Add using statements for your Auth and Ads gRPC client namespaces
// using YourAuthServiceNamespace;
// using YourAdsServiceNamespace;


var builder = WebApplication.CreateBuilder(args);

// --- Logging ---
builder.Logging.ClearProviders(); // Optional: Clear default providers if needed
builder.Logging.AddConsole();

// --- Database ---
builder.Services.AddDbContext<MessagingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
    {
        // Optional: Add Npgsql specific options if needed
        // npgsqlOptions.EnableRetryOnFailure();
    })
    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()) // Only log sensitive data in Dev
    );

// --- Blob Storage ---
// Assuming BlobStorageService implements IBlobStorageService from previous example
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
// If your service is called ImageStorageService and doesn't use an interface:
// builder.Services.AddSingleton<ImageStorageService>(); // Or AddScoped if it has scoped dependencies

// --- Real-time Connection Management ---
// Use AddSingleton for the in-memory manager.
builder.Services.AddSingleton<IConnectionManager, InMemoryConnectionManager>();

// --- Authentication (using Microsoft.Identity.Web) ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options =>
    {
        builder.Configuration.Bind("AzureAdB2C", options);
        // Ensure B2C issues 'oid' or 'sub' claim. 'name' might not be unique.
        // options.TokenValidationParameters.NameClaimType = "name"; // Let's rely on oid/sub instead
    },
    options => {
        builder.Configuration.Bind("AzureAdB2C", options);
    });

// --- Authorization ---
builder.Services.AddAuthorization(); // Policies can be added here if needed

// --- gRPC Configuration ---
builder.Services.AddGrpc(options =>
{
    // options.Interceptors.Add<AzureADB2CAuthInterceptor>(); // Removed for simplicity for now
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
})
.AddJsonTranscoding(); // Keep if you plan REST fallback/alternative

// --- gRPC Clients for External Services ---
builder.Services.AddGrpcClient<AuthService.AuthServiceClient>(o => // Replace with your actual Auth service client class
{
    o.Address = new Uri(builder.Configuration["GrpcServices:AuthServiceUrl"] ?? "http://auth-service-address"); // Get address from config
})
.ConfigureChannel(o =>
{
    // Add configuration for resilience, load balancing etc. if needed
    // o.Credentials = ... // If auth needed between services
});

builder.Services.AddGrpcClient<AdsService.AdsServiceClient>(o => // Replace with your actual Ads service client class
{
    o.Address = new Uri(builder.Configuration["GrpcServices:AdsServiceUrl"] ?? "http://ads-service-address"); // Get address from config
});
// --- End gRPC Clients ---

// Required for accessing HttpContext in services if needed (e.g., for GetUserId initially)
builder.Services.AddHttpContextAccessor();


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting(); // Routing must come first

app.UseAuthentication(); // Verifies the token
app.UseAuthorization(); // Enforces [Authorize] attributes

// Map gRPC Services
// ** IMPORTANT: Map your actual implementation! **
app.MapGrpcService<MessagingServiceImpl>();

// Keep the default check endpoint
app.MapGet("/", () => "Messaging Service is running. Communication with gRPC endpoints must be made through a gRPC client.");

// --- Database Migrations (Optional: Apply on startup for dev/simple scenarios) ---
// Be cautious using this in production, prefer dedicated migration strategies.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
    // dbContext.Database.Migrate(); // Uncomment to apply migrations on startup
}
// --- End Database Migrations ---

app.Run(); */