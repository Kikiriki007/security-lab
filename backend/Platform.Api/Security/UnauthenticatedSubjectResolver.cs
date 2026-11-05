using Platform.Access.Domain.Models;

namespace Platform.Api.Security;

public sealed class UnauthenticatedSubjectResolver : IRequestSubjectResolver
{
    public SubjectRef? Resolve(HttpContext context) => null;
}