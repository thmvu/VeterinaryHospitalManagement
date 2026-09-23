namespace VeterinaryHospitalManagement.Web.Services.Scheduling;

public sealed record CreateAppointmentRequest(string ActorUserId, int PetId, int VeterinarianId,
    DateTimeOffset StartAt, DateTimeOffset EndAt, string Reason);

public sealed record AppointmentListItem(int Id, string AppointmentNumber, int PetId, string PetName,
    int VeterinarianId, string VeterinarianName, DateTimeOffset StartAt, DateTimeOffset EndAt,
    string Reason, string Status);

public sealed record AppointmentAvailability(bool IsAvailable, string? Reason);

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentListItem>> ListAsync(DateTimeOffset from, DateTimeOffset to,
        int? veterinarianId = null, CancellationToken ct = default);
    Task<AppointmentAvailability> CheckAvailabilityAsync(int petId, int veterinarianId,
        DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken ct = default);
    Task<int> CreateAsync(CreateAppointmentRequest request, CancellationToken ct = default);
}

public sealed class AppointmentManagementException(string message) : Exception(message);
