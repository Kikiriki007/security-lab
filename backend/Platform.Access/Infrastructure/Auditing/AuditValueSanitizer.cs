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

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            if (char.IsHighSurrogate(ch) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                // A valid pair is judged as one code point (tag characters are invisible and sit outside the BMP)
                // and is never cut in half by the length limit.
                if (!IsUnsafe(CharUnicodeInfo.GetUnicodeCategory(char.ConvertToUtf32(ch, value[i + 1]))))
                {
                    if (builder.Length + 2 > maxLength)
                    {
                        break;
                    }

                    builder.Append(ch).Append(value[i + 1]);
                }

                i++;
                continue;
            }

            // A lone surrogate is not valid text: some sinks replace it, others reject the whole record.
            if (char.IsSurrogate(ch) || char.IsControl(ch) || IsUnsafe(CharUnicodeInfo.GetUnicodeCategory(ch)))
            {
                continue;
            }

            if (builder.Length == maxLength)
            {
                break;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static bool IsUnsafe(UnicodeCategory category) =>
        category is
            UnicodeCategory.Control or
            UnicodeCategory.LineSeparator or
            UnicodeCategory.ParagraphSeparator or
            UnicodeCategory.Format;
}
