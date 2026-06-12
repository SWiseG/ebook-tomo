using System.Text.Json;
using Ebook.Application.Common.Settings;
using Ebook.Domain.Abstractions;
using Ebook.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ebook.Infrastructure.Settings;

public sealed class SettingsStore(EbookDbContext db, IClock clock) : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrDefaultAsync<T>(string key, T defaultValue, CancellationToken ct = default)
    {
        var record = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (record is null)
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(record.ValueJson, JsonOptions) ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var record = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (record is null)
        {
            db.Settings.Add(new SettingRecord { Key = key, ValueJson = json, UpdatedAtUtc = clock.UtcNow });
        }
        else
        {
            record.ValueJson = json;
            record.UpdatedAtUtc = clock.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default) =>
        await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.ValueJson, ct);
}
