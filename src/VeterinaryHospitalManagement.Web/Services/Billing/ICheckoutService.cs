using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Billing;

public sealed record CheckoutLine(int VisitServiceId, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record CheckoutPreview(int VisitId, string VisitNumber, string OwnerName, string OwnerPhone,
    string PetName, IReadOnlyList<CheckoutLine> Items, decimal TotalAmount, int? ExistingInvoiceId);
public sealed record ConfirmCheckoutRequest(int VisitId, string ActorUserId, PaymentMethod PaymentMethod);
public sealed record InvoiceDetails(int Id, string InvoiceNumber, int VisitId, string VisitNumber, string OwnerName,
    string OwnerPhone, string PetName, string ProcessedByName, DateTimeOffset PaidAt,
    PaymentMethod PaymentMethod, IReadOnlyList<CheckoutLine> Items, decimal TotalAmount);
public sealed record UnpaidVisit(int VisitId, string VisitNumber, string OwnerName, string PetName,
    DateTimeOffset CompletedAt, decimal TotalAmount);
public sealed record PaidInvoice(int Id, string InvoiceNumber, string VisitNumber, string OwnerName,
    decimal TotalAmount, DateTimeOffset PaidAt);

public interface ICheckoutService
{
    Task<CheckoutPreview> PreviewAsync(int visitId, CancellationToken ct = default);
    Task<int> ConfirmAsync(ConfirmCheckoutRequest request, CancellationToken ct = default);
    Task<InvoiceDetails?> FindAsync(int invoiceId, CancellationToken ct = default);
    Task<IReadOnlyList<UnpaidVisit>> ListUnpaidAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PaidInvoice>> ListRecentAsync(CancellationToken ct = default);
}

public sealed class CheckoutManagementException(string message) : Exception(message);
