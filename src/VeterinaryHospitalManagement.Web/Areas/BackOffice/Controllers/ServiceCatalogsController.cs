using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.ServiceCatalogs;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Catalogs;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
[PermissionAuthorize(PermissionCodes.CatalogManage)]
public sealed class ServiceCatalogsController(IServiceCatalogService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await service.ListAsync(cancellationToken));

    [HttpGet]
    public IActionResult Create() => View(new CreateServiceCatalogViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateServiceCatalogViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await service.CreateAsync(new(
                CurrentUserId(), model.Code, model.Name, model.Category, model.Price, model.Description), cancellationToken);
            TempData["StatusMessage"] = "Đã tạo dịch vụ.";
            return RedirectToAction(nameof(Index));
        }
        catch (ServiceCatalogManagementException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var item = await service.FindAsync(id, cancellationToken);
        return item is null ? NotFound() : View(EditServiceCatalogViewModel.From(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditServiceCatalogViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReloadEditAsync(model, cancellationToken);
        }

        try
        {
            await service.UpdateAsync(new(
                CurrentUserId(), model.Id, DecodeVersion(model.RowVersion), model.Name,
                model.Category, model.Price, model.Description), cancellationToken);
            TempData["StatusMessage"] = "Đã cập nhật dịch vụ.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }
        catch (ServiceCatalogManagementException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadEditAsync(model, cancellationToken);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(EditServiceCatalogViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await service.SetActiveAsync(new(
                CurrentUserId(), model.Id, DecodeVersion(model.RowVersion), model.IsActive), cancellationToken);
            TempData["StatusMessage"] = model.IsActive ? "Đã mở lại dịch vụ." : "Đã tạm ngưng dịch vụ.";
        }
        catch (ServiceCatalogManagementException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    private async Task<IActionResult> ReloadEditAsync(EditServiceCatalogViewModel model, CancellationToken cancellationToken)
    {
        var current = await service.FindAsync(model.Id, cancellationToken);
        if (current is null)
        {
            return NotFound();
        }

        model.Code = current.Code;
        model.IsActive = current.IsActive;
        model.RowVersion = Convert.ToBase64String(current.RowVersion);
        return View(model);
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException();

    private static byte[] DecodeVersion(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new ServiceCatalogManagementException("Dữ liệu phiên bản không hợp lệ. Hãy tải lại trang.");
        }
    }
}
