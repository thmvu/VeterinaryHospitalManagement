using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using VeterinaryHospitalManagement.Web.Services.Scheduling;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Appointments;

public sealed class AppointmentListViewModel
{
    public IReadOnlyList<AppointmentListItem> Items { get; set; } = [];

    [Display(Name = "Ngày")]
    [DataType(DataType.Date)]
    public DateOnly? FilterDate { get; set; }

    [Display(Name = "Bác sĩ")]
    public int? FilterVeterinarianId { get; set; }

    [Display(Name = "Trạng thái")]
    public string? FilterStatus { get; set; }

    public IReadOnlyList<SelectListItem> Veterinarians { get; set; } = [];
}
