namespace TrunoWebApp.Data.Entities;

public class TruCommerceRegPriceEntity
{
    public int ItemId { get; set; }
    public int RegPriceId { get; set; }

    public decimal? UnitPrice { get; set; }
    public decimal? CasePrice { get; set; }
    public int? CaseSize { get; set; }
    public DateTime? IpStartDate { get; set; }
    public DateTime? IpEndDate { get; set; }
    public int? PriceType { get; set; }

    public string RawJson { get; set; } = "{}";
    public DateTime SyncedUtc { get; set; } = DateTime.UtcNow;

    public TruCommerceItemEntity? Item { get; set; }
}