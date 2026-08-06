using System.Security.Cryptography;
using System.Text;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

/// <summary>
/// Computes RFC 6238 TOTP codes (HMAC-SHA1, 30-second step, 6 digits) — the same
/// algorithm ASP.NET Core Identity's built-in authenticator provider uses, so a test
/// can act as the authenticator app given the raw shared key returned by
/// <c>enableauthenticator</c>.
/// </summary>
public static class TotpCodeGenerator
{
    public static string GenerateCode(string base32Secret)
    {
        var keyBytes = Base32Decode(base32Secret.Replace(" ", "").ToUpperInvariant());
        var timestep = (long)(DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch).TotalSeconds / 30;
        return ComputeTotp(keyBytes, timestep);
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.TrimEnd('=');

        var bits = new StringBuilder();
        foreach (var c in input)
        {
            var index = alphabet.IndexOf(c);
            if (index < 0)
            {
                continue;
            }
            bits.Append(Convert.ToString(index, 2).PadLeft(5, '0'));
        }

        var byteCount = bits.Length / 8;
        var bytes = new byte[byteCount];
        for (var i = 0; i < byteCount; i++)
        {
            bytes[i] = Convert.ToByte(bits.ToString(i * 8, 8), 2);
        }

        return bytes;
    }

    private static string ComputeTotp(byte[] key, long timestep)
    {
        var timestepBytes = BitConverter.GetBytes(timestep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestepBytes);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(timestepBytes);

        var offset = hash[^1] & 0xf;
        var binaryCode = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        return (binaryCode % 1000000).ToString("D6");
    }
}
