using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Schedules;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
[PermissionAuthorize(PermissionCodes.ScheduleManage)]
public sealed class SchedulesController(
    IVeterinarianShiftService service,
    IVietnamTimeProvider timeProvider) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? veterinarianId, CancellationToken ct)
    {
        var shifts = await service.ListAsync(veterinarianId, ct);
        var vets = await service.GetActiveVeterinariansAsync(ct);

        ViewBag.Veterinarians = vets;
        ViewBag.SelectedVeterinarianId = veterinarianId;
        return View(shifts);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? veterinarianId, CancellationToken ct)
    {
        var now = timeProvider.LocalNow;
        var defaultStart = now.Date.AddDays(1).AddHours(8);
        var defaultEnd = now.Date.AddDays(1).AddHours(12);

        var m = new CreateShiftViewModel
        {
            VeterinarianId = veterinarianId ?? 0,
            StartAtLocal = defaultStart,
            EndAtLocal = defaultEnd
        };
        await PopulateVeterinarians(m, ct);
        return View(m);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateShiftViewModel m, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateVeterinarians(m, ct);
            return View(m);
        }

        try
        {
            var offset = timeProvider.LocalNow.Offset;
            var startUtc = new DateTimeOffset(m.StartAtLocal, offset).ToUniversalTime();
            var endUtc = new DateTimeOffset(m.EndAtLocal, offset).ToUniversalTime();

            await service.CreateAsync(new(Current(), m.VeterinarianId, startUtc, endUtc), ct);
            TempData["StatusMessage"] = "Đã tạo ca làm việc thành công.";
            return RedirectToAction(nameof(Index), new { veterinarianId = m.VeterinarianId });
        }
        catch (ShiftManagementException e)
        {
            ModelState.AddModelError(string.Empty, e.Message);
            await PopulateVeterinarians(m, ct);
            return View(m);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var x = await service.FindAsync(id, ct);
        if (x is null) return NotFound();
        return View(EditShiftViewModel.From(x, timeProvider.LocalNow.Offset));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditShiftViewModel m, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadEdit(m, ct);

        try
        {
            var offset = timeProvider.LocalNow.Offset;
            var startUtc = new DateTimeOffset(m.StartAtLocal, offset).ToUniversalTime();
            var endUtc = new DateTimeOffset(m.EndAtLocal, offset).ToUniversalTime();

            await service.UpdateAsync(new(Current(), m.Id, Decode(m.RowVersion), startUtc, endUtc), ct);
            TempData["StatusMessage"] = "Đã cập nhật ca làm việc.";
            return RedirectToAction(nameof(Edit), new { id = m.Id });
        }
        catch (ShiftManagementException e)
        {
            ModelState.AddModelError(string.Empty, e.Message);
            return await ReloadEdit(m, ct);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(EditShiftViewModel m, CancellationToken ct)
    {
        try
        {
            await service.SetActiveAsync(new(Current(), m.Id, Decode(m.RowVersion), m.IsActive), ct);
            TempData["StatusMessage"] = "Đã cập nhật trạng thái ca làm việc.";
        }
        catch (ShiftManagementException e)
        {
            TempData["ErrorMessage"] = e.Message;
        }

        return RedirectToAction(nameof(Edit), new { id = m.Id });
    }

    private async Task PopulateVeterinarians(CreateShiftViewModel m, CancellationToken ct)
    {
        var vets = await service.GetActiveVeterinariansAsync(ct);
        m.Veterinarians = vets
            .Select(x => new SelectListItem($"{x.DoctorCode} - {x.FullName}", x.Id.ToString()))
            .ToList();
    }

    private async Task<IActionResult> ReloadEdit(EditShiftViewModel m, CancellationToken ct)
    {
        var current = await service.FindAsync(m.Id, ct);
        if (current is null) return NotFound();
        m.DoctorCode = current.DoctorCode;
        m.VeterinarianFullName = current.VeterinarianFullName;
        m.IsActive = current.IsActive;
        m.RowVersion = Convert.ToBase64String(current.RowVersion);
        return View(m);
    }

    private string Current() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException();

    private static byte[] Decode(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new ShiftManagementException("Dữ liệu phiên bản không hợp lệ. Hãy tải lại trang.");
        }
    }
}
