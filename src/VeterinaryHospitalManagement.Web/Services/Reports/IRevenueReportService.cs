using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Reports;

public sealed record RevenueInvoiceRow(
    int InvoiceId,
    string InvoiceNumber,
    DateTimeOffset PaidAt,
    string OwnerName,
    string PetName,
    PaymentMethod PaymentMethod,
    decimal TotalAmount);

public sealed record RevenueReport(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<RevenueInvoiceRow> Rows,
    decimal TotalAmount)
{
    public int InvoiceCount => Rows.Count;
}

public interface IRevenueReportService
{
    Task<RevenueReport> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
