using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Services.Catalogs;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.ServiceCatalogs;

public sealed class EditServiceCatalogViewModel
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên dịch vụ là bắt buộc.")]
    [StringLength(150, ErrorMessage = "Tên dịch vụ không được vượt quá 150 ký tự.")]
    [Display(Name = "Tên dịch vụ")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhóm dịch vụ là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Nhóm dịch vụ không được vượt quá 100 ký tự.")]
    [Display(Name = "Nhóm dịch vụ")]
    public string Category { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Đơn giá phải nằm trong giới hạn cho phép.")]
    [Display(Name = "Đơn giá")]
    [ModelBinder(BinderType = typeof(InvariantDecimalModelBinder))]
    public decimal Price { get; set; }

    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    public static EditServiceCatalogViewModel From(ServiceCatalogDetails item) => new()
    {
        Id = item.Id,
        Code = item.Code,
        Name = item.Name,
        Category = item.Category,
        Price = item.Price,
        Description = item.Description,
        IsActive = item.IsActive,
        RowVersion = Convert.ToBase64String(item.RowVersion)
    };
}
