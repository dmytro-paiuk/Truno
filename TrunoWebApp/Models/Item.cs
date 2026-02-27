using System.Text.Json.Serialization;

namespace TrunoWebApp.Models;

public class Item
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("upcEAN")] public string? UpcEAN { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("brandName")] public string? BrandName { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("department")] public int Department { get; set; }
    [JsonPropertyName("dateModified")] public string? DateModified { get; set; }

    [JsonPropertyName("effectivePrice")] public EffectivePrice? EffectivePrice { get; set; }
    [JsonPropertyName("regPrices")] public List<RegPrice> RegPrices { get; set; } = [];
}