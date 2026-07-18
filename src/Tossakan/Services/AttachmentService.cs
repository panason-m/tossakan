using Microsoft.EntityFrameworkCore;
using Tossakan.Data;
using Tossakan.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Tossakan.Services;

public class AttachmentService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AttachmentService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    /// <summary>Cheap synchronous check so callers can decide whether to swallow a Paste event
    /// before doing any async work (the event must be marked Handled before the first await).</summary>
    public static bool ClipboardHasImage() => Clipboard.GetContent().Contains(StandardDataFormats.Bitmap);

    /// <summary>Saves the clipboard's bitmap as a PNG attachment on the card, or returns null if the
    /// clipboard has no image. Decodes and re-encodes via SoftwareBitmap so the file is a valid PNG
    /// regardless of the clipboard source's native format.</summary>
    public async Task<Attachment?> AddClipboardImageAsync(int cardId)
    {
        var view = Clipboard.GetContent();
        if (!view.Contains(StandardDataFormats.Bitmap)) return null;

        var streamRef = await view.GetBitmapAsync();
        using var sourceStream = await streamRef.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(sourceStream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        AppPaths.EnsureFolders();
        var fileName = $"pasted-image-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        var storedPath = Path.Combine(AppPaths.AttachmentsFolder, $"{Guid.NewGuid():N}_{fileName}");

        var folder = await StorageFolder.GetFolderFromPathAsync(AppPaths.AttachmentsFolder);
        var file = await folder.CreateFileAsync(Path.GetFileName(storedPath), CreationCollisionOption.FailIfExists);
        using (var writeStream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, writeStream);
            encoder.SetSoftwareBitmap(softwareBitmap);
            await encoder.FlushAsync();
        }

        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId)
            ?? throw new InvalidOperationException($"Card {cardId} not found");
        var attachment = new Attachment
        {
            CardId = cardId,
            FileName = fileName,
            StoredPath = storedPath,
            AddedAt = DateTime.Now,
        };
        db.Attachments.Add(attachment);
        db.Activities.Add(new ActivityEntry
        {
            BoardId = card.List.BoardId,
            CardId = cardId,
            Description = $"Image pasted as attachment \"{fileName}\" on \"{card.Title}\"",
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();
        return attachment;
    }

    /// <summary>Copies the source file into the app's attachments folder and records it on the card.</summary>
    public async Task<Attachment> AddAttachmentAsync(int cardId, string sourceFilePath)
    {
        AppPaths.EnsureFolders();
        var fileName = Path.GetFileName(sourceFilePath);
        var storedPath = Path.Combine(AppPaths.AttachmentsFolder, $"{Guid.NewGuid():N}_{fileName}");
        await Task.Run(() => File.Copy(sourceFilePath, storedPath));

        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId)
            ?? throw new InvalidOperationException($"Card {cardId} not found");
        var attachment = new Attachment
        {
            CardId = cardId,
            FileName = fileName,
            StoredPath = storedPath,
            AddedAt = DateTime.Now,
        };
        db.Attachments.Add(attachment);
        db.Activities.Add(new ActivityEntry
        {
            BoardId = card.List.BoardId,
            CardId = cardId,
            Description = $"Attachment \"{fileName}\" added to \"{card.Title}\"",
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();
        return attachment;
    }

    public async Task RemoveAttachmentAsync(int attachmentId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var attachment = await db.Attachments.FindAsync(attachmentId);
        if (attachment is null) return;
        var path = attachment.StoredPath;
        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync();
        DeleteFiles(new[] { path });
    }

    /// <summary>Best-effort removal of stored attachment files (records are already gone).</summary>
    public static void DeleteFiles(IEnumerable<string> storedPaths)
    {
        foreach (var path in storedPaths)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
