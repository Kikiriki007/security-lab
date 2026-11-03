using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;

namespace Platform.Api.Security;

public sealed class DevelopmentSubjectResolver(IConfiguration configuration) : IRequestSubjectResolver
{
    public SubjectRef? Resolve(HttpContext context)
    {
        var id = configuration["Identity:DevSubjectId"];
        return string.IsNullOrWhiteSpace(id) ? null : new SubjectRef(id, AccountState.Active);
    }
}