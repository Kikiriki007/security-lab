using Team21.Acc.Core.Groups.Interfaces;
using Microsoft.EntityFrameworkCore;
using Team21.Acc.Core.Common;
using Team21.Acc.Core.Groups;
using Team21.Acc.Core.Groups.Dtos;
using Team21.Acc.Core.Groups.Visibility;
using Team21.Acc.Core.Groups.Visibility.Interfaces;
using Team21.Acc.Core.Interfaces;
using Team21.Acc.Infrastructure.Groups.Data;

namespace Team21.Acc.Infrastructure.Groups.Services;

public class GroupService : IGroupService
{
    private readonly IGroupsDbContext _context;
    private readonly ICurrentActor _actor;
    private readonly ICourseMembershipReader _membershipReader;
    private readonly IAccountStatusReader _accountStatusReader;


    public GroupService(
        IGroupsDbContext context,
        ICurrentActor actor,
        ICourseMembershipReader membershipReader,
        IAccountStatusReader accountStatusReader)
    {
        _context = context;
        _actor = actor;
        _membershipReader = membershipReader;
        _accountStatusReader = accountStatusReader;
    }


    public async Task<Result<IReadOnlyList<GroupDto>>> GetGroupsAsync(
        Guid courseId,
        CancellationToken ct = default)
    {
        // Provera nastavnickih prava
        var accessError = await CheckTeacherAccessAsync(courseId, ct);

        if (accessError is not null)
            return accessError;

        // Ucitavanje grupa iz odredjenog kursa
        var groups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CourseId == courseId)
            .OrderBy(g => g.Name)
            .Select(g => new GroupDto(
                g.Id,
                g.CourseId,
                g.Name,
                g.Status))
            .ToListAsync(ct);

        IReadOnlyList<GroupDto> result = groups;

        return Result<IReadOnlyList<GroupDto>>.Success(result);
    }


    public async Task<Result<GroupDto>> GetGroupAsync(
        Guid courseId,
        Guid groupId,
        CancellationToken ct = default)
    {
        // 1. Provera nastavnickih prava
        var accessError = await CheckTeacherAccessAsync(courseId, ct);

        if (accessError is not null)
            return accessError;

        // 2. Provera identifikatora grupe
        if (groupId == Guid.Empty)
            return Error.Validation("Invalid group ID.");

        // 3. Pronalazenje grupe u okviru kursa
        var group = await _context.Groups
            .AsNoTracking()
            .Where(g => g.Id == groupId &&
                        g.CourseId == courseId)
            .Select(g => new GroupDto(
                g.Id,
                g.CourseId,
                g.Name,
                g.Status))
            .FirstOrDefaultAsync(ct);

        // 4. Grupa ne postoji u trazenom kursu
        if (group is null)
            return Error.NotFound("Group not found.");

        // 5. Vracanje podataka
        return Result<GroupDto>.Success(group);
    }


    public async Task<Result<IReadOnlyList<GroupMemberDto>>> GetMembersAsync(
        Guid courseId,
        Guid groupId,
        CancellationToken ct = default)
    {
        // 1. Provera nastavnickih prava
        var accessError = await CheckTeacherAccessAsync(courseId, ct);

        if (accessError is not null)
            return accessError;

        // 2. Validacija identifikatora grupe
        if (groupId == Guid.Empty)
            return Error.Validation("Invalid group ID.");

        // 3. Provera da grupa postoji u trazenom kursu
        var groupExists = await _context.Groups
            .AnyAsync(g => g.Id == groupId &&
                           g.CourseId == courseId, ct);

        if (!groupExists)
            return Error.NotFound("Group not found.");

        // 4. Ucitavanje aktivnih clanova grupe
        var members = await _context.GroupMembers
            .AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new GroupMemberDto(
                m.Id,
                m.GroupId,
                m.UserId,
                m.CreatedAt))
            .ToListAsync(ct);

        IReadOnlyList<GroupMemberDto> result = members;

        return Result<IReadOnlyList<GroupMemberDto>>.Success(result);
    }


    public async Task<Result> RemoveMemberAsync(
        Guid courseId,
        Guid groupId,
        Guid userId,
        CancellationToken ct = default)
    {
        // 1. Provera nastavnickih prava
        var accessError = await CheckTeacherAccessAsync(courseId, ct);

        if (accessError is not null)
            return accessError;

        // 2. Validacija identifikatora
        if (groupId == Guid.Empty)
            return Error.Validation("Invalid group ID.");

        if (userId == Guid.Empty)
            return Error.Validation("Invalid user ID.");

        // 3. Provera da grupa pripada trazenom kursu
        var groupExists = await _context.Groups
            .AnyAsync(g => g.Id == groupId &&
                           g.CourseId == courseId, ct);

        if (!groupExists)
            return Error.NotFound("Group not found.");

        // 4. Pronalazenje aktivnog clanstva
        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(
                m => m.GroupId == groupId &&
                     m.UserId == userId,
                ct);

        if (member is null)
            return Error.NotFound("Group member not found.");

        // 5. Soft delete - cuvamo istoriju
        member.IsDeleted = true;
        member.DeletedAt = DateTime.UtcNow;
        member.DeletedBy = _actor.UserId!.Value;
        member.UpdatedBy = _actor.UserId.Value;

        // 6. Cuvanje promene
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }


    public async Task<Result<GroupMemberDto>> AddMemberAsync(
        Guid courseId,
        Guid groupId,
        Guid userId,
        CancellationToken ct = default)
    {
        // 1. Provera nastavnickih prava
        var accessError = await CheckTeacherAccessAsync(courseId, ct);

        if (accessError is not null)
            return accessError;

        // 2. Validacija identifikatora
        if (groupId == Guid.Empty)
            return Error.Validation("Invalid group ID.");

        if (userId == Guid.Empty)
            return Error.Validation("Invalid user ID.");

        // 3. Provera grupe i njenog statusa
        var group = await _context.Groups
            .FirstOrDefaultAsync(g =>
                g.Id == groupId &&
                g.CourseId == courseId, ct);

        if (group is null)
            return Error.NotFound("Group not found.");

        if (group.Status != GroupStatus.Active)
            return Error.Conflict("Group is archived.");

        // 4. Provera upisa studenta na kurs
        var membership = await _membershipReader.FindAsync(
            userId,
            courseId,
            ct);

        if (membership is null ||
            !membership.Active ||
            membership.Role != CourseRole.Student)
        {
            return Error.Validation(
                "User is not an active student in this course.");
        }

        // 5. Provera statusa studentskog naloga
        var accountStatus = await _accountStatusReader
            .GetStatusAsync(userId, ct);

        if (accountStatus != AccountStatus.Active)
            return Error.Validation(
                "Student account is not active.");

        // 6. Provera duplog clanstva
        var alreadyMember = await _context.GroupMembers
            .AnyAsync(m =>
                m.GroupId == groupId &&
                m.UserId == userId, ct);

        if (alreadyMember)
            return Error.Conflict(
                "Student is already a member of this group.");

        // 7. Kreiranje clanstva
        var member = new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            CreatedBy = _actor.UserId!.Value
        };

        _context.GroupMembers.Add(member);

        // 8. Cuvanje u bazi
        await _context.SaveChangesAsync(ct);

        // 9. Vracanje DTO objekta
        return Result<GroupMemberDto>.Success(
            new GroupMemberDto(
                member.Id,
                member.GroupId,
                member.UserId,
                member.CreatedAt));
    }



    public async Task<Result<GroupDto>> CreateGroupAsync(
        Guid courseId,
        string name,
        CancellationToken ct = default)
    {
        // Provera nastavnickih prava
        var accessError = await CheckTeacherAccessAsync(courseId, ct);

        if (accessError is not null)
            return accessError;

        // Validacija naziva grupe
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Group name is required.");

        if (name != name.Trim())
            return Error.Validation(
                "Group name cannot have leading or trailing whitespace.");

        if (name.Length > Group.NameMaxLength)
            return Error.Validation(
                $"Group name cannot exceed {Group.NameMaxLength} characters.");

        if (name.Any(char.IsControl))
            return Error.Validation(
                "Group name cannot contain control characters.");

        // TODO: Integracija sa ACC-01.
        // Proveriti da kurs postoji i da nije arhiviran.
        // Dok ova provera nije dostupna, ne dozvoljavamo upis.
        return Error.Internal(
            "Course status verification is not configured.");
    }

    private async Task<Error?> CheckTeacherAccessAsync(
        Guid courseId,
        CancellationToken ct)
    {
        // Provera prijavljenog korisnika
        if (!_actor.IsAuthenticated || _actor.UserId is null)
            return Error.Unauthorized("Sign in first.");

        // Provera identifikatora kursa
        if (courseId == Guid.Empty)
            return Error.Validation("Invalid course ID.");

        // Provera nastavnicke uloge na konkretnom kursu
        var membership = await _membershipReader.FindAsync(
            _actor.UserId.Value,
            courseId,
            ct);

        if (membership is null ||
            !membership.Active ||
            membership.Role != CourseRole.Teacher)
        {
            return Error.Forbidden(
                "You are not allowed to manage groups in this course.");
        }

        return null;
    }
}
