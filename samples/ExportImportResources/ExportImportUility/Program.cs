
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;


// Simple configuration loader
var configPath = "appsettings.json";
if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Config file not found: {configPath}.");
    return 1;
}

var config = JObject.Parse(File.ReadAllText(configPath));
string fhirServer = config.Value<string>("SourceFhirServerUrl") ?? throw new Exception("SourceFhirServerUrl missing");
string destinationfhirServer = config.Value<string>("DestinationFhirServerUrl") ?? throw new Exception("DestinationFhirServerUrl missing");
string scope = $"{fhirServer.TrimEnd('/')}/.default";

var storage = config["Storage"] ?? throw new Exception("Storage config missing");
string accountName = storage.Value<string>("AccountName") ?? throw new Exception("Storage:AccountName missing");
string containerName = storage.Value<string>("Container") ?? "fhir-import";
string blobNamePrefix = storage.Value<string>("BlobNamePrefix") ?? "imports/";
string blobName = storage.Value<string>("BlobName") ?? "export.ndjson";

bool preferRespondAsync = config.Value<bool?>("PreferRespondAsync") ?? true;
bool includeHistory = config.Value<bool?>("History") ?? false;

// resource input (file or inline)
string resourceInput = "resourceIds.txt";
if (!File.Exists(resourceInput))
{
    Console.Error.WriteLine($"Resource list not found: {resourceInput}");
    return 1;
}

var ids = File.ReadAllLines(resourceInput)
              .Select(l => l?.Trim())
              .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("#"))
              .ToList();

if (ids.Count == 0)
{
    Console.Error.WriteLine("No resource ids found in input.");
    return 1;
}

Console.WriteLine($"Found {ids.Count} resource ids. Fetching from {fhirServer}...");

// Managed Identity credential
var credential = new DefaultAzureCredential();

// Acquire an access token for FHIR using managed identity
var tokenRequest = new TokenRequestContext(new[] { scope });
var accessToken = (await credential.GetTokenAsync(tokenRequest)).Token;

// HttpClient for FHIR calls
using var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

var resources = new List<string>();
async Task FetchPagedHistory(string initialUrl)
{
    string nextUrl = initialUrl;


    while (!string.IsNullOrEmpty(nextUrl))
    {
        var resp = await http.GetAsync(nextUrl);
        if (!resp.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Failed page request {nextUrl}: {(int)resp.StatusCode}");
            return;
        }


        var json = JObject.Parse(await resp.Content.ReadAsStringAsync());


        var entries = json["entry"]?.Children().ToList();
        if (entries != null)
        {
            foreach (var entry in entries)
            {
                var resObj = entry["resource"] as JObject;
                if (resObj != null)
                {
                    resources.Add(resObj.ToString(Newtonsoft.Json.Formatting.None));
                }
            }
        }


        nextUrl = json["link"]?.FirstOrDefault(l => l?["relation"]?.ToString() == "next")?["url"]?.ToString();


        if (!string.IsNullOrEmpty(nextUrl))
            Console.WriteLine($"Following next page: {nextUrl}");
    }
}

foreach (var rid in ids)
{
    // expect format ResourceType/id
    var parts = rid.Split('/');
    if (parts.Length != 2)
    {
        Console.Error.WriteLine($"Skipping invalid resource id: {rid}");
        continue;
    }
    string url = includeHistory? $"{fhirServer.TrimEnd('/')}/{parts[0]}/{parts[1]}/_history?_count=1000": $"{fhirServer.TrimEnd('/')}/{parts[0]}/{parts[1]}";
    try
    {
        if (includeHistory)
        {
            Console.WriteLine($"Fetching history for {rid}");
            await FetchPagedHistory(url);
        }
        else
        {
            var res = await http.GetAsync(url);
            if (!res.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Failed to fetch {rid}: {(int)res.StatusCode} {res.ReasonPhrase}");
                continue;
            }
            var body = await res.Content.ReadAsStringAsync();
            // Ensure single-line JSON (NDJSON)
            var normalized = JObject.Parse(body).ToString(Newtonsoft.Json.Formatting.None);
            resources.Add(normalized);
        }

    Console.WriteLine($"Fetched {rid}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Exception fetching {rid}: {ex.Message}");
    }
}

if (resources.Count == 0)
{
    Console.Error.WriteLine("No resources fetched. Exiting.");
    return 1;
}
// Create NDJSON file
var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
var localTemp = Path.Combine(Path.GetTempPath(), $"fhir-export-{timestamp}.ndjson");
await File.WriteAllLinesAsync(localTemp, resources);
Console.WriteLine($"NDJSON written to {localTemp}");

// Upload to Azure Blob Storage using Managed Identity
string blobUrl;
{
    var blobServiceUrl = new Uri($"https://{accountName}.blob.core.windows.net");
    var blobCredential = credential; // DefaultAzureCredential supports MI / env / CLI
    var client = new BlobServiceClient(blobServiceUrl, blobCredential);
    var container = client.GetBlobContainerClient(containerName);
    await container.CreateIfNotExistsAsync();

    var finalBlobName = $"{blobNamePrefix}{timestamp}-{blobName}";
    var blobClient = container.GetBlobClient(finalBlobName);

    using var fs = File.OpenRead(localTemp);
    await blobClient.UploadAsync(fs, overwrite: true);
    blobUrl = blobClient.Uri.ToString();
    Console.WriteLine($"Uploaded NDJSON to {blobUrl}");
}

// Trigger $import on FHIR server
{
    // Acquire a destination server token for the import call
    using var dehttp = new HttpClient();
    string descope = $"{destinationfhirServer.TrimEnd('/')}/.default";
    var detokenRequest = new TokenRequestContext(new[] { descope });
    var deaccessToken = (await credential.GetTokenAsync(detokenRequest)).Token;
    dehttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", deaccessToken);

    // Build Parameters resource
    var importParams = new JObject
    {
        ["resourceType"] = "Parameters",
        ["parameter"] = new JArray
    {
        new JObject
        {
            ["name"] = "inputFormat",
            ["valueString"] = "application/fhir+ndjson"
        },
        new JObject
        {
            ["name"] = "mode",
            ["valueString"] = "IncrementalLoad"
        },
        new JObject
        {
            ["name"] = "input",
            ["part"] = new JArray
            {
                new JObject
                {
                    ["name"] = "url",
                    ["valueUri"] = blobUrl
                }
            }
        }
    }
    };

    var content = new StringContent(
        importParams.ToString(Newtonsoft.Json.Formatting.None),
        Encoding.UTF8,
        "application/fhir+json"
    );

    if (preferRespondAsync)
    {
        // ensure header is present and only once
        dehttp.DefaultRequestHeaders.Remove("Prefer");
        dehttp.DefaultRequestHeaders.Add("Prefer", "respond-async");
        
    }

    var importUrl = $"{destinationfhirServer.TrimEnd('/')}/$import";
    var resp = await dehttp.PostAsync(importUrl, content);
    var respBody = await resp.Content.ReadAsStringAsync();
    Console.WriteLine($"$import response status: {(int)resp.StatusCode} {resp.ReasonPhrase}");
    Console.WriteLine(respBody);

    if (resp.Content.Headers.TryGetValues("Content-Location", out var values))
    {
        string contentLocation = values.FirstOrDefault();
        Console.WriteLine($"Content-Location: {contentLocation}");
    }
    else
    {
        Console.WriteLine("Content-Location header not found.");
    }
}

return 0;