using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Users;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
[PermissionAuthorize(PermissionCodes.UserManage)]
public sealed class UsersController(IUserManagementService userManagementService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await userManagementService.ListAsync(cancellationToken));

    [HttpGet]
    public IActionResult Create() => View(new CreateUserViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await userManagementService.CreateAsync(new CreateManagedUserRequest(
                CurrentUserId(), model.FullName, model.Email, model.Password, model.RoleName), cancellationToken);
            TempData["StatusMessage"] = "Đã tạo tài khoản.";
            return RedirectToAction(nameof(Index));
        }
        catch (UserManagementException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var user = await userManagementService.FindAsync(id, cancellationToken);
        return user is null ? NotFound() : View(EditUserViewModel.From(user));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await userManagementService.UpdateAsync(new UpdateManagedUserRequest(
                CurrentUserId(), model.Id, model.ConcurrencyStamp, model.FullName, model.Email), cancellationToken);
            TempData["StatusMessage"] = "Đã cập nhật tài khoản.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }
        catch (UserManagementException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(EditUserViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await userManagementService.SetActiveAsync(new UserActivationRequest(
                CurrentUserId(), model.Id, model.ConcurrencyStamp, model.IsActive), cancellationToken);
            TempData["StatusMessage"] = model.IsActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản và thu hồi phiên đăng nhập.";
        }
        catch (UserManagementException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(ChangeUserRoleViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu đổi role không hợp lệ.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        try
        {
            await userManagementService.ChangeRoleAsync(new ChangeUserRoleRequest(
                CurrentUserId(), model.Id, model.ConcurrencyStamp, model.RoleName), cancellationToken);
            TempData["StatusMessage"] = "Đã đổi role và thu hồi phiên đăng nhập.";
        }
        catch (UserManagementException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Mật khẩu mới không hợp lệ.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        try
        {
            await userManagementService.ResetPasswordAsync(new ResetPasswordRequest(
                CurrentUserId(), model.Id, model.ConcurrencyStamp, model.NewPassword), cancellationToken);
            TempData["StatusMessage"] = "Đã đặt lại mật khẩu và thu hồi phiên đăng nhập.";
        }
        catch (UserManagementException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated request is missing its user identifier.");
}
