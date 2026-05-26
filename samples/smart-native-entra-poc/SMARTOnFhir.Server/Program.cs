using Azure.Identity;
using Microsoft.Graph;
using SMARTOnFhir.Server.Configuration;
using SMARTOnFhir.Server.Services;

var builder = WebApplication.CreateBuilder(args);


// Bind configuration
var smartConfig = new SmartConfig();
builder.Configuration.GetSection("SmartConfig").Bind(smartConfig);
builder.Services.Configure<SmartConfig>(builder.Configuration.GetSection("SmartConfig"));

// Add services
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// Add token validation service
builder.Services.AddSingleton(new TokenValidationService(smartConfig));

// Add Microsoft Graph client
// In Azure: set AZURE_CLIENT_ID env var to use Managed Identity
// Locally: uses Azure CLI (az login) or Visual Studio credential
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    TenantId = smartConfig.TenantId,
    ExcludeManagedIdentityCredential = !builder.Environment.IsProduction(),
    ExcludeInteractiveBrowserCredential = true,
    ExcludeSharedTokenCacheCredential = true,
    ExcludeVisualStudioCodeCredential = true,
    ExcludeEnvironmentCredential = true,
});
builder.Services.AddSingleton(new GraphServiceClient(credential));

// Add CORS — allows React dev server during development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors();

// Serve React consent UI: middleware serves index.html for non-file /auth/context/ requests
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/auth/context", StringComparison.OrdinalIgnoreCase) && !Path.HasExtension(path))
    {
        context.Request.Path = "/auth/context/index.html";
    }
    await next();
});

app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.Run();
