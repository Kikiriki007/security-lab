using Platform.Access.Infrastructure.Auditing;

namespace Platform.Access.Tests.Auditing;

[TestFixture]
public sealed class AuditValueSanitizerTests
{
    [TestCase("abc\r\ndef", "abcdef")]
    [TestCase("a\0b", "ab")]
    [TestCase("a\tb\u001bc", "abc")]
    [TestCase("a\u0085b", "ab")]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_ControlAndFormatCharacters_AreRemoved(string raw, string expected)
    {
        Assert.That(AuditValueSanitizer.Clean(raw, 64), Is.EqualTo(expected));
    }

    // Line/paragraph separators, a right-to-left override and a zero-width space,
    // built from code points so the source file stays plain ASCII.
    private static IEnumerable<TestCaseData> UnicodeSeparatorCases()
    {
        yield return new TestCaseData("a" + (char)0x2028 + "b" + (char)0x2029 + "c", "abc");
        yield return new TestCaseData("user" + (char)0x202E + "evil", "userevil");
        yield return new TestCaseData("a" + (char)0x200B + "b", "ab");
    }

    [TestCaseSource(nameof(UnicodeSeparatorCases))]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_UnicodeSeparatorsAndFormatCharacters_AreRemoved(string raw, string expected)
    {
        Assert.That(AuditValueSanitizer.Clean(raw, 64), Is.EqualTo(expected));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_LongValue_IsTruncatedToLimit()
    {
        var raw = new string('a', 10_000);

        Assert.That(AuditValueSanitizer.Clean(raw, 64), Has.Length.EqualTo(64));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Truncation_NeverLeavesLoneSurrogate()
    {
        // 63 ASCII characters followed by an emoji (two UTF-16 code units).
        var raw = new string('a', 63) + "\U0001F600";

        var cleaned = AuditValueSanitizer.Clean(raw, 64);

        Assert.That(cleaned, Is.EqualTo(new string('a', 63)));
        Assert.That(char.IsHighSurrogate(cleaned[^1]), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_NullOrEmpty_GivesEmptyString(string? raw)
    {
        Assert.That(AuditValueSanitizer.Clean(raw, 64), Is.Empty);
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_PlainValue_IsUnchanged()
    {
        Assert.That(AuditValueSanitizer.Clean("attempt:42", 64), Is.EqualTo("attempt:42"));
    }
}
