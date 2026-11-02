using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Infrastructure;
using UTHMLibrarySpace.Models;

namespace UTHMLibrarySpace.Controllers;

/// <summary>Students and lecturers report broken facilities or ask questions, with an optional photo.</summary>
[Authorize]
public class FeedbackController : Controller
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    private readonly AppDbContext _db;
    private readonly string _dir;

    public FeedbackController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _dir = Path.Combine(env.ContentRootPath, "App_Data", "feedback");
    }

    // ---------- Submit page (also shows the user's own feedback history) ----------
    [Authorize(Policy = AuthPolicies.CanBook)]
    public async Task<IActionResult> Index(int? facilityId) =>
        View(await PageAsync(new FeedbackFormVm { FacilityId = facilityId }));

    // ---------- Submit (POST) ----------
    [Authorize(Policy = AuthPolicies.CanBook)]
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Submit([Bind(Prefix = "Form")] FeedbackFormVm vm, IFormFile? image)
    {
        if (vm.FacilityId is { } fid && !await _db.Facilities.AnyAsync(f => f.Id == fid))
            ModelState.AddModelError("Form.FacilityId", "Please choose a valid facility.");

        string? ext = null;
        if (image is { Length: > 0 })
        {
            if (image.Length > MaxImageBytes)
                ModelState.AddModelError("image", "Photo is too large. The maximum size is 5 MB.");
            else
            {
                var head = new byte[12];
                await using (var s = image.OpenReadStream()) _ = await s.ReadAsync(head);
                ext = head[0] == 0xFF && head[1] == 0xD8 ? ".jpg"
                    : head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 ? ".png"
                    : head[0] == 0x52 && head[1] == 0x49 && head[8] == 0x57 && head[9] == 0x45 ? ".webp" : null;
                if (ext is null)
                    ModelState.AddModelError("image", "Photo must be a JPG, PNG or WebP image.");
            }
        }

        if (!ModelState.IsValid)
            return View(nameof(Index), await PageAsync(vm));

        var uid = User.CurrentUserId()!.Value;
        var fb = new Feedback
        {
            UserId = uid,
            FacilityId = vm.FacilityId,
            Kind = vm.Kind,
            Subject = (vm.Subject ?? "").Trim(),
            Description = (vm.Description ?? "").Trim(),
        };

        if (ext is not null)
        {
            Directory.CreateDirectory(_dir);
            fb.ImageFileName = $"fb-u{uid}-{Guid.NewGuid():N}{ext}";
            await using var outStream = System.IO.File.Create(Path.Combine(_dir, fb.ImageFileName));
            await image!.CopyToAsync(outStream);
        }

        _db.Feedbacks.Add(fb);
        await _db.SaveChangesAsync();
        this.Toast("Thank you! Your feedback was sent to the PTTA team.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Photo (author or admin only) ----------
    public async Task<IActionResult> Image(int id)
    {
        var fb = await _db.Feedbacks.FindAsync(id);
        if (fb?.ImageFileName is null ||
            (fb.UserId != User.CurrentUserId() && !User.IsInRole(Roles.Admin)))
            return NotFound();

        var path = Path.Combine(_dir, fb.ImageFileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var type = Path.GetExtension(path) switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        return PhysicalFile(path, type, enableRangeProcessing: true);
    }

    private async Task<FeedbackPageVm> PageAsync(FeedbackFormVm form)
    {
        var uid = User.CurrentUserId()!.Value;
        return new FeedbackPageVm
        {
            Form = form,
            Items = await _db.Feedbacks
                .Include(f => f.Facility)
                .Where(f => f.UserId == uid)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync(),
            Facilities = await _db.Facilities
                .OrderBy(f => f.Type).ThenBy(f => f.Name)
                .ToListAsync(),
        };
    }
}