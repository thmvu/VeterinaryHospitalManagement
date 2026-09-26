using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Veterinarians;
public sealed class CreateVeterinarianViewModel { [Required,Display(Name="Tài khoản")] public string UserId { get; set; }=string.Empty; [StringLength(150),Display(Name="Chuyên khoa")] public string? Specialty { get; set; } public IReadOnlyList<SelectListItem> Users { get; set; }=[]; }
