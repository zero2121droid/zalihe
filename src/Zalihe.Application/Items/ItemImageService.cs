using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;

namespace Zalihe.Application.Items;

public record ItemImageFile(string ContentType, byte[] Data, DateTimeOffset UpdatedAt);

/// <summary>
/// Pictures the user adds to items. One per item: a new one replaces the old. Removing it shows the
/// shop's product image again, if there is one.
/// </summary>
public class ItemImageService(IAppDbContext db, ItemService items, TimeProvider timeProvider)
{
    public const string TooLarge = "item.image_too_large";
    public const string Unsupported = "item.image_unsupported";
    public const string Field = "file";

    /// <summary>
    /// Sets the item's picture from an uploaded file of <paramref name="length"/> bytes; the result has
    /// the updated item. A file that is too large is refused before it is read.
    /// </summary>
    public async Task<SaveItemResult> SetAsync(Guid itemId, Stream content, long length, CancellationToken ct)
    {
        // The tenant filter makes another company's item "not found".
        var item = await db.Items.SingleOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return SaveItemResult.Missing;

        if (length > ItemImage.MaxBytes)
        {
            return Failed(new AppError(TooLarge, Field, new Dictionary<string, object> { ["maxMb"] = ItemImage.MaxBytes / (1024 * 1024) }));
        }

        using var buffer = new MemoryStream((int)length);
        await content.CopyToAsync(buffer, ct);
        var data = buffer.ToArray();
        if (ItemImage.DetectContentType(data) is null) return Failed(new AppError(Unsupported, Field));

        var now = timeProvider.GetUtcNow();
        var image = await db.ItemImages.SingleOrDefaultAsync(i => i.ItemId == itemId, ct);
        if (image is null)
        {
            db.ItemImages.Add(new ItemImage(item, data, now));
        }
        else
        {
            image.Replace(data, now);
        }

        await db.SaveChangesAsync(ct);
        return new SaveItemResult(await items.GetAsync(itemId, ct), []);
    }

    /// <summary>Removes the picture the user added; false when there is no such item.</summary>
    public async Task<bool> RemoveAsync(Guid itemId, CancellationToken ct)
    {
        if (!await db.Items.AnyAsync(i => i.Id == itemId, ct)) return false;

        await db.ItemImages.Where(i => i.ItemId == itemId).ExecuteDeleteAsync(ct);
        return true;
    }

    /// <summary>The picture the user added; null when the item has none or doesn't exist.</summary>
    public Task<ItemImageFile?> GetAsync(Guid itemId, CancellationToken ct) =>
        db.ItemImages.AsNoTracking()
            .Where(i => i.ItemId == itemId)
            .Select(i => new ItemImageFile(i.ContentType, i.Data, i.UpdatedAt))
            .SingleOrDefaultAsync(ct);

    private static SaveItemResult Failed(AppError error) => new(null, [error]);
}
