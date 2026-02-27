using System.Text.Json.Serialization;

namespace TrunoWebApp.Models;

public class Metadata
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("offset")] public int Offset { get; set; }
    [JsonPropertyName("pageSize")] public int PageSize { get; set; }
    [JsonPropertyName("remaining")] public int Remaining { get; set; }
}