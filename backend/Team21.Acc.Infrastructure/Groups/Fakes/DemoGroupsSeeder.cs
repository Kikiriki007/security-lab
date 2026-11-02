using Microsoft.EntityFrameworkCore;
using Team21.Acc.Core.Groups;
using Team21.Acc.Infrastructure.Common;
using Team21.Acc.Infrastructure.Groups.Data;

namespace Team21.Acc.Infrastructure.Groups.Fakes;

/// <summary>
/// Development only. Groups A and B in the demo course, StudentA in A, StudentB in B, one assignment
/// restricted to A and one open to the whole course. Runs once, a removed membership stays removed.
/// </summary>
public class DemoGroupsSeeder
{
    private readonly IGroupsDbContext _context;

    public DemoGroupsSeeder(IGroupsDbContext context)
    {
        _context = context;
    }

    /// <summary>Returns false when the demo data was already there.</summary>
    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        // Deleted rows count too, so a demo removal is not undone on restart
        var seeded = await _context.Groups
            .IgnoreQueryFilters([TeamDbContextBase.SoftDeleteFilter])
            .AnyAsync(g => g.Id == DemoIds.GroupA, ct);

        if (seeded)
            return false;

        var groupA = NewGroup(DemoIds.GroupA, "A");
        var groupB = NewGroup(DemoIds.GroupB, "B");

        var restricted = NewAudience(DemoIds.RestrictedToA, MaterialVisibility.RestrictedToGroups);
        var courseWide = NewAudience(DemoIds.CourseWide, MaterialVisibility.CourseWide);

        _context.Groups.AddRange(groupA, groupB);
        _context.GroupMembers.AddRange(
            NewMember(groupA.Id, DemoIds.StudentA),
            NewMember(groupB.Id, DemoIds.StudentB));
        _context.MaterialAudiences.AddRange(restricted, courseWide);
        _context.GroupMaterialAccesses.Add(new GroupMaterialAccess
        {
            MaterialAudienceId = restricted.Id,
            GroupId = groupA.Id,
            CreatedBy = DemoIds.Teacher
        });

        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static Group NewGroup(Guid id, string name) => new()
    {
        Id = id,
        CourseId = DemoIds.Course,
        Name = name,
        CreatedBy = DemoIds.Teacher
    };

    private static GroupMember NewMember(Guid groupId, Guid userId) => new()
    {
        GroupId = groupId,
        UserId = userId,
        CreatedBy = DemoIds.Teacher
    };

    private static MaterialAudience NewAudience(Guid materialId, MaterialVisibility visibility) => new()
    {
        CourseId = DemoIds.Course,
        MaterialType = MaterialType.Assignment,
        MaterialId = materialId,
        Visibility = visibility,
        CreatedBy = DemoIds.Teacher
    };
}
