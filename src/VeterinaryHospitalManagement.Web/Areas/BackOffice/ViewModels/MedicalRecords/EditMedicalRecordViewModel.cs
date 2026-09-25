using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.MedicalRecords;

public sealed class EditMedicalRecordViewModel
{
    public int VisitId { get; set; }
    [ValidateNever]
    public string VisitNumber { get; set; } = string.Empty;
    [ValidateNever]
    public string PetName { get; set; } = string.Empty;
    public string? RowVersionBase64 { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do khám.")]
    [StringLength(2000, ErrorMessage = "Lý do khám không được vượt quá 2000 ký tự.")]
    public string ChiefComplaint { get; set; } = string.Empty;

    public string? Symptoms { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? TemperatureC { get; set; }
    public string? Diagnosis { get; set; }
    public string? TreatmentNotes { get; set; }
    public DateOnly? FollowUpDate { get; set; }
}
