namespace VeterinaryHospitalManagement.Web.Models.Entities;

public class AuditLog
{
    public long Id { get; set; }

    public string ActorType { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public ApplicationUser? User { get; set; }
}
