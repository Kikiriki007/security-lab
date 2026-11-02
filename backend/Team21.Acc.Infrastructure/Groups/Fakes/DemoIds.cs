namespace Team21.Acc.Infrastructure.Groups.Fakes;

/// <summary>
/// Fixed ids of the Development demo data. Readable on purpose, used as X-Test-User values.
/// </summary>
public static class DemoIds
{
    public static readonly Guid Course = Guid.Parse("00000000-0000-0000-0000-00000000c001");

    public static readonly Guid Teacher = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Assistant = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid StudentA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    public static readonly Guid StudentB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    public static readonly Guid BlockedStudent = Guid.Parse("00000000-0000-0000-0000-0000000000bb");
    public static readonly Guid Outsider = Guid.Parse("00000000-0000-0000-0000-0000000000ff");
}
