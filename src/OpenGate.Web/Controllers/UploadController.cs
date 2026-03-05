using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGate.Domain.Interfaces;
using OpenGate.Web.Services;

namespace OpenGate.Web.Controllers;

[Route("api/upload")]
[ApiController]
[Authorize(Roles = "Admin")]
public class UploadController(IWebHostEnvironment env, ISettingRepository settingRepo, BrandingProvider brandingProvider, FileUploadService fileUploadService) : ControllerBase
{
    [HttpPost("logo")]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        var result = await fileUploadService.UploadLogoAsync(ms, file.FileName, file.Length);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        var setting = await settingRepo.GetByKeyAsync("LogoUrl");
        if (setting != null)
        {
            setting.Value = result.Url!;
            setting.UpdatedAt = DateTime.UtcNow;
            await settingRepo.UpdateAsync(setting);
        }

        brandingProvider.InvalidateCache();
        return Ok(new { url = result.Url });
    }

    [HttpDelete("logo")]
    public async Task<IActionResult> RemoveLogo()
    {
        var uploadsDir = Path.Combine(env.WebRootPath, "uploads");
        if (Directory.Exists(uploadsDir))
        {
            foreach (var old in Directory.EnumerateFiles(uploadsDir, "logo.*"))
            {
                try { System.IO.File.Delete(old); } catch { }
            }
        }

        var setting = await settingRepo.GetByKeyAsync("LogoUrl");
        if (setting != null)
        {
            setting.Value = "";
            setting.UpdatedAt = DateTime.UtcNow;
            await settingRepo.UpdateAsync(setting);
        }

        brandingProvider.InvalidateCache();
        return Ok(new { url = "" });
    }
}
