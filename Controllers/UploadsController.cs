using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers;

[Route("api/uploads")]
[ApiController]
[Authorize]
public class UploadsController : ControllerBase
{
    private static readonly HashSet<string> HorseExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly HashSet<string> JockeyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".pdf"
    };

    private const long MaxFileSize = 10 * 1024 * 1024;
    private readonly IWebHostEnvironment _env;

    public UploadsController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? category)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "File không được để trống."
            });
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest(new
            {
                message = "Dung lượng file tối đa là 10MB."
            });
        }

        var normalizedCategory = category?.Trim().ToLowerInvariant();
        if (normalizedCategory != "horses" && normalizedCategory != "jockeys")
        {
            return BadRequest(new
            {
                message = "Category không hợp lệ."
            });
        }

        var extension = Path.GetExtension(file.FileName);
        var allowedExtensions = normalizedCategory == "horses" ? HorseExtensions : JockeyExtensions;

        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                message = "Định dạng file không hợp lệ."
            });
        }

        var webRootPath = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var uploadFolder = Path.Combine(webRootPath, "uploads", normalizedCategory);
        Directory.CreateDirectory(uploadFolder);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"/uploads/{normalizedCategory}/{fileName}";
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{url}";

        return Ok(new
        {
            message = "Upload file thành công.",
            url,
            absoluteUrl,
            fileName,
            contentType = file.ContentType,
            size = file.Length
        });
    }
}
