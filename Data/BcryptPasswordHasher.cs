using Microsoft.AspNetCore.Identity;

namespace BocconiLMS.Data;

public class BcryptPasswordHasher : IPasswordHasher<ApplicationUser>
{
    public string HashPassword(ApplicationUser user, string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

    public PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword)) return PasswordVerificationResult.Failed;
        bool valid = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
        return valid ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
    }
}
