using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Audit;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Audit;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
public sealed class AuditController(IAuditQueryService auditQueryService) : Controller
{
    [HttpGet]
    [PermissionAuthorize(PermissionCodes.AuditView)]
    public async Task<IActionResult> Index([FromQuery] AuditLogIndexViewModel filter, CancellationToken cancellationToken)
    {
        var model = await auditQueryService.QueryAuditLogsAsync(filter, cancellationToken);
        return View(model);
    }
}
