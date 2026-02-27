namespace TrunoWebApp.Data.Entities;

public class TruCommerceItemEntity
{
    public int Id { get; set; }

    public string? UpcEAN { get; set; }
    public string? Description { get; set; }
    public string? BrandName { get; set; }
    public string? Category { get; set; }
    public int? Department { get; set; }
    public DateTime? DateModified { get; set; }

    public decimal? EffectiveUnitPrice { get; set; }
    public decimal? EffectiveCasePrice { get; set; }
    public int? EffectiveCaseSize { get; set; }

    public string RawJson { get; set; } = "{}";
    public DateTime SyncedUtc { get; set; } = DateTime.UtcNow;

    public List<TruCommerceRegPriceEntity> RegPrices { get; set; } = [];
}