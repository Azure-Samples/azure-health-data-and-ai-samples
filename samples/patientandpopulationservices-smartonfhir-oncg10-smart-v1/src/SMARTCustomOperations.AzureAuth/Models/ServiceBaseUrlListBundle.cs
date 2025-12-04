using System.Text.Json.Serialization;

namespace SMARTCustomOperations.AzureAuth.Models
{
	public class ResourceBase
	{
		[JsonPropertyName("resourceType")]
		public string? ResourceType { get; set; }
		[JsonPropertyName("id")]
		public string? Id { get; set; }
	}
	public class Address
	{
		[JsonPropertyName("line")]
		public List<string>? Line { get; set; }
		[JsonPropertyName("city")]
		public string? City { get; set; }
		[JsonPropertyName("state")]
		public string? State { get; set; }
		[JsonPropertyName("postalCode")]
		public string? PostalCode { get; set; }
		[JsonPropertyName("country")]
		public string? Country { get; set; }
	}
	public class Identifier
	{
		[JsonPropertyName("system")]
		public string? System { get; set; }
		[JsonPropertyName("value")]
		public string? Value { get; set; }
	}
	public class EndpointResource : ResourceBase
	{		
		[JsonPropertyName("status")]
		public string? Status { get; set; }
		[JsonPropertyName("connectionType")]
		public ConnectionType? ConnectionType { get; set; }
		[JsonPropertyName("name")]
		public string? Name { get; set; } 
		[JsonPropertyName("payloadType")]
		public List<PayloadType>? PayloadType { get; set; }
		[JsonPropertyName("address")]
		public string? Address { get; set; }
	}
	public class ConnectionType
	{
		[JsonPropertyName("system")]
		public string? System { get; set; }
		[JsonPropertyName("code")]
		public string? Code { get; set; }
	}
	public class PayloadType
	{
		[JsonPropertyName("coding")]
		public List<Coding>? Coding { get; set; }
	}
	public class Coding
	{
		[JsonPropertyName("system")]
		public string? System { get; set; } 
		[JsonPropertyName("code")]
		public string? Code { get; set; } 
	}

	public class OrganizationResource : ResourceBase
	{				
		[JsonPropertyName("identifier")]
		public List<Identifier>? Identifier { get; set; }
		[JsonPropertyName("active")]
		public bool? Active { get; set; }
		[JsonPropertyName("name")]
		public string? Name { get; set; }
		[JsonPropertyName("address")]
		public List<Address>? Address { get; set; }
		[JsonPropertyName("endpoint")]
		public List<EndpointReference>? Endpoint { get; set; }
	}
	public class EndpointReference
	{
		[JsonPropertyName("reference")]
		public string? Reference { get; set; }
	}
	public class Entry
	{
		[JsonPropertyName("fullUrl")]
		public string? FullUrl { get; set; }
		[JsonPropertyName("resource")]
		public object? Resource { get; set; }
	}
	public class Bundle : ResourceBase
	{
		[JsonPropertyName("type")]
		public string? Type { get; set; } = "collection";
		[JsonPropertyName("entry")]
		public List<Entry>? Entry { get; set; }
	}
	public enum ResourceType
	{
		Bundle,
		Organization,
		Endpoint
	}


}
