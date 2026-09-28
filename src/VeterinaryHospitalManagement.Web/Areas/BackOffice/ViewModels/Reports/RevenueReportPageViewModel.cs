using VeterinaryHospitalManagement.Web.Services.Reports;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Reports;

public sealed class RevenueReportPageViewModel
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public RevenueReport? Report { get; init; }
    public string? Error { get; init; }
}
