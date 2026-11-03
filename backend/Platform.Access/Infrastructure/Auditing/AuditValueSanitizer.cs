using System.Globalization;
using System.Text;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Makes a value safe for an audit record (AUD-03, threat T11 log injection):
/// removes control characters (CR, LF, NUL, C1 controls), Unicode line and paragraph
/// separators and invisible format characters (for example a right-to-left override),
/// then cuts the value to a fixed length without splitting a surrogate pair.
/// </summary>
internal static class AuditValueSanitizer
{
    public static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));

        foreach (var ch in value)
        {
            if (IsUnsafe(ch))
            {
                continue;
            }

            if (builder.Length == maxLength)
            {
                break;
            }

            builder.Append(ch);
        }

        if (builder.Length > 0 && char.IsHighSurrogate(builder[^1]))
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    private static bool IsUnsafe(char ch) =>
        char.IsControl(ch) ||
        CharUnicodeInfo.GetUnicodeCategory(ch) is
            UnicodeCategory.LineSeparator or
            UnicodeCategory.ParagraphSeparator or
            UnicodeCategory.Format;
}
