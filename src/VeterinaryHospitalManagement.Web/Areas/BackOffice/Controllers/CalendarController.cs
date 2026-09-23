using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Appointments;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
[PermissionAuthorize(PermissionCodes.CalendarView)]
public sealed class CalendarController(
    IAppointmentService appointmentService,
    IVeterinarianShiftService shiftService,
    IVietnamTimeProvider timeProvider,
    ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? veterinarianId, CancellationToken ct)
    {
        var vets = await db.VeterinarianProfiles.AsNoTracking()
            .Where(v => v.IsActive && v.User.IsActive)
            .OrderBy(v => v.User.FullName)
            .Select(v => new SelectListItem
            {
                Value = v.Id.ToString(),
                Text = $"{v.User.FullName} ({v.DoctorCode})"
            })
            .ToListAsync(ct);

        ViewBag.Veterinarians = vets;
        ViewBag.SelectedVeterinarianId = veterinarianId;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Events(DateTimeOffset start, DateTimeOffset end, int? veterinarianId, CancellationToken ct)
    {
        var offset = timeProvider.LocalNow.Offset;
        var events = new List<CalendarEventDto>();

        // 1. Load Appointments
        var appointments = await appointmentService.ListAsync(start, end, veterinarianId, null, ct);
        foreach (var apt in appointments)
        {
            var startLocal = apt.StartAt.ToOffset(offset);
            var endLocal = apt.EndAt.ToOffset(offset);

            var statusClass = apt.Status switch
            {
                "Scheduled" => "event-scheduled",
                "CheckedIn" => "event-checked-in",
                "Cancelled" => "event-cancelled",
                "NoShow" => "event-noshow",
                _ => "event-default"
            };

            var statusText = apt.Status switch
            {
                "Scheduled" => "Chờ khám",
                "CheckedIn" => "Đã tiếp nhận",
                "Cancelled" => "Đã hủy",
                "NoShow" => "Vắng mặt",
                _ => apt.Status
            };

            events.Add(new CalendarEventDto
            {
                Id = $"apt-{apt.Id}",
                Title = $"{apt.PetName} - BS. {apt.VeterinarianName}",
                Start = startLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
                End = endLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
                AllDay = false,
                ClassName = statusClass,
                Url = Url.Action("Details", "Appointments", new { area = "BackOffice", id = apt.Id }),
                ExtendedProps = new CalendarEventExtendedProps
                {
                    PetName = apt.PetName,
                    VeterinarianName = apt.VeterinarianName,
                    Status = apt.Status,
                    StatusText = statusText,
                    Reason = apt.Reason,
                    Type = "appointment"
                }
            });
        }

        // 2. Load Shifts
        var shifts = await shiftService.ListAsync(veterinarianId, ct);
        var filteredShifts = shifts.Where(s => s.StartAt < end && start < s.EndAt && s.IsActive);

        foreach (var shift in filteredShifts)
        {
            var startLocal = shift.StartAt.ToOffset(offset);
            var endLocal = shift.EndAt.ToOffset(offset);

            events.Add(new CalendarEventDto
            {
                Id = $"shift-{shift.Id}",
                Title = $"Ca trực: {shift.VeterinarianFullName}",
                Start = startLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
                End = endLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
                AllDay = false,
                ClassName = "event-shift",
                ExtendedProps = new CalendarEventExtendedProps
                {
                    VeterinarianName = shift.VeterinarianFullName,
                    Status = "Active",
                    StatusText = "Ca làm việc",
                    Type = "shift"
                }
            });
        }

        return Json(events);
    }
}
