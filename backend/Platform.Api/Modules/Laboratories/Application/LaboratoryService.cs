using Platform.Api.Modules.Laboratories.Domain;

namespace Platform.Api.Modules.Laboratories.Application;

public enum LabResultStatus
{
    Ok,
    InvalidTitle,
    Unauthenticated
}

public sealed record LabResult(LabResultStatus Status, Laboratory? Laboratory = null)
{
    public static LabResult Ok(Laboratory laboratory) => new(LabResultStatus.Ok, laboratory);
    public static readonly LabResult InvalidTitle = new(LabResultStatus.InvalidTitle);
    public static readonly LabResult Unauthenticated = new(LabResultStatus.Unauthenticated);
}

// LAB-01 slucaj upotrebe: kreiranje laboratorije.
//
// Poziva se tek posle AccessCheckFilter-a (uloga i poznata radnja).
public sealed class LaboratoryService(
    ILaboratoryRepository repository,
    TimeProvider timeProvider,
    ILogger<LaboratoryService> logger)
{
    public async Task<LabResult> CreateAsync(
        string? title, IIdentityContext caller, CancellationToken cancellationToken)
    {
        // Autor dolazi iskljucivo iz serverskog identiteta, nikad iz tela zahteva.
        Guid? creatorId = CallerId.From(caller);
        if (creatorId is null)
        {
            return LabResult.Unauthenticated;
        }

        if (!IsAcceptableTitle(title))
        {
            return LabResult.InvalidTitle;
        }

        Laboratory laboratory;
        try
        {
            // Domen generise novi Id i vreme kreiranja na serveru.
            laboratory = new Laboratory(title!, creatorId.Value, timeProvider);
        }
        catch (ArgumentException)
        {
            return LabResult.InvalidTitle;
        }

        await repository.AddAsync(laboratory, cancellationToken);

        logger.LogInformation(
            "Laboratory created: laboratoryId={LaboratoryId} createdBy={CreatedBy}",
            laboratory.Id, laboratory.CreatedBy);

        return LabResult.Ok(laboratory);
    }

    // Brza provera pre domena; konacno pravilo (obavezan, najvise 200 znakova) je u Laboratory.
    private static bool IsAcceptableTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title) && title.Trim().Length <= Laboratory.MaxTitleLength;
}