using System.Text.Json.Serialization;

namespace TrunoWebApp.Models;

public class EffectivePrice
{
    [JsonPropertyName("unitPrice")] public double UnitPrice { get; set; }
    [JsonPropertyName("casePrice")] public double CasePrice { get; set; }
    [JsonPropertyName("caseSize")] public int CaseSize { get; set; }
}