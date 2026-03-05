using OpenGate.Application.Interfaces;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Web.Services;

public class FileUploadService(IWebHostEnvironment env, IClamAvService clamAv, ISettingRepository settingRepo)
{
    private static readonly HashSet<string> AllowedTicketExtensions =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg",
        ".mp4", ".webm", ".mov", ".avi",
        ".txt", ".log"
    ];

    private static readonly HashSet<string> AllowedImageExtensions =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg", ".ico"
    ];

    private const long MaxImageFileSize = 2 * 1024 * 1024; // 2 MB (logos only)

    public record UploadResult(bool Success, string? Url = null, string? Error = null);

    private async Task<long> GetMaxUploadBytesAsync()
    {
        var setting = await settingRepo.GetByKeyAsync("MaxUploadSizeGB");
        if (setting != null && decimal.TryParse(setting.Value, out var gb) && gb > 0)
            return (long)(gb * 1024 * 1024 * 1024);
        return 1L * 1024 * 1024 * 1024; // default 1 GB
    }

    public async Task<UploadResult> UploadTicketAttachmentAsync(Stream fileStream, string fileName, long fileSize, string ticketId)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (!AllowedTicketExtensions.Contains(ext))
            return new UploadResult(false, Error: $"File type '{ext}' is not allowed. Allowed: images, videos, .txt, .log");

        var maxBytes = await GetMaxUploadBytesAsync();
        if (fileSize > maxBytes)
        {
            var limitDisplay = maxBytes >= 1024 * 1024 * 1024
                ? $"{maxBytes / (1024.0 * 1024 * 1024):G3} GB"
                : $"{maxBytes / (1024.0 * 1024):G3} MB";
            return new UploadResult(false, Error: $"File exceeds the {limitDisplay} limit.");
        }

        var scanResult = await ScanStreamAsync(fileStream);
        if (!scanResult.IsClean)
            return new UploadResult(false, Error: $"File rejected by antivirus: {scanResult.Threat}");

        var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "tickets", ticketId);
        Directory.CreateDirectory(uploadsDir);

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, safeFileName);

        fileStream.Position = 0;
        await using var fs = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(fs);

        return new UploadResult(true, Url: $"/uploads/tickets/{ticketId}/{safeFileName}");
    }

    public async Task<UploadResult> UploadLogoAsync(Stream fileStream, string fileName, long fileSize)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (!AllowedImageExtensions.Contains(ext))
            return new UploadResult(false, Error: $"File type '{ext}' is not allowed.");

        if (fileSize > MaxImageFileSize)
            return new UploadResult(false, Error: "File exceeds the 2 MB limit.");

        var scanResult = await ScanStreamAsync(fileStream);
        if (!scanResult.IsClean)
            return new UploadResult(false, Error: $"File rejected by antivirus: {scanResult.Threat}");

        var uploadsDir = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        foreach (var old in Directory.EnumerateFiles(uploadsDir, "logo.*"))
        {
            try { File.Delete(old); } catch { }
        }

        var safeFileName = $"logo{ext}";
        var filePath = Path.Combine(uploadsDir, safeFileName);

        fileStream.Position = 0;
        await using var fs = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(fs);

        return new UploadResult(true, Url: $"/uploads/{safeFileName}");
    }

    public async Task<long> GetMaxUploadSizeForClientAsync() => await GetMaxUploadBytesAsync();

    private async Task<ScanResult> ScanStreamAsync(Stream stream)
    {
        if (!clamAv.IsEnabled) return ScanResult.Skip();

        stream.Position = 0;
        var result = await clamAv.ScanAsync(stream);
        stream.Position = 0;
        return result;
    }
}
