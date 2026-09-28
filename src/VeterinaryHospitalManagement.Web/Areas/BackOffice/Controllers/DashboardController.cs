using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Dashboard;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
public sealed class DashboardController(IDashboardService dashboardService) : Controller
{
    [HttpGet]
    [PermissionAuthorize(PermissionCodes.DashboardView)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await dashboardService.GetDashboardMetricsAsync(cancellationToken);
        return View(model);
    }
}
