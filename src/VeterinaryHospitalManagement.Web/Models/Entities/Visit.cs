using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Models.Entities;

/// <summary>
/// Đại diện cho một lượt khám tại bệnh viện.
/// Mỗi Visit liên kết tối đa một Appointment (hoặc walk-in không có Appointment).
/// Snapshot owner/pet/veterinarian được lưu tại thời điểm check-in để lịch sử không bị ảnh hưởng khi thông tin thay đổi sau.
/// </summary>
public sealed class Visit
{
    public int Id { get; set; }

    /// <summary>Số thứ tự visit dạng V-YYYYMMDD-NNNN, unique.</summary>
    public string VisitNumber { get; set; } = string.Empty;

    /// <summary>Lịch hẹn liên kết (nullable = walk-in).</summary>
    public int? AppointmentId { get; set; }

    public int PetId { get; set; }
    public int VeterinarianId { get; set; }

    // ── Snapshots tại thời điểm check-in ──────────────────────────────────
    public string PetNameSnapshot { get; set; } = string.Empty;
    public string OwnerNameSnapshot { get; set; } = string.Empty;
    public string OwnerPhoneSnapshot { get; set; } = string.Empty;
    public string VeterinarianNameSnapshot { get; set; } = string.Empty;

    // ── Trạng thái ──────────────────────────────────────────────────────
    public VisitStatus Status { get; set; } = VisitStatus.Waiting;

    /// <summary>Thời điểm check-in (luôn có khi tạo Visit).</summary>
    public DateTimeOffset CheckedInAt { get; set; }

    /// <summary>Thời điểm bác sĩ bắt đầu khám (set khi chuyển InProgress).</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Thời điểm hoàn tất khám (set khi chuyển Completed).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Lý do hủy, bắt buộc khi Cancelled.</summary>
    public string? CancellationReason { get; set; }

    public string CheckedInByUserId { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = [];

    // ── Navigation ────────────────────────────────────────────────────────
    public Appointment? Appointment { get; set; }
    public Pet Pet { get; set; } = null!;
    public VeterinarianProfile Veterinarian { get; set; } = null!;
    public ApplicationUser CheckedInByUser { get; set; } = null!;
}
