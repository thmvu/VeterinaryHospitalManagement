using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Models.Entities;

/// <summary>Biên nhận thanh toán một lần cho một lượt khám đã hoàn tất.</summary>
public sealed class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int VisitId { get; set; }
    public string OwnerNameSnapshot { get; set; } = string.Empty;
    public string OwnerPhoneSnapshot { get; set; } = string.Empty;
    public string PetNameSnapshot { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTimeOffset PaidAt { get; set; }
    public string ProcessedByUserId { get; set; } = string.Empty;
    public string ProcessedByNameSnapshot { get; set; } = string.Empty;
    public Visit Visit { get; set; } = null!;
    public ApplicationUser ProcessedByUser { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}
