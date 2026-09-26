using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Services.Catalogs;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Prescriptions;

public sealed class EditPrescriptionViewModel
{
    public int VisitId { get; set; }
    public string VisitNumber { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;
    public string? RowVersionBase64 { get; set; }

    [StringLength(2000, ErrorMessage = "Hướng dẫn chung tối đa 2000 ký tự.")]
    public string? Instructions { get; set; }

    public List<PrescriptionItemInputViewModel> Items { get; set; } = [];
    public IReadOnlyList<MedicineListItem> Medicines { get; set; } = [];
}

public sealed class PrescriptionItemInputViewModel
{
    public int? Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn thuốc.")]
    public int MedicineId { get; set; }
    [Required(ErrorMessage = "Liều dùng là bắt buộc."), StringLength(200)]
    public string Dosage { get; set; } = string.Empty;
    [Required(ErrorMessage = "Đường dùng là bắt buộc."), StringLength(100)]
    public string Route { get; set; } = string.Empty;
    [Required(ErrorMessage = "Tần suất là bắt buộc."), StringLength(200)]
    public string Frequency { get; set; } = string.Empty;
    [Required(ErrorMessage = "Thời gian dùng là bắt buộc."), StringLength(200)]
    public string Duration { get; set; } = string.Empty;
    [Range(0.01, 99999999.99, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    [ModelBinder(BinderType = typeof(PrescriptionQuantityModelBinder))]
    public decimal Quantity { get; set; }
    [StringLength(1000)]
    public string? Instructions { get; set; }
}
