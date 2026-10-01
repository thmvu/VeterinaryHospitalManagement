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
    IVisitReportService visitReportService,
    IServiceRevenueReportService serviceRevenueReportService,
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
        var rows = report.Rows.Select(row => (IReadOnlyList<WorkbookCell>)new WorkbookCell[]
        {
            WorkbookCell.FromText(row.PaidAt.ToOffset(vietnamTimeProvider.LocalNow.Offset).ToString("yyyy-MM-dd HH:mm")),
            WorkbookCell.FromText(row.InvoiceNumber),
            WorkbookCell.FromText(row.OwnerName),
            WorkbookCell.FromText(row.PetName),
            WorkbookCell.FromText(row.PaymentMethod == VeterinaryHospitalManagement.Web.Models.Enums.PaymentMethod.Cash ? "Tiền mặt" : "Chuyển khoản"),
            WorkbookCell.FromNumber(row.TotalAmount)
        }).ToList();
        var file = ReportWorkbookExporter.Create("Doanh thu", "Báo cáo doanh thu",
            $"Từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy} (giờ Việt Nam)",
            ["Ngày thanh toán", "Số hóa đơn", "Chủ nuôi", "Thú cưng", "Phương thức", "Số tiền (VND)"],
            rows, "Tổng doanh thu", report.TotalAmount);
        var fileName = $"DoanhThu_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.ReportView)]
    public async Task<IActionResult> Visits(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        if (!TryResolveRange(from, to, out var fromDate, out var toDate))
            return View(new VisitReportPageViewModel
            {
                From = fromDate,
                To = toDate,
                Error = "Khoảng ngày không hợp lệ. Ngày bắt đầu phải trước hoặc bằng ngày kết thúc."
            });

        var report = await visitReportService.GetAsync(fromDate, toDate, cancellationToken);
        return View(new VisitReportPageViewModel { From = fromDate, To = toDate, Report = report });
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportVisits(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        if (!TryResolveRange(from, to, out var fromDate, out var toDate))
            return BadRequest("Khoảng ngày không hợp lệ.");

        var report = await visitReportService.GetAsync(fromDate, toDate, cancellationToken);
        var rows = report.Statuses.Select(row => (IReadOnlyList<WorkbookCell>)new WorkbookCell[]
        {
            WorkbookCell.FromText(row.StatusName),
            WorkbookCell.FromNumber(row.Count)
        }).ToList();
        var file = ReportWorkbookExporter.Create("Lượt khám", "Báo cáo lượt khám",
            $"Ngày tiếp nhận từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy} (giờ Việt Nam)",
            ["Trạng thái", "Số lượt"], rows, "Tổng lượt khám", report.TotalCount);
        var fileName = $"LuotKham_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.ReportView)]
    public async Task<IActionResult> Services(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        if (!TryResolveRange(from, to, out var fromDate, out var toDate))
            return View(new ServiceReportPageViewModel
            {
                From = fromDate,
                To = toDate,
                Error = "Khoảng ngày không hợp lệ. Ngày bắt đầu phải trước hoặc bằng ngày kết thúc."
            });

        var report = await serviceRevenueReportService.GetAsync(fromDate, toDate, cancellationToken);
        return View(new ServiceReportPageViewModel { From = fromDate, To = toDate, Report = report });
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportServices(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        if (!TryResolveRange(from, to, out var fromDate, out var toDate))
            return BadRequest("Khoảng ngày không hợp lệ.");

        var report = await serviceRevenueReportService.GetAsync(fromDate, toDate, cancellationToken);
        var rows = report.Rows.Select(row => (IReadOnlyList<WorkbookCell>)new WorkbookCell[]
        {
            WorkbookCell.FromText(row.ServiceName),
            WorkbookCell.FromNumber(row.Quantity),
            WorkbookCell.FromNumber(row.LineCount),
            WorkbookCell.FromNumber(row.Revenue)
        }).ToList();
        var file = ReportWorkbookExporter.Create("Dịch vụ", "Báo cáo doanh thu dịch vụ",
            $"Ngày thanh toán từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy} (giờ Việt Nam)",
            ["Dịch vụ", "Số lượng", "Số lần thực hiện", "Doanh thu (VND)"],
            rows, "Tổng doanh thu", report.TotalRevenue);
        var fileName = $"DoanhThuDichVu_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
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
