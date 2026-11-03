namespace Platform.Api.Services.Identity;

public interface ILogoutService
{
    public Task ExecuteAsync(string? rawToken, string correlationId, CancellationToken ct = default);
}