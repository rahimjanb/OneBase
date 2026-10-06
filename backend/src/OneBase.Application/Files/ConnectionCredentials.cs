using System.Security.Cryptography;
using System.Text;

namespace OneBase.Application.Files;

/// <summary>
/// Логин и пароль подключения отдела к Windows. Пароль — из криптографического генератора, хранится хэшем PBKDF2-SHA256
/// (проверка входа) и, отдельно, зашифрованным Data Protection (повторный показ). Открытый пароль нигде не пишется.
/// </summary>
public static class ConnectionCredentials
{
    public const int PasswordLength = 20;
    private const int Iterations = 100_000;
    private const string Scheme = "pbkdf2-sha256";

    // Без похожих символов (0/O, 1/l/I): пароль иногда переписывают руками.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    /// <summary>«production_8f3a1»: код отдела латиницей и 5 случайных шестнадцатеричных знаков.</summary>
    public static string NewUsername(string departmentCode)
    {
        var code = new string(departmentCode.ToLowerInvariant().Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9').ToArray());
        return $"{(code.Length == 0 ? "dept" : code)}_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3))[..5]}";
    }

    public static string NewPassword() => RandomNumberGenerator.GetString(Alphabet, PasswordLength);

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string stored, string password)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations) || iterations < 10_000)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Отпечаток пары логин/пароль для кэша проверенных входов (сам пароль в памяти кэша не держится).</summary>
    public static string Fingerprint(string username, string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{username}\n{password}")));
}
