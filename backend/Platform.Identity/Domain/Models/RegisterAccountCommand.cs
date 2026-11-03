namespace Platform.Identity.Domain.Models;

public sealed record RegisterAccountCommand(string Email, string Password)
{
    public override string ToString() => "RegisterAccountCommand { *** }";
}