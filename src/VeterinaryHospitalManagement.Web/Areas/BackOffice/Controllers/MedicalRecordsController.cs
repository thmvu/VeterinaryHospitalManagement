using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.MedicalRecords;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Visits;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
public sealed class MedicalRecordsController(
    IMedicalRecordService medicalRecords, IVisitService visits, ApplicationDbContext db) : Controller
{
    [HttpGet]
    [PermissionAuthorize(PermissionCodes.MedicalRecordView)]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var visit = await visits.GetDetailsAsync(id, ct);
        if (visit is null) return NotFound();
        var assigned = await IsAssignedActiveVeterinarianAsync(visit.VeterinarianId, ct);
        if (!assigned && !User.IsInRole(SystemRoleNames.Admin)) return Forbid();
        var record = await medicalRecords.FindByVisitAsync(id, ct);
        return View(new MedicalRecordPageViewModel
        {
            VisitId = id, VisitNumber = visit.VisitNumber, PetName = visit.PetName,
            VeterinarianName = visit.VeterinarianName, VisitStatus = visit.Status,
            Record = record, CanEdit = assigned && visit.Status == VisitStatus.InProgress.ToString()
                && record?.Status != ClinicalDocumentStatus.Finalized.ToString()
        });
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.MedicalRecordEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var visit = await visits.GetDetailsAsync(id, ct);
        if (visit is null) return NotFound();
        if (!await IsAssignedActiveVeterinarianAsync(visit.VeterinarianId, ct)) return Forbid();
        if (visit.Status != VisitStatus.InProgress.ToString()) return RedirectToAction(nameof(Details), new { id });
        var record = await medicalRecords.FindByVisitAsync(id, ct);
        if (record?.Status == ClinicalDocumentStatus.Finalized.ToString()) return RedirectToAction(nameof(Details), new { id });
        return View(new EditMedicalRecordViewModel
        {
            VisitId = id, VisitNumber = visit.VisitNumber, PetName = visit.PetName,
            RowVersionBase64 = record is null ? null : Convert.ToBase64String(record.RowVersion),
            ChiefComplaint = record?.ChiefComplaint ?? string.Empty,
            Symptoms = record?.Symptoms, WeightKg = record?.WeightKg,
            TemperatureC = record?.TemperatureC, Diagnosis = record?.Diagnosis,
            TreatmentNotes = record?.TreatmentNotes, FollowUpDate = record?.FollowUpDate
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.MedicalRecordEdit)]
    public async Task<IActionResult> Edit(EditMedicalRecordViewModel model, CancellationToken ct)
    {
        var visit = await visits.GetDetailsAsync(model.VisitId, ct);
        if (visit is null) return NotFound();
        if (!await IsAssignedActiveVeterinarianAsync(visit.VeterinarianId, ct)) return Forbid();
        model.VisitNumber = visit.VisitNumber;
        model.PetName = visit.PetName;
        if (!ModelState.IsValid) return View(model);

        byte[]? rowVersion;
        try { rowVersion = string.IsNullOrWhiteSpace(model.RowVersionBase64) ? null : Convert.FromBase64String(model.RowVersionBase64); }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "Dữ liệu trang không hợp lệ. Hãy tải lại trang.");
            return View(model);
        }

        try
        {
            await medicalRecords.SaveDraftAsync(new(model.VisitId, CurrentUserId(), rowVersion,
                model.ChiefComplaint, model.Symptoms, model.WeightKg, model.TemperatureC,
                model.Diagnosis, model.TreatmentNotes, model.FollowUpDate), ct);
            TempData["StatusMessage"] = "Đã lưu bản nháp bệnh án.";
            return RedirectToAction(nameof(Details), new { id = model.VisitId });
        }
        catch (MedicalRecordAccessException) { return Forbid(); }
        catch (MedicalRecordManagementException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private Task<bool> IsAssignedActiveVeterinarianAsync(int veterinarianId, CancellationToken ct) =>
        db.VeterinarianProfiles.AsNoTracking().AnyAsync(x => x.Id == veterinarianId
            && x.UserId == CurrentUserId() && x.IsActive && x.User.IsActive, ct);
}
