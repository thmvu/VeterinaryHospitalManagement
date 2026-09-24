namespace VeterinaryHospitalManagement.Web.Services.Visits;

// ── Request/Result DTOs ────────────────────────────────────────────────────────

/// <summary>Yêu cầu check-in từ một Appointment đã Scheduled.</summary>
public sealed record CheckInFromAppointmentRequest(int AppointmentId, string PerformedByUserId);

/// <summary>Yêu cầu walk-in không có Appointment.</summary>
public sealed record WalkInRequest(int PetId, int VeterinarianId, string PerformedByUserId);

/// <summary>Yêu cầu phân công lại bác sĩ (chỉ khi Waiting).</summary>
public sealed record AssignVeterinarianRequest(int VisitId, int NewVeterinarianId, string PerformedByUserId, byte[] RowVersion);

/// <summary>Yêu cầu hủy Visit (chỉ khi Waiting).</summary>
public sealed record CancelVisitRequest(int VisitId, string CancellationReason, string PerformedByUserId, byte[] RowVersion);

/// <summary>Yêu cầu bắt đầu khám (InProgress), chỉ bác sĩ phụ trách.</summary>
public sealed record StartVisitRequest(int VisitId, string PerformedByUserId, byte[] RowVersion);

/// <summary>DTO hiển thị Visit trong danh sách hàng đợi.</summary>
public sealed record VisitQueueItem(
    int Id,
    string VisitNumber,
    string PetName,
    string OwnerName,
    string OwnerPhone,
    string VeterinarianName,
    string Status,
    DateTimeOffset CheckedInAt,
    int? AppointmentId);

/// <summary>DTO chi tiết một Visit.</summary>
public sealed record VisitDetailDto(
    int Id,
    string VisitNumber,
    int? AppointmentId,
    int PetId,
    string PetName,
    string OwnerName,
    string OwnerPhone,
    int VeterinarianId,
    string VeterinarianName,
    string Status,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? CancellationReason,
    string CheckedInByName,
    byte[] RowVersion);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IVisitService
{
    /// <summary>
    /// Check-in từ Appointment Scheduled.
    /// Idempotent: nếu Appointment đã CheckedIn và Visit tồn tại thì trả về Visit đó.
    /// </summary>
    Task<int> CheckInFromAppointmentAsync(CheckInFromAppointmentRequest request, CancellationToken ct = default);

    /// <summary>Walk-in tạo Visit Waiting không liên kết Appointment.</summary>
    Task<int> WalkInAsync(WalkInRequest request, CancellationToken ct = default);

    /// <summary>Phân công lại bác sĩ (chỉ khi Waiting).</summary>
    Task AssignVeterinarianAsync(AssignVeterinarianRequest request, CancellationToken ct = default);

    /// <summary>Hủy Visit (chỉ khi Waiting, chưa có clinical data).</summary>
    Task CancelAsync(CancelVisitRequest request, CancellationToken ct = default);

    /// <summary>Bắt đầu khám (chỉ bác sĩ phụ trách, chuyển InProgress).</summary>
    Task StartAsync(StartVisitRequest request, CancellationToken ct = default);

    /// <summary>Lấy danh sách hàng đợi hôm nay (Waiting + InProgress), theo bác sĩ nếu có.</summary>
    Task<IReadOnlyList<VisitQueueItem>> GetQueueAsync(int? veterinarianId, CancellationToken ct = default);

    /// <summary>Chi tiết Visit theo Id.</summary>
    Task<VisitDetailDto?> GetDetailsAsync(int visitId, CancellationToken ct = default);
}
