using System.Collections.Concurrent;
using Platform.Access.Contracts;
using Platform.Access.Domain.Models;

namespace Platform.Access.Infrastructure.Resources;

public sealed class InMemoryResourceDescriptorResolver : IResourceDescriptorResolver
{
    private readonly ConcurrentDictionary<(string Type, string Id), Guid> resourceCourses = new();

    public void SetCourse(ResourceRef resource, Guid courseId)
    {
        ArgumentNullException.ThrowIfNull(resource);
        resourceCourses[(resource.Type, resource.Id)] = courseId;
    }

    public bool Remove(ResourceRef resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return resourceCourses.TryRemove((resource.Type, resource.Id), out _);
    }

    public ValueTask<Guid?> ResolveCourseIdAsync(
        ResourceRef resource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Guid?>(
            resourceCourses.TryGetValue((resource.Type, resource.Id), out var courseId)
                ? courseId
                : null);
    }
}
