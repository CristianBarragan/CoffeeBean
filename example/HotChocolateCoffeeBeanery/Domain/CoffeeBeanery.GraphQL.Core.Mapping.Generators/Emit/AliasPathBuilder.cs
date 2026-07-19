using System.Security.Cryptography;
using System.Text;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;

internal static class AliasPathBuilder
{
    private const char Separator = '_';
    private const int MaxIdentifierLength = 63; // PostgreSQL identifier limit

    public static string Build(string parent, string current, int? index = null)
    {
        var segment = index is { } i
            ? $"{current}{Separator}{i}"
            : current;

        var combined = string.IsNullOrEmpty(parent)
            ? segment
            : $"{parent}{Separator}{segment}";

        return Truncate(combined);
    }

    private static string Truncate(string alias)
    {
        if (alias.Length <= MaxIdentifierLength)
            return alias;

        byte[] hash;
        using (var md5 = MD5.Create())
        {
            hash = md5.ComputeHash(Encoding.UTF8.GetBytes(alias));
        }

        // First 4 bytes (8 hex characters) are sufficient for the suffix.
        var sb = new StringBuilder(8);
        for (var i = 0; i < 4; i++)
        {
            sb.Append(hash[i].ToString("x2"));
        }

        var suffix = sb.ToString();
        var keepLength = MaxIdentifierLength - suffix.Length - 1;

        return $"{alias.Substring(0, keepLength)}{Separator}{suffix}";
    }
}