using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Reports;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Reports;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
public sealed class ReportsController(
    IRevenueReportService revenueReportService,
    IVietnamTimeProvider vietnamTimeProvider) : Controller
{
    [HttpGet]
    [PermissionAuthorize(PermissionCodes.ReportView)]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(vietnamTimeProvider.LocalNow.DateTime);
        var fromDate = from.HasValue ? DateOnly.FromDateTime(from.Value) : new DateOnly(today.Year, today.Month, 1);
        var toDate = to.HasValue ? DateOnly.FromDateTime(to.Value) : today;
        if (!ModelState.IsValid || fromDate > toDate || toDate == DateOnly.MaxValue)
        {
            return View(new RevenueReportPageViewModel
            {
                From = fromDate,
                To = toDate,
                Error = "Khoảng ngày không hợp lệ. Ngày bắt đầu phải trước hoặc bằng ngày kết thúc."
            });
        }

        var report = await revenueReportService.GetAsync(fromDate, toDate, cancellationToken);
        return View(new RevenueReportPageViewModel { From = fromDate, To = toDate, Report = report });
    }
}
