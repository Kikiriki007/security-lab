using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Team21.Acc.Core.Common;
using Team21.Acc.Core.Courses;
using Team21.Acc.Core.Interfaces;
using Team21.Acc.Infrastructure.Courses.Data;
using Team21.Acc.Infrastructure.Courses.Services;

namespace Team21.Acc.Tests.Courses;

public class CourseServiceTests
{
    private SqliteConnection _connection = null!;
    private CoursesDbContext _db = null!;

    private static readonly Guid ManagerId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CoursesDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new CoursesDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private CourseService CreateService(Guid? actorId, bool allowed)
    {
        return new CourseService(
            _db,
            new TestActor(actorId),
            new TestAuthorization(allowed));
    }

    [Test]
    public async Task Create_ValidRequest_CreatesCourse()
    {
        var service = CreateService(ManagerId, true);

        var result = await service.CreateAsync(
            new CreateCourseRequest(
                " oib-test ",
                "Test Course",
                "Description"));

        Assert.That(result.IsSuccess, Is.True);

        var saved = await _db.Courses.AsNoTracking().SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved.Code, Is.EqualTo("OIB-TEST"));
            Assert.That(saved.Name, Is.EqualTo("Test Course"));
            Assert.That(saved.Status, Is.EqualTo(CourseStatus.Active));
            Assert.That(saved.CreatedBy, Is.EqualTo(ManagerId));
        });
    }

    [Test]
    public async Task Create_AnonymousUser_ReturnsUnauthorized()
    {
        var service = CreateService(null, true);

        var result = await service.CreateAsync(
            new CreateCourseRequest("TEST", "Test Course", null));

        Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.Unauthorized));
        Assert.That(await _db.Courses.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Create_UserWithoutPermission_ReturnsForbidden()
    {
        var service = CreateService(Guid.NewGuid(), false);

        var result = await service.CreateAsync(
            new CreateCourseRequest("TEST", "Test Course", null));

        Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.Forbidden));
        Assert.That(await _db.Courses.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Create_EmptyName_ReturnsValidationError()
    {
        var service = CreateService(ManagerId, true);

        var result = await service.CreateAsync(
            new CreateCourseRequest("TEST", "", null));

        Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.Validation));
        Assert.That(await _db.Courses.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        _db.Courses.Add(
            new Course("TEST", "Original Course", null, ManagerId));

        await _db.SaveChangesAsync();

        var service = CreateService(ManagerId, true);

        var result = await service.CreateAsync(
            new CreateCourseRequest("test", "Duplicate Course", null));

        Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.Conflict));
        Assert.That(await _db.Courses.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Archive_ValidRequest_ArchivesCourse()
    {
        var course = new Course(
            "ARCHIVE-TEST",
            "Archive Test",
            null,
            ManagerId);

        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var service = CreateService(ManagerId, true);

        var result = await service.ArchiveAsync(course.Id);

        Assert.That(result.IsSuccess, Is.True);

        _db.ChangeTracker.Clear();

        var saved = await _db.Courses.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved.Status, Is.EqualTo(CourseStatus.Archived));
            Assert.That(saved.ArchivedAt, Is.Not.Null);
            Assert.That(saved.ArchivedBy, Is.EqualTo(ManagerId));
            Assert.That(saved.IsDeleted, Is.False);
        });
    }

    [Test]
public async Task Archive_UserWithoutPermission_ReturnsNotFound()
{
    var course = new Course(
        "ARCHIVE-DENIED",
        "Protected Course",
        null,
        ManagerId);

    _db.Courses.Add(course);
    await _db.SaveChangesAsync();

    var service = CreateService(Guid.NewGuid(), false);

    var result = await service.ArchiveAsync(course.Id);

    Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.NotFound));

    _db.ChangeTracker.Clear();

    var saved = await _db.Courses.SingleAsync();

    Assert.Multiple(() =>
    {
        Assert.That(saved.Status, Is.EqualTo(CourseStatus.Active));
        Assert.That(saved.ArchivedAt, Is.Null);
        Assert.That(saved.ArchivedBy, Is.Null);
    });
}
    private sealed class TestActor(Guid? userId) : ICurrentActor
    {
        public Guid? UserId => userId;

        public bool IsAuthenticated =>
            userId.HasValue && userId.Value != Guid.Empty;
    }

    private sealed class TestAuthorization(bool allowed)
        : ICourseAuthorization
    {
        public Task<bool> CanCreateAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(allowed);
        }

        public Task<bool> CanArchiveAsync(
            Guid userId,
            Guid courseId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(allowed);
        }
    }

    [Test]
public async Task Archive_NonExistingCourse_ReturnsNotFound()
{
    var service = CreateService(ManagerId, true);

    var result = await service.ArchiveAsync(Guid.NewGuid());

    Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.NotFound));
}

[Test]
public async Task Archive_Twice_IsIdempotent()
{
    var course = new Course(
        "ARCHIVE-TWICE",
        "Idempotency Test",
        null,
        ManagerId);

    _db.Courses.Add(course);
    await _db.SaveChangesAsync();

    var service = CreateService(ManagerId, true);

    // First archive
    var firstResult = await service.ArchiveAsync(course.Id);

    Assert.That(firstResult.IsSuccess, Is.True);

    _db.ChangeTracker.Clear();

    var firstSaved = await _db.Courses.SingleAsync();

    var originalArchivedAt = firstSaved.ArchivedAt;
    var originalArchivedBy = firstSaved.ArchivedBy;

    // Second archive
    var secondResult = await service.ArchiveAsync(course.Id);

    Assert.That(secondResult.IsSuccess, Is.True);

    _db.ChangeTracker.Clear();

    var secondSaved = await _db.Courses.SingleAsync();

    Assert.Multiple(() =>
    {
        Assert.That(secondSaved.Status,
            Is.EqualTo(CourseStatus.Archived));

        Assert.That(secondSaved.ArchivedAt,
            Is.EqualTo(originalArchivedAt));

        Assert.That(secondSaved.ArchivedBy,
            Is.EqualTo(originalArchivedBy));

        Assert.That(secondSaved.IsDeleted, Is.False);
    });

    Assert.That(await _db.Courses.CountAsync(), Is.EqualTo(1));
}
}