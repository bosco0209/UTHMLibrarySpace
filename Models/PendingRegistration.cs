using System.ComponentModel.DataAnnotations;

namespace UTHMLibrarySpace.Models;

/// <summary>A registration waiting for the emailed 6-digit verification code.</summary>
public class PendingRegistration
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Email { get; set; } = "";

    [Required, MaxLength(6)]
    public string Code { get; set; } = "";

    public DateTime ExpiresAt { get; set; }

    // Snapshot of the submitted form (so we can create the real User later)
    public string Name { get; set; } = "";
    public string IdNumber { get; set; } = "";
    public string Role { get; set; } = Roles.Student;
    public string Faculty { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";

    public int Attempts { get; set; }
}