using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Permissions;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
public sealed class PermissionsController(IRolePermissionAdminService permissionAdminService) : Controller
{
    [HttpGet]
    [PermissionAuthorize(PermissionCodes.PermissionManage)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await permissionAdminService.GetMatrixAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionCodes.PermissionManage)]
    public async Task<IActionResult> UpdateRole(UpdateRolePermissionsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu phân quyền không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await permissionAdminService.UpdateRolePermissionsAsync(
                model.RoleName,
                model.SelectedPermissions,
                actorUserId,
                cancellationToken);

            TempData["StatusMessage"] = $"Đã cập nhật phân quyền thành công cho vai trò '{model.RoleName}'.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
