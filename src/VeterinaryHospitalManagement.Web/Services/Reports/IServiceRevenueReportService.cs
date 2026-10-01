namespace VeterinaryHospitalManagement.Web.Services.Reports;

public sealed record ServiceRevenueRow(string ServiceName, decimal Quantity, int LineCount, decimal Revenue);

public sealed record ServiceRevenueReport(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<ServiceRevenueRow> Rows,
    decimal TotalQuantity,
    int TotalLineCount,
    decimal TotalRevenue);

public interface IServiceRevenueReportService
{
    Task<ServiceRevenueReport> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
