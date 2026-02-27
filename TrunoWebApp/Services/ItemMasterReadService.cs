using Microsoft.EntityFrameworkCore;
using TrunoWebApp.Data;
using TrunoWebApp.Data.Entities;

namespace TrunoWebApp.Services;

public sealed class ItemMasterReadService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ItemMasterReadService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<(List<TruCommerceItemEntity> Items, int Total)> GetItemsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var q = db.TruCommerceItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(x =>
                (x.UpcEAN != null && x.UpcEAN.Contains(search)) ||
                (x.Description != null && x.Description.Contains(search)) ||
                (x.BrandName != null && x.BrandName.Contains(search)));
        }

        var total = await q.CountAsync(ct);

        var items = await q.OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<TruCommerceItemEntity?> GetItemWithPricesAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.TruCommerceItems.AsNoTracking()
            .Include(x => x.RegPrices)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }
}