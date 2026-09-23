using System.ComponentModel.DataAnnotations;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Appointments;

public sealed class MarkNoShowViewModel
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string RowVersionBase64 { get; set; } = string.Empty;
}
