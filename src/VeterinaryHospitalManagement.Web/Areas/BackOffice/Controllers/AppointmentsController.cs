using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Appointments;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
public sealed class AppointmentsController(
    IAppointmentService appointmentService,
    IVietnamTimeProvider timeProvider,
    ApplicationDbContext db) : Controller
{
    [HttpGet]
    [PermissionAuthorize(PermissionCodes.AppointmentView)]
    public async Task<IActionResult> Index(DateOnly? filterDate, int? filterVeterinarianId, string? filterStatus, CancellationToken ct)
    {
        DateTimeOffset? from = null;
        DateTimeOffset? to = null;

        var offset = timeProvider.LocalNow.Offset;
        if (filterDate.HasValue)
        {
            var startLocal = filterDate.Value.ToDateTime(TimeOnly.MinValue);
            var endLocal = filterDate.Value.ToDateTime(TimeOnly.MaxValue);
            from = new DateTimeOffset(startLocal, offset).ToUniversalTime();
            to = new DateTimeOffset(endLocal, offset).ToUniversalTime();
        }

        var items = await appointmentService.ListAsync(from, to, filterVeterinarianId, filterStatus, ct);
        var vets = await GetActiveVeterinariansAsync(ct);

        var vm = new AppointmentListViewModel
        {
            Items = items,
            FilterDate = filterDate,
            FilterVeterinarianId = filterVeterinarianId,
            FilterStatus = filterStatus,
            Veterinarians = vets
        };

        return View(vm);
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.AppointmentCreate)]
    public async Task<IActionResult> Create(int? petId, int? veterinarianId, CancellationToken ct)
    {
        var now = timeProvider.LocalNow;
        var defaultStart = now.Date.AddDays(1).AddHours(9);
        var defaultEnd = defaultStart.AddHours(1);

        var vm = new CreateAppointmentViewModel
        {
            PetId = petId ?? 0,
            VeterinarianId = veterinarianId ?? 0,
            StartAtLocal = defaultStart,
            EndAtLocal = defaultEnd
        };

        await PopulateSelectListsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.AppointmentCreate)]
    public async Task<IActionResult> Create(CreateAppointmentViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSelectListsAsync(vm, ct);
            return View(vm);
        }

        try
        {
            var offset = timeProvider.LocalNow.Offset;
            var startUtc = new DateTimeOffset(vm.StartAtLocal, offset).ToUniversalTime();
            var endUtc = new DateTimeOffset(vm.EndAtLocal, offset).ToUniversalTime();

            var appointmentId = await appointmentService.CreateAsync(
                new CreateAppointmentRequest(CurrentUserId(), vm.PetId, vm.VeterinarianId, startUtc, endUtc, vm.Reason),
                ct);

            TempData["StatusMessage"] = "Đã tạo lịch hẹn thành công.";
            return RedirectToAction(nameof(Details), new { id = appointmentId });
        }
        catch (AppointmentManagementException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateSelectListsAsync(vm, ct);
            return View(vm);
        }
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.AppointmentView)]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var item = await appointmentService.GetDetailsAsync(id, ct);
        if (item is null) return NotFound();

        var offset = timeProvider.LocalNow.Offset;
        var vm = new AppointmentDetailViewModel
        {
            Id = item.Id,
            AppointmentNumber = item.AppointmentNumber,
            PetId = item.PetId,
            PetName = item.PetName,
            OwnerName = item.OwnerName,
            OwnerPhone = item.OwnerPhone,
            VeterinarianId = item.VeterinarianId,
            VeterinarianName = item.VeterinarianName,
            StartAtLocal = item.StartAt.ToOffset(offset).DateTime,
            EndAtLocal = item.EndAt.ToOffset(offset).DateTime,
            Reason = item.Reason,
            Status = item.Status,
            CancellationReason = item.CancellationReason,
            CreatedByName = item.CreatedByName,
            CreatedAtLocal = item.CreatedAt.ToOffset(offset).DateTime,
            RowVersionBase64 = Convert.ToBase64String(item.RowVersion)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.AppointmentCancel)]
    public async Task<IActionResult> Cancel(CancelAppointmentViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu hủy lịch hẹn không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }

        try
        {
            var rowVersion = Convert.FromBase64String(vm.RowVersionBase64);
            await appointmentService.CancelAsync(vm.Id, CurrentUserId(), vm.Reason, rowVersion, ct);
            TempData["StatusMessage"] = "Đã hủy lịch hẹn thành công.";
        }
        catch (AppointmentManagementException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.AppointmentMarkNoShow)]
    public async Task<IActionResult> MarkNoShow(MarkNoShowViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu đánh dấu vắng mặt không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }

        try
        {
            var rowVersion = Convert.FromBase64String(vm.RowVersionBase64);
            await appointmentService.MarkNoShowAsync(vm.Id, CurrentUserId(), rowVersion, ct);
            TempData["StatusMessage"] = "Đã ghi nhận vắng mặt (No-Show) cho lịch hẹn.";
        }
        catch (AppointmentManagementException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.AppointmentView)]
    public async Task<IActionResult> CheckAvailability(int petId, int veterinarianId, DateTime startAtLocal, DateTime endAtLocal, CancellationToken ct)
    {
        if (petId <= 0 || veterinarianId <= 0 || startAtLocal >= endAtLocal)
        {
            return Json(new { isAvailable = false, reason = "Thông tin thời gian hoặc đối tượng không hợp lệ." });
        }

        var offset = timeProvider.LocalNow.Offset;
        var startUtc = new DateTimeOffset(startAtLocal, offset).ToUniversalTime();
        var endUtc = new DateTimeOffset(endAtLocal, offset).ToUniversalTime();

        var result = await appointmentService.CheckAvailabilityAsync(petId, veterinarianId, startUtc, endUtc, ct);
        return Json(new { isAvailable = result.IsAvailable, reason = result.Reason });
    }

    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task PopulateSelectListsAsync(CreateAppointmentViewModel vm, CancellationToken ct)
    {
        var pets = await db.Pets.AsNoTracking()
            .Where(p => p.IsActive && p.Owner.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.Name} (Chủ: {p.Owner.FullName} - {p.Owner.PhoneNumber})"
            })
            .ToListAsync(ct);

        vm.Pets = pets;
        vm.Veterinarians = await GetActiveVeterinariansAsync(ct);
    }

    private async Task<IReadOnlyList<SelectListItem>> GetActiveVeterinariansAsync(CancellationToken ct)
    {
        return await db.VeterinarianProfiles.AsNoTracking()
            .Where(v => v.IsActive && v.User.IsActive)
            .OrderBy(v => v.User.FullName)
            .Select(v => new SelectListItem
            {
                Value = v.Id.ToString(),
                Text = $"{v.User.FullName} ({v.DoctorCode})"
            })
            .ToListAsync(ct);
    }
}
