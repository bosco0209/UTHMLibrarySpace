using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Infrastructure;

namespace UTHMLibrarySpace.ViewComponents;

public class AnnouncementBannerViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public AnnouncementBannerViewComponent(AppDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await _db.Announcements.Where(a => a.IsActive).OrderByDescending(a => a.CreatedAt).Take(5).ToListAsync());
}

public class NotificationBellViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public NotificationBellViewComponent(AppDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var uid = UserClaimsPrincipal.CurrentUserId();
        if (uid is null) return Content("");
        var items = await _db.Notifications.Where(n => n.UserId == uid).OrderByDescending(n => n.CreatedAt).Take(8).ToListAsync();
        ViewBag.Unread = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead);
        return View(items);
    }
}
