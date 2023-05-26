using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FhirBlaze.SharedComponents;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using FhirBlaze.Graph;
using System.Net.Http;
using Blazored.Modal;
using FhirBlaze.SharedComponents.Services;
using FhirBlaze.Model;
using DiffPlex.DiffBuilder;
using DiffPlex;

namespace FhirBlaze
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("#app");


            SystemUrl systemUrl = new();
            systemUrl.LookupCodeSystemUrlList = new();
            systemUrl.TransLateCodeSystemUrlList = new();
            var lookupCodeSystemUrls = builder.Configuration.GetSection("LookupCodeSystemUrls").GetChildren();
            var translateCodeSystemUrls = builder.Configuration.GetSection("TranslateCodeSystemUrls").GetChildren();

            foreach (var item in lookupCodeSystemUrls)
            {
                CodeSystemModel codeSystem = new CodeSystemModel()
                {
                    CodeSystemName = item.Key,
                    CodeSystemValue = item.Value
                };

                systemUrl.LookupCodeSystemUrlList.Add(codeSystem);
            }

            foreach (var item in translateCodeSystemUrls)
            {
                CodeSystemModel codeSystem = new CodeSystemModel()
                {
                    CodeSystemName = item.Key,
                    CodeSystemValue = item.Value
                };

                systemUrl.TransLateCodeSystemUrlList.Add(codeSystem);
            }

            builder.Services.AddSingleton(systemUrl);


            builder.Services.AddHttpClient<GraphClientFactory>(sp => new HttpClient { BaseAddress = new Uri("https://graph.microsoft.com") });
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddMsalAuthentication<RemoteAuthenticationState, RemoteUserAccount>(options =>
            {
                var scopes = builder.Configuration.GetValue<string>("GraphScopes");
                if (string.IsNullOrEmpty(scopes))
                {
                    Console.WriteLine("WARNING: No permission scopes were found in the GraphScopes app setting. Using default User.Read.");
                    scopes = "User.Read";
                }

                foreach (var scope in scopes.Split(';'))
                {
                    Console.WriteLine($"Adding {scope} to requested permissions");
                    options.ProviderOptions.DefaultAccessTokenScopes.Add(scope);
                }

                builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
                options.ProviderOptions.LoginMode = "redirect";
            })
            .AddAccountClaimsPrincipalFactory<RemoteAuthenticationState, RemoteUserAccount, GraphUserAccountFactory>();

            builder.Services.AddScoped<GraphClientFactory>();
            if (builder.Configuration.GetValue<bool>("UseGraphir"))
            {
            }
            else
            {

                builder.Services.AddFhirService(() =>
                {
                    var fhir = new FhirDataConnection();
                    builder.Configuration.Bind("FhirConnection", fhir);
                    return fhir;
                });
            }

            var aPIM = new APIMDataConnection();
            builder.Configuration.Bind("APIMConnection", aPIM);
            builder.Services.AddSingleton(aPIM);
            builder.Services.AddScoped<IAPIMService, APIMService>();
            builder.Services.AddHttpClient();

            builder.Services.AddBlazoredModal();

            builder.Services.AddScoped<ISideBySideDiffBuilder, SideBySideDiffBuilder>();
            builder.Services.AddScoped<IDiffer, Differ>();


            await builder.Build().RunAsync();
        }
    }
}