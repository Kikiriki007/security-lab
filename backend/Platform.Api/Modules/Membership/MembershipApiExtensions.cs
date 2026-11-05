using Platform.Membership.Composition;

namespace Platform.Api.Modules.Membership;

/// <summary>
/// ACC-02: jedino mesto gde se modul za članstvo povezuje sa Platform.Api.
/// Program.cs poziva samo AddMembership() i MapMembershipAsync(); sve ostalo je ovde,
/// u našem folderu, pa zajednički fajlovi ostaju netaknuti kada menjamo svoj kod.
/// </summary>
public static class MembershipApiExtensions
{
    /// <summary>Poziva se pre AddAccessControl() (zbog ACC-02-5).</summary>
    public static IServiceCollection AddMembership(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddMembershipModule(environment);
        return services;
    }

    public static Task MapMembershipAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Endpoint za upis dolazi u ACC-02-3, a pravilo ko sme da upisuje u ACC-02-4.
        return Task.CompletedTask;
    }
}