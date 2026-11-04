using System.Text.RegularExpressions;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Format check for identifiers that reach an audit record (actor, correlation id, resource type and id).
/// The raw value is validated, never cleaned and then accepted: cleaning could turn one id into another
/// (an id with a zero-width character into the admin's id) or merge two ids by cutting them to the same
/// prefix. Anything outside the format is the caller's cue to record <see cref="AccessAuditRecord.Invalid"/>.
/// </summary>
/// <remarks>
/// ASCII letters and digits first, then letters, digits and <c>_ . : @ -</c>. That covers GUIDs, ASP.NET Core
/// trace ids and the ids used in tests, and excludes whitespace, <c>=</c>, quotes, control, format and
/// surrogate characters, so no value can start a second <c>key=value</c> pair in a rendered log line.
/// </remarks>
internal static partial class AuditIdentifier
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.:@-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static bool IsValid(string? value, int maxLength) =>
        !string.IsNullOrEmpty(value) && value.Length <= maxLength && Pattern().IsMatch(value);
}
