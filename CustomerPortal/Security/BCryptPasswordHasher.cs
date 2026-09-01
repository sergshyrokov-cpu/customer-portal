namespace CustomerPortal.Security;

/// <summary>
/// SC-1: BCrypt via BCrypt.Net-Next, default work factor. Not the built-in
/// ASP.NET Core Identity PBKDF2 hasher (security-conventions.md SC-1).
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
