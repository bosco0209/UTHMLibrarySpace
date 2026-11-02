namespace UTHMLibrarySpace.Models;

/// <summary>A password reset waiting for the emailed 6-digit verification code.</summary>
public class PasswordResetRequest
{
    public int Id { get; set; }

    /// <summary>Matric / staff / email used to identify the account.</summary>
    public string Email { get; set; } = "";

    public string Code { get; set; } = "";

    public DateTime ExpiresAt { get; set; }

    public int Attempts { get; set; }

    /// <summary>True once the code was correct — the user may then set a new password.</summary>
    public bool Verified { get; set; }
}
