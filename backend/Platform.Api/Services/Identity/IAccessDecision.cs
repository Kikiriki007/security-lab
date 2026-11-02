namespace Platform.Api.Services.Identity;

public interface IAccessDecision
{
    bool CanChangeAccountStatus(CallerIdentity caller);
}