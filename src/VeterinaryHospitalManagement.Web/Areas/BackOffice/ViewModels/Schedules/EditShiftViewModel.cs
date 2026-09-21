using System.ComponentModel.DataAnnotations;
using VeterinaryHospitalManagement.Web.Services.Scheduling;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Schedules;

public sealed class EditShiftViewModel
{
    public int Id { get; set; }

    public int VeterinarianId { get; set; }

    public string DoctorCode { get; set; } = string.Empty;

    public string VeterinarianFullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
    [Display(Name = "Thời gian bắt đầu")]
    public DateTime StartAtLocal { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
    [Display(Name = "Thời gian kết thúc")]
    public DateTime EndAtLocal { get; set; }

    public bool IsActive { get; set; }

    public string RowVersion { get; set; } = string.Empty;

    public static EditShiftViewModel From(ShiftDetails x, TimeSpan offset) => new()
    {
        Id = x.Id,
        VeterinarianId = x.VeterinarianId,
        DoctorCode = x.DoctorCode,
        VeterinarianFullName = x.VeterinarianFullName,
        StartAtLocal = x.StartAt.ToOffset(offset).DateTime,
        EndAtLocal = x.EndAt.ToOffset(offset).DateTime,
        IsActive = x.IsActive,
        RowVersion = Convert.ToBase64String(x.RowVersion)
    };
}
