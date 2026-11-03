namespace Platform.Api.Services.Identity;

public interface ILoginService
{
    public Task<LoginResult> ExecuteAsync(string? email, string? password, string correlationId,
        CancellationToken ct = default);
}