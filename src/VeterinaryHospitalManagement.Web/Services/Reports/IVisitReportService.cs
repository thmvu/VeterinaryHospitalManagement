using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Reports;

public sealed record VisitStatusReportRow(VisitStatus Status, string StatusName, int Count);

public sealed record VisitReport(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<VisitStatusReportRow> Statuses,
    int TotalCount);

public interface IVisitReportService
{
    Task<VisitReport> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
