using Platform.Access.Domain.Models;

namespace Platform.Api.Security;

public interface IRequestSubjectResolver
{
    SubjectRef? Resolve(HttpContext context);
}