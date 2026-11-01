using System.Security.Cryptography;
using System.Text;

namespace Relifes.Api.Auth;

public static class ApiKey
{
    private const string Prefix = "rl_";

    public static string Generate() =>
        Prefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    // Keys are 256 bits of randomness, so a plain SHA-256 is safe and lets us look a key up by its hash.
    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
