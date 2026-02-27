using System.Text.Json.Serialization;

namespace TrunoWebApp.Models;

public class Root
{
    [JsonPropertyName("items")]
    public List<Item> Items { get; set; } = [];

    [JsonPropertyName("metadata")]
    public Metadata? Metadata { get; set; }
}