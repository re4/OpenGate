using Microsoft.AspNetCore.Identity;
using OpenGate.Domain.Entities;

namespace OpenGate.Infrastructure.Identity;

public class BcryptPasswordHasher : IPasswordHasher<ApplicationUser>
{
    private readonly PasswordHasher<ApplicationUser> _defaultHasher = new();

    public string HashPassword(ApplicationUser user, string password)
    {
        return _defaultHasher.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (IsBcryptHash(hashedPassword))
        {
            var normalised = hashedPassword.Replace("$2y$", "$2a$");
            if (BCrypt.Net.BCrypt.Verify(providedPassword, normalised))
                return PasswordVerificationResult.SuccessRehashNeeded;

            return PasswordVerificationResult.Failed;
        }

        return _defaultHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }

    private static bool IsBcryptHash(string hash) =>
        hash.StartsWith("$2y$") || hash.StartsWith("$2b$") || hash.StartsWith("$2a$");
}
