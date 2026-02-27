// See https://aka.ms/new-console-template for more information

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

public static class Program
{
    // Hardcoded config (as requested)
    private const string BaseUrl = "https://test.trucommerce.com/v2/"; // IMPORTANT: trailing slash
    private const string ApiUser = "epic-test-ecom";
    private const string ApiPassword = "3M4R#mUEHVYbG8ZV";
    private const int PageSize = 1000;

    // SQL Server connection string (hardcoded)
    private const string DbConnectionString =
        "Server=localhost,1433;Database=TrunoDb;User Id=sa;Password=DB_Password;TrustServerCertificate=True;";

    public static async Task Main()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        using var http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(120)
        };

        // Basic auth
        var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApiUser}:{ApiPassword}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Epic-Test");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(DbConnectionString)
            .EnableSensitiveDataLogging(false)
            .Options;

        await using var db = new AppDbContext(dbOptions);

        // Quick start. For production: use migrations instead.
        await db.Database.EnsureCreatedAsync();

        var offset = 0;
        var totalSaved = 0;

        while (true)
        {
            // IMPORTANT: no leading slash here, otherwise "/v2" can be dropped.
            var relativeUrl = $"itemMaster/query?pageSize={PageSize}&offset={offset}";

            using var resp = await http.GetAsync(relativeUrl);
            Console.WriteLine($"GET {resp.RequestMessage?.RequestUri}");

            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");
                Console.WriteLine(body);
                return;
            }

            Root? root;
            try
            {
                root = JsonSerializer.Deserialize<Root>(body, jsonOptions);
            }
            catch (JsonException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine($"Path: {ex.Path}");
                Console.WriteLine($"Line: {ex.LineNumber}, Byte: {ex.BytePositionInLine}");
                throw;
            }

            if (root?.Items is null || root.Items.Count == 0)
            {
                Console.WriteLine("No more items.");
                break;
            }

            var pageIds = root.Items.Select(x => x.Id).ToArray();

            await using var tx = await db.Database.BeginTransactionAsync();

            try
            {
                // Load existing items for this page
                var existingItems = await db.TruCommerceItems
                    .Where(x => pageIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                // Load existing reg prices for these items
                var existingRegPrices = await db.TruCommerceRegPrices
                    .Where(x => pageIds.Contains(x.ItemId))
                    .ToListAsync();

                var existingRegMap = existingRegPrices.ToDictionary(
                    x => (x.ItemId, x.RegPriceId),
                    x => x
                );

                foreach (var apiItem in root.Items)
                {
                    var itemRawJson = JsonSerializer.Serialize(apiItem);

                    if (!existingItems.TryGetValue(apiItem.Id, out var itemEntity))
                    {
                        itemEntity = new TruCommerceItemEntity { Id = apiItem.Id };
                        db.TruCommerceItems.Add(itemEntity);
                        existingItems[apiItem.Id] = itemEntity;
                    }

                    itemEntity.UpcEAN = apiItem.UpcEAN;
                    itemEntity.Description = apiItem.Description;
                    itemEntity.BrandName = apiItem.BrandName;
                    itemEntity.Category = apiItem.Category;
                    itemEntity.Department = apiItem.Department == 0 ? null : apiItem.Department;
                    itemEntity.DateModified = ParseDate(apiItem.DateModified);

                    itemEntity.EffectiveUnitPrice = apiItem.EffectivePrice is null ? null : (decimal)apiItem.EffectivePrice.UnitPrice;
                    itemEntity.EffectiveCasePrice = apiItem.EffectivePrice is null ? null : (decimal)apiItem.EffectivePrice.CasePrice;
                    itemEntity.EffectiveCaseSize = apiItem.EffectivePrice?.CaseSize;

                    itemEntity.RawJson = itemRawJson;
                    itemEntity.SyncedUtc = DateTime.UtcNow;

                    if (apiItem.RegPrices is { Count: > 0 })
                    {
                        foreach (var rp in apiItem.RegPrices)
                        {
                            var key = (apiItem.Id, rp.Id);
                            var rpRawJson = JsonSerializer.Serialize(rp);

                            if (!existingRegMap.TryGetValue(key, out var rpEntity))
                            {
                                rpEntity = new TruCommerceRegPriceEntity
                                {
                                    ItemId = apiItem.Id,
                                    RegPriceId = rp.Id
                                };
                                db.TruCommerceRegPrices.Add(rpEntity);
                                existingRegMap[key] = rpEntity;
                            }

                            rpEntity.UnitPrice = rp.UnitPrice;
                            rpEntity.CasePrice = (decimal)rp.CasePrice;
                            rpEntity.CaseSize = rp.CaseSize;
                            rpEntity.IpStartDate = ParseDate(rp.IpStartDate);
                            rpEntity.IpEndDate = ParseDate(rp.IpEndDate);
                            rpEntity.PriceType = rp.PriceType;

                            rpEntity.RawJson = rpRawJson;
                            rpEntity.SyncedUtc = DateTime.UtcNow;
                        }
                    }
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                totalSaved += root.Items.Count;
                Console.WriteLine($"Saved page: {root.Items.Count}. Total saved: {totalSaved}. Remaining: {root.Metadata?.Remaining}");

                if ((root.Metadata?.Remaining ?? 0) <= 0 || root.Items.Count < PageSize)
                    break;

                offset += PageSize;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        Console.WriteLine($"Done. Total items saved: {totalSaved}");
    }

    private static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, out var dt) ? dt : null;
}

/* =========================
   API DTOs
   ========================= */

public sealed class Root
{
    [JsonPropertyName("items")]
    public List<Item> Items { get; set; } = [];

    [JsonPropertyName("metadata")]
    public Metadata? Metadata { get; set; }
}

public sealed class Metadata
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("offset")] public int Offset { get; set; }
    [JsonPropertyName("pageSize")] public int PageSize { get; set; }
    [JsonPropertyName("remaining")] public int Remaining { get; set; }
}

public sealed class Item
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

public sealed class EffectivePrice
{
    [JsonPropertyName("unitPrice")] public double UnitPrice { get; set; }
    [JsonPropertyName("casePrice")] public double CasePrice { get; set; }
    [JsonPropertyName("caseSize")] public int CaseSize { get; set; }
}

public sealed class RegPrice
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("unitPrice")] public decimal? UnitPrice { get; set; }
    [JsonPropertyName("casePrice")] public decimal? CasePrice { get; set; }
    [JsonPropertyName("caseSize")] public int CaseSize { get; set; }
    [JsonPropertyName("ipStartDate")] public string? IpStartDate { get; set; }
    [JsonPropertyName("ipEndDate")] public string? IpEndDate { get; set; }
    [JsonPropertyName("priceType")] public int PriceType { get; set; }
}

/* =========================
   EF Core entities
   ========================= */

public sealed class TruCommerceItemEntity
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

public sealed class TruCommerceRegPriceEntity
{
    public int ItemId { get; set; }
    public int RegPriceId { get; set; }

    public decimal? UnitPrice { get; set; }   // instead of int?
    public decimal? CasePrice { get; set; }
    public int? CaseSize { get; set; }
    public DateTime? IpStartDate { get; set; }
    public DateTime? IpEndDate { get; set; }
    public int? PriceType { get; set; }

    public string RawJson { get; set; } = "{}";
    public DateTime SyncedUtc { get; set; } = DateTime.UtcNow;

    public TruCommerceItemEntity? Item { get; set; }
}

/* =========================
   DbContext
   ========================= */

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TruCommerceItemEntity> TruCommerceItems => Set<TruCommerceItemEntity>();
    public DbSet<TruCommerceRegPriceEntity> TruCommerceRegPrices => Set<TruCommerceRegPriceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TruCommerceItemEntity>(e =>
        {
            e.ToTable("TruCommerceItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();

            e.Property(x => x.UpcEAN).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(400);
            e.Property(x => x.BrandName).HasMaxLength(200);
            e.Property(x => x.Category).HasMaxLength(200);

            e.Property(x => x.EffectiveUnitPrice).HasColumnType("decimal(18,4)");
            e.Property(x => x.EffectiveCasePrice).HasColumnType("decimal(18,4)");

            e.Property(x => x.RawJson).IsRequired();
            e.Property(x => x.SyncedUtc).IsRequired();
        });

        modelBuilder.Entity<TruCommerceRegPriceEntity>(e =>
        {
            e.ToTable("TruCommerceRegPrices");
            e.HasKey(x => new { x.ItemId, x.RegPriceId });

            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(x => x.CasePrice).HasColumnType("decimal(18,4)");

            e.Property(x => x.RawJson).IsRequired();
            e.Property(x => x.SyncedUtc).IsRequired();

            e.HasOne(x => x.Item)
                .WithMany(i => i.RegPrices)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}