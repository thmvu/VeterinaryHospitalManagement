using System.ComponentModel.DataAnnotations;
using VeterinaryHospitalManagement.Web.Services.Veterinarians;
namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Veterinarians;
public sealed class EditVeterinarianViewModel { public int Id { get; set; } public string DoctorCode { get; set; }=string.Empty; public string FullName { get; set; }=string.Empty; [StringLength(150),Display(Name="Chuyên khoa")] public string? Specialty { get; set; } public bool IsActive { get; set; } [Required] public string RowVersion { get; set; }=string.Empty; public static EditVeterinarianViewModel From(VeterinarianDetails x)=>new(){Id=x.Id,DoctorCode=x.DoctorCode,FullName=x.FullName,Specialty=x.Specialty,IsActive=x.IsActive,RowVersion=Convert.ToBase64String(x.RowVersion)}; }
