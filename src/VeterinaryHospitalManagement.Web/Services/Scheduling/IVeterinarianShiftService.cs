namespace VeterinaryHospitalManagement.Web.Services.Scheduling;

// ── DTOs ────────────────────────────────────────────────────────────────────

public sealed record ShiftListItem(
    int Id,
    int VeterinarianId,
    string DoctorCode,
    string VeterinarianFullName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsActive,
    byte[] RowVersion);

public sealed record ShiftDetails(
    int Id,
    int VeterinarianId,
    string DoctorCode,
    string VeterinarianFullName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsActive,
    byte[] RowVersion);

public sealed record VeterinarianChoiceItem(
    int Id,
    string DoctorCode,
    string FullName);

public sealed record CreateShiftRequest(
    string ActorUserId,
    int VeterinarianId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);

public sealed record UpdateShiftRequest(
    string ActorUserId,
    int Id,
    byte[] ExpectedRowVersion,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);

public sealed record ShiftActivationRequest(
    string ActorUserId,
    int Id,
    byte[] ExpectedRowVersion,
    bool IsActive);

// ── Interface ────────────────────────────────────────────────────────────────

public interface IVeterinarianShiftService
{
    Task<IReadOnlyList<ShiftListItem>> ListAsync(int? veterinarianId, CancellationToken ct = default);

    Task<ShiftDetails?> FindAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<VeterinarianChoiceItem>> GetActiveVeterinariansAsync(CancellationToken ct = default);

    Task<int> CreateAsync(CreateShiftRequest request, CancellationToken ct = default);

    Task UpdateAsync(UpdateShiftRequest request, CancellationToken ct = default);

    Task SetActiveAsync(ShiftActivationRequest request, CancellationToken ct = default);
}
