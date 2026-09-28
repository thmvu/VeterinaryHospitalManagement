namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Audit;

public sealed class AuditLogIndexViewModel
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? UserId { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    public IReadOnlyList<AuditLogItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<string> AvailableActions { get; init; } = [];
    public IReadOnlyList<string> AvailableEntities { get; init; } = [];
    public IReadOnlyList<AuditUserOptionViewModel> AvailableUsers { get; init; } = [];
}

public sealed record AuditLogItemViewModel(
    long Id,
    string ActorType,
    string? UserId,
    string UserDisplayName,
    string Action,
    string EntityName,
    string EntityId,
    string Description,
    DateTimeOffset CreatedAt
);

public sealed record AuditUserOptionViewModel(string Id, string DisplayName);
