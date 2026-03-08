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

    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        [".png"] = [new byte[] { 0x89, 0x50, 0x4E, 0x47 }],
        [".jpg"] = [new byte[] { 0xFF, 0xD8, 0xFF }],
        [".jpeg"] = [new byte[] { 0xFF, 0xD8, 0xFF }],
        [".gif"] = [new byte[] { 0x47, 0x49, 0x46, 0x38 }],
        [".webp"] = [new byte[] { 0x52, 0x49, 0x46, 0x46 }],
        [".bmp"] = [new byte[] { 0x42, 0x4D }],
        [".ico"] = [new byte[] { 0x00, 0x00, 0x01, 0x00 }, new byte[] { 0x00, 0x00, 0x02, 0x00 }],
        [".mp4"] = [new byte[] { 0x00, 0x00, 0x00 }],  // ftyp box (4th byte varies)
        [".webm"] = [new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }],
        [".mov"] = [new byte[] { 0x00, 0x00, 0x00 }],
        [".avi"] = [new byte[] { 0x52, 0x49, 0x46, 0x46 }],
    };

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
        if (string.IsNullOrEmpty(ticketId) || !ticketId.All(c => char.IsLetterOrDigit(c) || c == '-'))
            return new UploadResult(false, Error: "Invalid ticket ID.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (!AllowedTicketExtensions.Contains(ext))
            return new UploadResult(false, Error: $"File type '{ext}' is not allowed. Allowed: images, videos, .txt, .log");

        if (!ValidateFileSignature(fileStream, ext))
            return new UploadResult(false, Error: $"File content does not match the '{ext}' file type.");

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

        var baseDir = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads", "tickets"));
        var uploadsDir = Path.GetFullPath(Path.Combine(baseDir, ticketId));
        if (!uploadsDir.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            return new UploadResult(false, Error: "Invalid ticket ID.");

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

        if (!ValidateFileSignature(fileStream, ext))
            return new UploadResult(false, Error: $"File content does not match the '{ext}' file type.");

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

    private static bool ValidateFileSignature(Stream stream, string extension)
    {
        if (!MagicBytes.TryGetValue(extension, out var signatures))
            return true; // .txt, .log, .svg — no binary signature to check

        if (stream.Length == 0) return false;

        stream.Position = 0;
        var headerSize = signatures.Max(s => s.Length);
        var header = new byte[headerSize];
        var bytesRead = stream.Read(header, 0, headerSize);
        stream.Position = 0;

        if (bytesRead == 0) return false;

        return signatures.Any(sig =>
            bytesRead >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig));
    }

    private async Task<ScanResult> ScanStreamAsync(Stream stream)
    {
        if (!clamAv.IsEnabled) return ScanResult.Skip();

        stream.Position = 0;
        var result = await clamAv.ScanAsync(stream);
        stream.Position = 0;
        return result;
    }
}
