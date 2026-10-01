using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Services.Reports;

public sealed class VisitReportService(
    ApplicationDbContext db,
    IVietnamTimeProvider vietnamTimeProvider) : IVisitReportService
{
    public async Task<VisitReport> GetAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (from > to || to == DateOnly.MaxValue)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        var offset = vietnamTimeProvider.LocalNow.Offset;
        var startUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
        var endUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
        var counts = await db.Visits.AsNoTracking()
            .Where(visit => visit.CheckedInAt >= startUtc && visit.CheckedInAt < endUtc)
            .GroupBy(visit => visit.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var countByStatus = counts.ToDictionary(item => item.Status, item => item.Count);
        var statuses = Enum.GetValues<VisitStatus>()
            .Select(status => new VisitStatusReportRow(status, DisplayName(status), countByStatus.GetValueOrDefault(status)))
            .ToList();

        return new VisitReport(from, to, statuses, statuses.Sum(item => item.Count));
    }

    private static string DisplayName(VisitStatus status) => status switch
    {
        VisitStatus.Waiting => "Đang chờ",
        VisitStatus.InProgress => "Đang khám",
        VisitStatus.Completed => "Đã hoàn tất",
        VisitStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };
}
