using System.Text.Json.Serialization;

namespace TrunoWebApp.Models;

public class RegPrice
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("unitPrice")] public decimal? UnitPrice { get; set; }
    [JsonPropertyName("casePrice")] public decimal? CasePrice { get; set; }
    [JsonPropertyName("caseSize")] public int CaseSize { get; set; }
    [JsonPropertyName("ipStartDate")] public string? IpStartDate { get; set; }
    [JsonPropertyName("ipEndDate")] public string? IpEndDate { get; set; }
    [JsonPropertyName("priceType")] public int PriceType { get; set; }
}