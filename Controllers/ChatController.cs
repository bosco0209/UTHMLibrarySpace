using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using UTHMLibrarySpace.Infrastructure;
using UTHMLibrarySpace.Models;
using UTHMLibrarySpace.Services;

namespace UTHMLibrarySpace.Controllers;

/// <summary>JSON endpoint used by the floating chatbot widget.</summary>
public class ChatController : Controller
{
    private readonly GeminiChatService _chat;
    public ChatController(GeminiChatService chat) => _chat = chat;

    [HttpPost, IgnoreAntiforgeryToken]
    public async Task<IActionResult> Ask([FromBody] ChatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Length > 2000)
            return BadRequest(new { error = "Message must be 1–2000 characters." });

        var (reply, source) = await _chat.AskAsync(req.Message.Trim(), req.History ?? new(), CurrentUser());
        return Json(new { reply, source });
    }

    /// <summary>So the assistant can answer per role, e.g. "you cannot book rooms" for a pure admin.</summary>
    private ChatUserContext CurrentUser()
    {
        if (User.Identity?.IsAuthenticated != true) return ChatUserContext.Anonymous;

        return new ChatUserContext(
            Name: User.FindFirstValue(ClaimTypes.Name),
            Role: User.FindFirstValue(ClaimTypes.Role),
            BookingRole: User.FindFirstValue(UserClaims.BookingRole),
            IsAuthenticated: true);
    }
}