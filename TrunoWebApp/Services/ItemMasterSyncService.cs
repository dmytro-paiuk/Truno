using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrunoWebApp.Data;
using TrunoWebApp.Data.Entities;
using TrunoWebApp.Models;

namespace TrunoWebApp.Services;

public sealed class ItemMasterSyncService
{
    private const int PageSize = 1000;

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;

    public ItemMasterSyncService(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _httpFactory = httpFactory;
    }

    public async Task<int> SyncAsync(Action<string>? progress = null, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient("TruCommerce");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var offset = 0;
        var totalSaved = 0;

        progress?.Invoke("Starting sync...");

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var relativeUrl = $"itemMaster/query?pageSize={PageSize}&offset={offset}";
            progress?.Invoke($"Requesting page. Offset={offset}...");

            using var resp = await http.GetAsync(relativeUrl, ct);
            progress?.Invoke($"HTTP {(int)resp.StatusCode} received. Reading body...");

            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {body}");

            Root? root;
            try
            {
                root = JsonSerializer.Deserialize<Root>(body, jsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"JSON deserialization failed: {ex.Message}. Path={ex.Path}, Line={ex.LineNumber}, Byte={ex.BytePositionInLine}",
                    ex);
            }

            if (root?.Items is null || root.Items.Count == 0)
            {
                progress?.Invoke("No more items.");
                break;
            }

            var pageIds = root.Items.Select(x => x.Id).ToArray();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Load existing items for this page
                var existingItems = await _db.TruCommerceItems
                    .Where(x => pageIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

                // Load existing reg prices for these items
                var existingRegPrices = await _db.TruCommerceRegPrices
                    .Where(x => pageIds.Contains(x.ItemId))
                    .ToListAsync(ct);

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
                        _db.TruCommerceItems.Add(itemEntity);
                        existingItems[apiItem.Id] = itemEntity;
                    }

                    itemEntity.UpcEAN = apiItem.UpcEAN;
                    itemEntity.Description = apiItem.Description;
                    itemEntity.BrandName = apiItem.BrandName;
                    itemEntity.Category = apiItem.Category;
                    itemEntity.Department = apiItem.Department == 0 ? null : apiItem.Department;
                    itemEntity.DateModified = ParseDate(apiItem.DateModified);

                    itemEntity.EffectiveUnitPrice = apiItem.EffectivePrice is null
                        ? null
                        : (decimal)apiItem.EffectivePrice.UnitPrice;

                    itemEntity.EffectiveCasePrice = apiItem.EffectivePrice is null
                        ? null
                        : (decimal)apiItem.EffectivePrice.CasePrice;

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

                                _db.TruCommerceRegPrices.Add(rpEntity);
                                existingRegMap[key] = rpEntity;
                            }

                            rpEntity.UnitPrice = rp.UnitPrice;
                            rpEntity.CasePrice = rp.CasePrice;
                            rpEntity.CaseSize = rp.CaseSize;
                            rpEntity.IpStartDate = ParseDate(rp.IpStartDate);
                            rpEntity.IpEndDate = ParseDate(rp.IpEndDate);
                            rpEntity.PriceType = rp.PriceType;

                            rpEntity.RawJson = rpRawJson;
                            rpEntity.SyncedUtc = DateTime.UtcNow;
                        }
                    }
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                totalSaved += root.Items.Count;
                progress?.Invoke(
                    $"Saved page: {root.Items.Count}. Total saved: {totalSaved}. Remaining: {root.Metadata?.Remaining}");

                if ((root.Metadata?.Remaining ?? 0) <= 0 || root.Items.Count < PageSize)
                    break;

                offset += PageSize;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        progress?.Invoke($"Done. Total items saved: {totalSaved}");
        return totalSaved;
    }

    private static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, out var dt) ? dt : null;
}