using System.ComponentModel.DataAnnotations;
using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Pets;

public sealed class EditPetViewModel
{
    [Required]
    public int PetId { get; set; }

    public string PetCode { get; set; } = string.Empty;

    public int OwnerId { get; set; }

    public string OwnerCode { get; set; } = string.Empty;

    public string OwnerFullName { get; set; } = string.Empty;

    [Required]
    public string ExpectedRowVersion { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên thú cưng là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên thú cưng không được vượt quá 100 ký tự.")]
    [Display(Name = "Tên thú cưng")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loài.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loài.")]
    [Display(Name = "Loài")]
    public int SpeciesId { get; set; }

    [Display(Name = "Giống")]
    public int? BreedId { get; set; }

    [EnumDataType(typeof(PetSex), ErrorMessage = "Giới tính thú cưng không hợp lệ.")]
    [Display(Name = "Giới tính")]
    public PetSex Sex { get; set; }

    [Display(Name = "Ngày sinh")]
    [DataType(DataType.Date)]
    public DateOnly? BirthDate { get; set; }

    [StringLength(100, ErrorMessage = "Màu lông không được vượt quá 100 ký tự.")]
    [Display(Name = "Màu lông / Đặc điểm")]
    public string? Color { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    public bool IsActive { get; set; }
}
