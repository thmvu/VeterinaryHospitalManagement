using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Prescriptions;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Catalogs;
using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Visits;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
public sealed class PrescriptionsController(
    IPrescriptionService prescriptions, IVisitService visits, IMedicineService medicines,
    ApplicationDbContext db) : Controller
{
    [HttpGet, PermissionAuthorize(PermissionCodes.PrescriptionView)]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var visit = await visits.GetDetailsAsync(id, ct);
        if (visit is null) return NotFound();
        var assigned = await IsAssignedVeterinarianAsync(visit.VeterinarianId, ct);
        if (!assigned && !User.IsInRole(SystemRoleNames.Admin)) return Forbid();
        var prescription = await prescriptions.FindByVisitAsync(id, ct);
        return View(new PrescriptionPageViewModel
        {
            VisitId = id, VisitNumber = visit.VisitNumber, PetName = visit.PetName,
            VeterinarianName = visit.VeterinarianName, VisitStatus = visit.Status,
            CanEdit = assigned && visit.Status == VisitStatus.InProgress.ToString() &&
                prescription?.Status != ClinicalDocumentStatus.Finalized.ToString(),
            Prescription = prescription
        });
    }

    [HttpGet, PermissionAuthorize(PermissionCodes.PrescriptionManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var visit = await visits.GetDetailsAsync(id, ct);
        if (visit is null) return NotFound();
        if (!await IsAssignedVeterinarianAsync(visit.VeterinarianId, ct)) return Forbid();
        if (visit.Status != VisitStatus.InProgress.ToString()) return RedirectToAction(nameof(Details), new { id });
        var prescription = await prescriptions.FindByVisitAsync(id, ct);
        if (prescription?.Status == ClinicalDocumentStatus.Finalized.ToString())
            return RedirectToAction(nameof(Details), new { id });
        var model = new EditPrescriptionViewModel
        {
            VisitId = id, VisitNumber = visit.VisitNumber, PetName = visit.PetName,
            RowVersionBase64 = prescription is null ? null : Convert.ToBase64String(prescription.RowVersion),
            Instructions = prescription?.Instructions,
            Items = prescription?.Items.Select(x => new PrescriptionItemInputViewModel
            {
                Id = x.Id, MedicineId = x.MedicineId, Dosage = x.Dosage, Route = x.Route,
                Frequency = x.Frequency, Duration = x.Duration, Quantity = x.Quantity,
                Instructions = x.Instructions
            }).ToList() ?? []
        };
        if (model.Items.Count == 0) model.Items.Add(new PrescriptionItemInputViewModel());
        await FillMedicinesAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.PrescriptionManage)]
    public async Task<IActionResult> Edit(EditPrescriptionViewModel model, CancellationToken ct)
    {
        var visit = await visits.GetDetailsAsync(model.VisitId, ct);
        if (visit is null) return NotFound();
        if (!await IsAssignedVeterinarianAsync(visit.VeterinarianId, ct)) return Forbid();
        model.VisitNumber = visit.VisitNumber;
        model.PetName = visit.PetName;
        await FillMedicinesAsync(model, ct);
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
            await prescriptions.SaveDraftAsync(new(model.VisitId, CurrentUserId(), rowVersion, model.Instructions,
                model.Items.Select(x => new PrescriptionItemDraft(x.Id, x.MedicineId, x.Dosage, x.Route,
                    x.Frequency, x.Duration, x.Quantity, x.Instructions)).ToList()), ct);
            TempData["StatusMessage"] = "Đã lưu bản nháp đơn thuốc.";
            return RedirectToAction(nameof(Details), new { id = model.VisitId });
        }
        catch (PrescriptionAccessException) { return Forbid(); }
        catch (PrescriptionManagementException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    private async Task FillMedicinesAsync(EditPrescriptionViewModel model, CancellationToken ct) =>
        model.Medicines = await medicines.ListAsync(ct);

    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private Task<bool> IsAssignedVeterinarianAsync(int veterinarianId, CancellationToken ct) =>
        db.VeterinarianProfiles.AsNoTracking().AnyAsync(x => x.Id == veterinarianId &&
            x.UserId == CurrentUserId() && x.IsActive && x.User.IsActive &&
            db.UserRoles.Any(link => link.UserId == x.UserId &&
                db.Roles.Any(role => role.Id == link.RoleId && role.Name == SystemRoleNames.Veterinarian)), ct);
}
