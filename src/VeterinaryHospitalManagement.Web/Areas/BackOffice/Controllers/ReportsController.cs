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
        if (!TryResolveRange(from, to, out var fromDate, out var toDate))
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

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.ReportExport)]
    public async Task<IActionResult> Export(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        if (!TryResolveRange(from, to, out var fromDate, out var toDate))
            return BadRequest("Khoảng ngày không hợp lệ.");

        var report = await revenueReportService.GetAsync(fromDate, toDate, cancellationToken);
        var file = RevenueWorkbookExporter.Create(report, vietnamTimeProvider.LocalNow.Offset);
        var fileName = $"DoanhThu_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private bool TryResolveRange(DateTime? from, DateTime? to, out DateOnly fromDate, out DateOnly toDate)
    {
        var today = DateOnly.FromDateTime(vietnamTimeProvider.LocalNow.DateTime);
        fromDate = from.HasValue ? DateOnly.FromDateTime(from.Value) : new DateOnly(today.Year, today.Month, 1);
        toDate = to.HasValue ? DateOnly.FromDateTime(to.Value) : today;
        return ModelState.IsValid && fromDate <= toDate && toDate < DateOnly.MaxValue;
    }
}
