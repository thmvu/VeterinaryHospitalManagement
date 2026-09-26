using System.ComponentModel.DataAnnotations;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Billing;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Invoices;

public sealed class InvoiceIndexViewModel
{
    public IReadOnlyList<UnpaidVisit> Unpaid { get; set; } = [];
    public IReadOnlyList<PaidInvoice> Recent { get; set; } = [];
}

public sealed class CheckoutPageViewModel
{
    public required CheckoutPreview Preview { get; set; }
    public bool CanCheckout { get; set; }
}

public sealed class ConfirmCheckoutViewModel
{
    [Range(1, int.MaxValue)]
    public int VisitId { get; set; }

    [Required]
    public PaymentMethod PaymentMethod { get; set; }
}
