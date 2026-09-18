using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Owners;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Owners;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
[PermissionAuthorize(PermissionCodes.OwnerView)]
public sealed class OwnersController(IOwnerService ownerService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? phoneNumber, string? ownerCode, CancellationToken cancellationToken)
    {
        var model = new OwnerIndexViewModel
        {
            PhoneNumber = phoneNumber,
            OwnerCode = ownerCode,
            Searched = !string.IsNullOrWhiteSpace(phoneNumber) || !string.IsNullOrWhiteSpace(ownerCode)
        };
        if (!model.Searched)
        {
            return View(model);
        }

        try
        {
            model.Results = await ownerService.SearchAsync(
                new OwnerSearchCriteria(phoneNumber, ownerCode), cancellationToken);
        }
        catch (OwnerManagementException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return View(model);
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.OwnerManage)]
    public IActionResult Create() => View(new CreateOwnerViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.OwnerManage)]
    public async Task<IActionResult> Create(CreateOwnerViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var ownerCode = await ownerService.CreateAsync(new CreateOwnerRequest(
                CurrentUserId(), model.FullName, model.PhoneNumber, model.Email, model.Address), cancellationToken);
            TempData["StatusMessage"] = $"Đã tạo chủ nuôi với mã {ownerCode}.";
            return RedirectToAction(nameof(Index));
        }
        catch (OwnerManagementException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.PetView)]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var owner = await ownerService.FindAsync(id, cancellationToken);
        return owner is null ? NotFound() : View(owner);
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.OwnerManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var owner = await ownerService.FindAsync(id, cancellationToken);
        return owner is null ? NotFound() : View(EditOwnerViewModel.From(owner));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.OwnerManage)]
    public async Task<IActionResult> Edit(EditOwnerViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await ownerService.UpdateAsync(new UpdateOwnerRequest(
                CurrentUserId(), model.Id, DecodeRowVersion(model.RowVersion),
                model.FullName, model.PhoneNumber, model.Email, model.Address), cancellationToken);
            TempData["StatusMessage"] = "Đã cập nhật chủ nuôi.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }
        catch (OwnerManagementException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.OwnerManage)]
    public async Task<IActionResult> SetActive(EditOwnerViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await ownerService.SetActiveAsync(new OwnerActivationRequest(
                CurrentUserId(), model.Id, DecodeRowVersion(model.RowVersion), model.IsActive), cancellationToken);
            TempData["StatusMessage"] = model.IsActive ? "Đã mở khóa chủ nuôi." : "Đã khóa chủ nuôi.";
        }
        catch (OwnerManagementException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    private static byte[] DecodeRowVersion(string base64Value)
    {
        try
        {
            return Convert.FromBase64String(base64Value);
        }
        catch (FormatException)
        {
            throw new OwnerManagementException("Dữ liệu phiên bản không hợp lệ. Hãy tải lại trang và thử lại.");
        }
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated request is missing its user identifier.");
}
