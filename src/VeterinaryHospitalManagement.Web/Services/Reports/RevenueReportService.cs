using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Services.Reports;

public sealed class RevenueReportService(
    ApplicationDbContext db,
    IVietnamTimeProvider vietnamTimeProvider) : IRevenueReportService
{
    public async Task<RevenueReport> GetAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (from > to || to == DateOnly.MaxValue)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        var offset = vietnamTimeProvider.LocalNow.Offset;
        var startUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
        var endUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();

        var rows = await db.Invoices.AsNoTracking()
            .Where(invoice => invoice.PaidAt >= startUtc && invoice.PaidAt < endUtc)
            .OrderByDescending(invoice => invoice.PaidAt)
            .ThenByDescending(invoice => invoice.Id)
            .Select(invoice => new RevenueInvoiceRow(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.PaidAt,
                invoice.OwnerNameSnapshot,
                invoice.PetNameSnapshot,
                invoice.PaymentMethod,
                invoice.TotalAmount))
            .ToListAsync(cancellationToken);

        return new RevenueReport(from, to, rows, rows.Sum(row => row.TotalAmount));
    }
}
