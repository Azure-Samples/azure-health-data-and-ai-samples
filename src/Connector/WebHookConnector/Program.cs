using System.Reflection;
using WebHookConnector.Configuration;
using WebHookConnector.PostToSubscriber;


MyConfiguration myConfiguration = new();
var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

builder.Services.AddControllers();

var config = builder.Configuration
    .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
    .AddEnvironmentVariables("AZURE_").Build();

config.Bind(myConfiguration);
builder.Services.AddHttpClient<IWebHookCall, WebHookCall>(httpClient =>
{
    httpClient.BaseAddress = new Uri(myConfiguration.UploadFhirJsonURL);
    httpClient.Timeout= TimeSpan.FromHours(1);
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
