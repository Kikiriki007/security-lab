namespace Platform.Api.Modules.Laboratories.Api;

// Javni HTTP ugovor LAB-01.
//
// Zahtev za kreiranje sadrzi naslov.
public sealed record CreateLaboratoryRequest(string? Title);

public sealed record LaboratoryResponse(
    Guid Id,
    string Title,
    string Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

// Kodovi su stabilni; frontend (api/labs.ts) ih prevodi u poruke za korisnika.
public sealed record ApiErrorResponse(string Code, string Message);

public static class LabErrorCodes
{
    public const string InvalidTitle = "INVALID_TITLE";
    public const string InvalidBody = "INVALID_BODY";
}