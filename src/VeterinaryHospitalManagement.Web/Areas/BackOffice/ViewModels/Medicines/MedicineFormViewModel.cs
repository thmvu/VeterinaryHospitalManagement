using System.ComponentModel.DataAnnotations;
using VeterinaryHospitalManagement.Web.Services.Catalogs;
namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Medicines;
public sealed class MedicineFormViewModel
{
 public int Id{get;set;}
 [Required(ErrorMessage="Mã thuốc là bắt buộc."),StringLength(30),Display(Name="Mã thuốc")]public string Code{get;set;}=string.Empty;
 [Required(ErrorMessage="Tên thuốc là bắt buộc."),StringLength(150),Display(Name="Tên thuốc")]public string Name{get;set;}=string.Empty;
 [StringLength(200),Display(Name="Hoạt chất")]public string? ActiveIngredient{get;set;}
 [StringLength(100),Display(Name="Hàm lượng")]public string? Strength{get;set;}
 [Required(ErrorMessage="Đơn vị tính là bắt buộc."),StringLength(50),Display(Name="Đơn vị tính")]public string Unit{get;set;}=string.Empty;
 public bool IsActive{get;set;}=true; public string RowVersion{get;set;}=string.Empty;
 public static MedicineFormViewModel From(MedicineDetails x)=>new(){Id=x.Id,Code=x.Code,Name=x.Name,ActiveIngredient=x.ActiveIngredient,Strength=x.Strength,Unit=x.Unit,IsActive=x.IsActive,RowVersion=Convert.ToBase64String(x.RowVersion)};
}
