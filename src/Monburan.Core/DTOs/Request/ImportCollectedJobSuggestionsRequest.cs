namespace Monburan.Core.DTOs.Request;

public sealed record CollectedJobInput(
    string Title,
    string? CompanyName,
    string? City,
    string? State,
    string? Description,
    string SourceName,
    string SourceUrl,
    DateTimeOffset CapturedAt,
    string Region);

public sealed record ImportCollectedJobSuggestionsRequest(IReadOnlyList<CollectedJobInput> Jobs);

public sealed record ImportCollectedJobSuggestionsResponse(int ImportedCount, int DuplicateCount);
