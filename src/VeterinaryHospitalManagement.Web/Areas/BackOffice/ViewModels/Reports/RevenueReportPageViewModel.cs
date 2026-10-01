using VeterinaryHospitalManagement.Web.Services.Reports;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Reports;

public sealed class RevenueReportPageViewModel
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public RevenueReport? Report { get; init; }
    public string? Error { get; init; }
}

public sealed class VisitReportPageViewModel
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public VeterinaryHospitalManagement.Web.Services.Reports.VisitReport? Report { get; init; }
    public string? Error { get; init; }
}

public sealed class ServiceReportPageViewModel
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public VeterinaryHospitalManagement.Web.Services.Reports.ServiceRevenueReport? Report { get; init; }
    public string? Error { get; init; }
}
