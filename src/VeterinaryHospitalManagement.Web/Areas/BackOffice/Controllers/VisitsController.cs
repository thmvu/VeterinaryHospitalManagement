using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Visits;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Services.Time;
using VeterinaryHospitalManagement.Web.Services.Visits;
using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Catalogs;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
public sealed class VisitsController(
    IVisitService visitService,
    IVietnamTimeProvider timeProvider,
    ApplicationDbContext db,
    IClinicalServiceService clinicalServices,
    IServiceCatalogService catalogs) : Controller
{
    // ── Hàng đợi khám (Queue) ──────────────────────────────────────────────────

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.VisitView)]
    public async Task<IActionResult> Index(int? veterinarianId, CancellationToken ct)
    {
        var queue = await visitService.GetQueueAsync(veterinarianId, ct);
        var vets = await GetActiveVeterinariansAsync(ct);

        var vm = new VisitQueueViewModel
        {
            SelectedVeterinarianId = veterinarianId,
            VeterinarianOptions = vets,
            Queue = queue
        };

        return View(vm);
    }

    // ── Chi tiết lượt khám ─────────────────────────────────────────────────────

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.VisitView)]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var detail = await visitService.GetDetailsAsync(id, ct);
        if (detail is null) return NotFound();

        var userId = GetCurrentUserId();
        var isAssignedVet = await db.VeterinarianProfiles
            .AnyAsync(v => v.Id == detail.VeterinarianId && v.UserId == userId, ct);

        var offset = timeProvider.LocalNow.Offset;
        var vm = new VisitDetailViewModel
        {
            Detail = detail,
            CheckedInAtLocal = detail.CheckedInAt.ToOffset(offset).DateTime,
            StartedAtLocal = detail.StartedAt.HasValue ? detail.StartedAt.Value.ToOffset(offset).DateTime : null,
            CompletedAtLocal = detail.CompletedAt.HasValue ? detail.CompletedAt.Value.ToOffset(offset).DateTime : null,
            CanStart = detail.Status == "Waiting" && isAssignedVet,
            CanComplete = detail.Status == "InProgress" && isAssignedVet && User.IsInRole(SystemRoleNames.Veterinarian),
            CanViewMedicalRecord = isAssignedVet || User.IsInRole(SystemRoleNames.Admin),
            CanManageServices = isAssignedVet && detail.Status == "InProgress" && User.IsInRole(SystemRoleNames.Veterinarian),
            ServiceLines = detail.Status is "InProgress" or "Completed" ? await clinicalServices.ListAsync(id, ct) : [],
            ServiceOptions = detail.Status == "InProgress" ? (await catalogs.ListAsync(ct)).Where(x => x.IsActive && x.Price == decimal.Truncate(x.Price)).ToList() : [],
            VeterinarianOptions = await GetActiveVeterinariansAsync(ct)
        };

        return View(vm);
    }

    // ── Tiếp nhận vãng lai (Walk-in) ───────────────────────────────────────────

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.VisitWalkIn)]
    public async Task<IActionResult> WalkIn(CancellationToken ct)
    {
        var vm = new WalkInViewModel
        {
            PetOptions = await GetActivePetOptionsAsync(ct),
            VeterinarianOptions = await GetActiveVeterinariansAsync(ct)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.VisitWalkIn)]
    public async Task<IActionResult> WalkIn(WalkInViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.PetOptions = await GetActivePetOptionsAsync(ct);
            model.VeterinarianOptions = await GetActiveVeterinariansAsync(ct);
            return View(model);
        }

        try
        {
            var userId = GetCurrentUserId();
            var visitId = await visitService.WalkInAsync(new(model.PetId!.Value, model.VeterinarianId!.Value, userId), ct);
            TempData["StatusMessage"] = "Tiếp nhận thú cưng vãng lai thành công.";
            return RedirectToAction(nameof(Details), new { id = visitId });
        }
        catch (VisitManagementException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.PetOptions = await GetActivePetOptionsAsync(ct);
            model.VeterinarianOptions = await GetActiveVeterinariansAsync(ct);
            return View(model);
        }
    }

    // ── Tiếp nhận từ Lịch hẹn (Check-in) ───────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.VisitCheckIn)]
    public async Task<IActionResult> CheckIn(int appointmentId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var visitId = await visitService.CheckInFromAppointmentAsync(new(appointmentId, userId), ct);
            TempData["StatusMessage"] = "Tiếp nhận lịch hẹn vào hàng đợi khám thành công.";
            return RedirectToAction(nameof(Details), new { id = visitId });
        }
        catch (VisitManagementException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction("Details", "Appointments", new { id = appointmentId });
        }
    }

    // ── Phân công lại bác sĩ (Assign) ──────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.VisitAssign)]
    public async Task<IActionResult> Assign(AssignVeterinarianViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu phân công bác sĩ không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = model.VisitId });
        }

        try
        {
            var userId = GetCurrentUserId();
            var rowVersion = Convert.FromBase64String(model.RowVersionBase64);
            await visitService.AssignVeterinarianAsync(new(model.VisitId, model.NewVeterinarianId, userId, rowVersion), ct);
            TempData["StatusMessage"] = "Phân công lại bác sĩ phụ trách thành công.";
        }
        catch (Exception ex) when (ex is VisitManagementException or FormatException)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.VisitId });
    }

    // ── Hủy lượt khám (Cancel) ─────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.VisitCancel)]
    public async Task<IActionResult> Cancel(CancelVisitViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do hủy lượt khám.";
            return RedirectToAction(nameof(Details), new { id = model.VisitId });
        }

        try
        {
            var userId = GetCurrentUserId();
            var rowVersion = Convert.FromBase64String(model.RowVersionBase64);
            await visitService.CancelAsync(new(model.VisitId, model.CancellationReason, userId, rowVersion), ct);
            TempData["StatusMessage"] = "Hủy lượt khám thành công.";
        }
        catch (Exception ex) when (ex is VisitManagementException or FormatException)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.VisitId });
    }

    // ── Bắt đầu khám (Start) ────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.VisitStart)]
    public async Task<IActionResult> Start(StartVisitViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu bắt đầu khám không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = model.VisitId });
        }

        try
        {
            var userId = GetCurrentUserId();
            var rowVersion = Convert.FromBase64String(model.RowVersionBase64);
            await visitService.StartAsync(new(model.VisitId, userId, rowVersion), ct);
            TempData["StatusMessage"] = "Đã bắt đầu lượt khám (chuyển sang trạng thái Đang khám).";
        }
        catch (Exception ex) when (ex is VisitManagementException or FormatException)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.VisitId });
    }

    // ── Hoàn tất khám ────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.VisitComplete)]
    public async Task<IActionResult> Complete(CompleteVisitViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu hoàn tất lượt khám không hợp lệ. Hãy tải lại trang.";
            return RedirectToAction(nameof(Details), new { id = model.VisitId });
        }
        try
        {
            await visitService.CompleteAsync(new(model.VisitId, GetCurrentUserId(),
                Convert.FromBase64String(model.RowVersionBase64)), ct);
            TempData["StatusMessage"] = "Đã hoàn tất lượt khám và chốt hồ sơ lâm sàng.";
        }
        catch (Exception ex) when (ex is VisitManagementException or FormatException)
        { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = model.VisitId });
    }

    // ── Dịch vụ lượt khám ───────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.VisitServiceManage)]
    public async Task<IActionResult> AddService(int visitId, int serviceCatalogId, string quantity, CancellationToken ct)
    {
        if (!decimal.TryParse(quantity, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            TempData["ErrorMessage"] = "Số lượng không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = visitId });
        }
        try
        {
            await clinicalServices.AddAsync(new(visitId, GetCurrentUserId(), serviceCatalogId, parsed), ct);
            TempData["StatusMessage"] = "Đã thêm dịch vụ vào lượt khám.";
        }
        catch (ClinicalServiceAccessException) { return Forbid(); }
        catch (ClinicalServiceManagementException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = visitId });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.VisitServiceManage)]
    public async Task<IActionResult> PerformService(int visitId, int lineId, string rowVersionBase64, CancellationToken ct)
    {
        try
        {
            await clinicalServices.PerformAsync(new(lineId, GetCurrentUserId(), Convert.FromBase64String(rowVersionBase64)), ct);
            TempData["StatusMessage"] = "Đã xác nhận thực hiện dịch vụ.";
        }
        catch (ClinicalServiceAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is ClinicalServiceManagementException or FormatException)
        { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = visitId });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.VisitServiceManage)]
    public async Task<IActionResult> CancelService(int visitId, int lineId, string rowVersionBase64, string reason, CancellationToken ct)
    {
        try
        {
            await clinicalServices.CancelAsync(new(lineId, GetCurrentUserId(), Convert.FromBase64String(rowVersionBase64), reason), ct);
            TempData["StatusMessage"] = "Đã hủy dòng dịch vụ.";
        }
        catch (ClinicalServiceAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is ClinicalServiceManagementException or FormatException)
        { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = visitId });
    }

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Không tìm thấy thông tin đăng nhập.");

    private async Task<IReadOnlyList<SelectListItem>> GetActiveVeterinariansAsync(CancellationToken ct)
    {
        return await db.VeterinarianProfiles
            .AsNoTracking()
            .Where(v => v.IsActive && v.User.IsActive)
            .OrderBy(v => v.User.FullName)
            .Select(v => new SelectListItem(
                $"{v.DoctorCode} - {v.User.FullName} ({v.Specialty})",
                v.Id.ToString()))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<SelectListItem>> GetActivePetOptionsAsync(CancellationToken ct)
    {
        return await db.Pets
            .AsNoTracking()
            .Where(p => p.IsActive && p.Owner.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem(
                $"{p.PetCode} - {p.Name} (Chủ: {p.Owner.FullName} - {p.Owner.PhoneNumber})",
                p.Id.ToString()))
            .ToListAsync(ct);
    }
}
