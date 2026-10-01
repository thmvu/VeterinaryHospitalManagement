using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Services.Reports;

public sealed class ServiceRevenueReportService(
    ApplicationDbContext db,
    IVietnamTimeProvider vietnamTimeProvider) : IServiceRevenueReportService
{
    public async Task<ServiceRevenueReport> GetAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (from > to || to == DateOnly.MaxValue)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        var offset = vietnamTimeProvider.LocalNow.Offset;
        var startUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
        var endUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
        var rows = await db.InvoiceItems.AsNoTracking()
            .Where(item => item.Invoice.PaidAt >= startUtc && item.Invoice.PaidAt < endUtc)
            .GroupBy(item => item.DescriptionSnapshot)
            .Select(group => new ServiceRevenueRow(
                group.Key,
                group.Sum(item => item.Quantity),
                group.Count(),
                group.Sum(item => item.LineTotal)))
            .OrderByDescending(row => row.Revenue)
            .ThenBy(row => row.ServiceName)
            .ToListAsync(cancellationToken);

        return new ServiceRevenueReport(from, to, rows,
            rows.Sum(row => row.Quantity), rows.Sum(row => row.LineCount), rows.Sum(row => row.Revenue));
    }
}
