using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Pets;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Owners;
using VeterinaryHospitalManagement.Web.Services.Pets;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
[PermissionAuthorize(PermissionCodes.PetView)]
public sealed class PetsController(IPetService petService, IOwnerService ownerService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var pet = await petService.FindAsync(id, cancellationToken);
        if (pet is null)
        {
            return NotFound();
        }

        return View(pet);
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.PetManage)]
    public async Task<IActionResult> Create(int ownerId, CancellationToken cancellationToken)
    {
        var owner = await ownerService.FindAsync(ownerId, cancellationToken);
        if (owner is null)
        {
            return NotFound();
        }

        if (!owner.IsActive)
        {
            TempData["ErrorMessage"] = "Không thể tạo thú cưng cho chủ nuôi đang bị khóa.";
            return RedirectToAction("Details", "Owners", new { id = ownerId });
        }

        ViewBag.SpeciesList = await petService.GetActiveSpeciesAsync(cancellationToken);
        return View(new CreatePetViewModel
        {
            OwnerId = owner.Id,
            OwnerCode = owner.OwnerCode,
            OwnerFullName = owner.FullName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.PetManage)]
    public async Task<IActionResult> Create(CreatePetViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.SpeciesList = await petService.GetActiveSpeciesAsync(cancellationToken);
            return View(model);
        }

        try
        {
            var petCode = await petService.CreateAsync(new CreatePetRequest(
                CurrentUserId(),
                model.OwnerId,
                model.Name,
                model.SpeciesId,
                model.BreedId,
                model.Sex,
                model.BirthDate,
                model.Color,
                model.Notes), cancellationToken);

            TempData["StatusMessage"] = $"Đã thêm thú cưng {model.Name} ({petCode}).";
            return RedirectToAction("Details", "Owners", new { id = model.OwnerId });
        }
        catch (PetManagementException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ViewBag.SpeciesList = await petService.GetActiveSpeciesAsync(cancellationToken);
            return View(model);
        }
    }

    [HttpGet]
    [PermissionAuthorize(PermissionCodes.PetManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var pet = await petService.FindAsync(id, cancellationToken);
        if (pet is null)
        {
            return NotFound();
        }

        await PopulateEditOptionsAsync(pet.SpeciesId, pet.BreedId, cancellationToken);

        return View(new EditPetViewModel
        {
            PetId = pet.Id,
            PetCode = pet.PetCode,
            OwnerId = pet.OwnerId,
            OwnerCode = pet.OwnerCode,
            OwnerFullName = pet.OwnerFullName,
            ExpectedRowVersion = Convert.ToBase64String(pet.RowVersion),
            Name = pet.Name,
            SpeciesId = pet.SpeciesId,
            BreedId = pet.BreedId,
            Sex = pet.Sex,
            BirthDate = pet.BirthDate,
            Color = pet.Color,
            Notes = pet.Notes,
            IsActive = pet.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.PetManage)]
    public async Task<IActionResult> Edit(EditPetViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateEditOptionsAsync(model.SpeciesId, model.BreedId, cancellationToken);
            return View(model);
        }

        try
        {
            var rowVersion = DecodeRowVersion(model.ExpectedRowVersion);
            await petService.UpdateAsync(new UpdatePetRequest(
                CurrentUserId(),
                model.PetId,
                rowVersion,
                model.Name,
                model.SpeciesId,
                model.BreedId,
                model.Sex,
                model.BirthDate,
                model.Color,
                model.Notes), cancellationToken);

            TempData["StatusMessage"] = $"Đã cập nhật thông tin thú cưng {model.PetCode}.";
            return RedirectToAction(nameof(Details), new { id = model.PetId });
        }
        catch (PetConcurrencyException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await PopulateEditOptionsAsync(model.SpeciesId, model.BreedId, cancellationToken);
            return View(model);
        }
        catch (PetManagementException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await PopulateEditOptionsAsync(model.SpeciesId, model.BreedId, cancellationToken);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.PetManage)]
    public async Task<IActionResult> SetActive(int id, string expectedRowVersion, bool isActive, CancellationToken cancellationToken)
    {
        try
        {
            var rowVersion = DecodeRowVersion(expectedRowVersion);
            await petService.SetActiveAsync(new PetActivationRequest(
                CurrentUserId(), id, rowVersion, isActive), cancellationToken);

            TempData["StatusMessage"] = isActive
                ? "Đã mở khóa thú cưng thành công."
                : "Đã khóa thú cưng thành công.";
        }
        catch (PetConcurrencyException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (PetManagementException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> BreedsBySpecies(int speciesId, CancellationToken cancellationToken)
    {
        var breeds = await petService.GetBreedsBySpeciesAsync(speciesId, cancellationToken);
        return Json(breeds);
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Không tìm thấy định danh người dùng đăng nhập.");

    private async Task PopulateEditOptionsAsync(int speciesId, int? breedId, CancellationToken cancellationToken)
    {
        ViewBag.SpeciesList = await petService.GetSpeciesForEditAsync(speciesId, cancellationToken);
        ViewBag.BreedList = await petService.GetBreedsForEditAsync(speciesId, breedId, cancellationToken);
    }

    private static byte[] DecodeRowVersion(string base64Value)
    {
        try
        {
            return Convert.FromBase64String(base64Value);
        }
        catch (FormatException)
        {
            throw new PetManagementException("Dữ liệu phiên bản không hợp lệ. Hãy tải lại trang và thử lại.");
        }
    }
}
