using System.ComponentModel.DataAnnotations;
using VeterinaryHospitalManagement.Web.Services.Owners;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Owners;

public sealed class EditOwnerViewModel
{
    [Required]
    public int Id { get; set; }

    [Required(ErrorMessage = "Dữ liệu phiên bản không hợp lệ.")]
    public string RowVersion { get; set; } = string.Empty;

    public string OwnerCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    [Required(ErrorMessage = "Họ tên chủ nuôi là bắt buộc.")]
    [StringLength(150, ErrorMessage = "Họ tên không được vượt quá 150 ký tự.")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
    [StringLength(30, ErrorMessage = "Số điện thoại không được vượt quá 30 ký tự.")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự.")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    public static EditOwnerViewModel From(OwnerDetails owner) => new()
    {
        Id = owner.Id,
        RowVersion = Convert.ToBase64String(owner.RowVersion),
        OwnerCode = owner.OwnerCode,
        IsActive = owner.IsActive,
        FullName = owner.FullName,
        PhoneNumber = owner.PhoneNumber,
        Email = owner.Email,
        Address = owner.Address
    };
}
