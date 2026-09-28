using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Dashboard;

namespace VeterinaryHospitalManagement.Web.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardMetricsAsync(CancellationToken cancellationToken = default);
}
