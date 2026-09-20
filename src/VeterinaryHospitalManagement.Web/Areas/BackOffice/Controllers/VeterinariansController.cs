using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Veterinarians;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Veterinarians;
namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;
[Area("BackOffice"),Authorize,PermissionAuthorize(PermissionCodes.CatalogManage)]
public sealed class VeterinariansController(IVeterinarianProfileService service):Controller
{
 [HttpGet] public async Task<IActionResult> Index(CancellationToken ct)=>View(await service.ListAsync(ct));
 [HttpGet] public async Task<IActionResult> Create(CancellationToken ct){var m=new CreateVeterinarianViewModel();await Choices(m,ct);return View(m);}
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Create(CreateVeterinarianViewModel m,CancellationToken ct){if(!ModelState.IsValid){await Choices(m,ct);return View(m);}try{await service.CreateAsync(new(Current(),m.UserId,m.DoctorCode,m.Specialty),ct);TempData["StatusMessage"]="Đã tạo hồ sơ bác sĩ.";return RedirectToAction(nameof(Index));}catch(VeterinarianManagementException e){ModelState.AddModelError(string.Empty,e.Message);await Choices(m,ct);return View(m);}}
 [HttpGet] public async Task<IActionResult> Edit(int id,CancellationToken ct){var x=await service.FindAsync(id,ct);return x is null?NotFound():View(EditVeterinarianViewModel.From(x));}
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Edit(EditVeterinarianViewModel m,CancellationToken ct){if(!ModelState.IsValid)return await ReloadEdit(m,ct);try{await service.UpdateAsync(new(Current(),m.Id,Decode(m.RowVersion),m.Specialty),ct);TempData["StatusMessage"]="Đã cập nhật hồ sơ bác sĩ.";return RedirectToAction(nameof(Edit),new{id=m.Id});}catch(VeterinarianManagementException e){ModelState.AddModelError(string.Empty,e.Message);return await ReloadEdit(m,ct);}}
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> SetActive(EditVeterinarianViewModel m,CancellationToken ct){try{await service.SetActiveAsync(new(Current(),m.Id,Decode(m.RowVersion),m.IsActive),ct);TempData["StatusMessage"]="Đã cập nhật trạng thái bác sĩ.";}catch(VeterinarianManagementException e){TempData["ErrorMessage"]=e.Message;}return RedirectToAction(nameof(Edit),new{id=m.Id});}
 private async Task Choices(CreateVeterinarianViewModel m,CancellationToken ct)=>m.Users=(await service.EligibleUsersAsync(ct)).Select(x=>new SelectListItem($"{x.FullName} ({x.Email})",x.Id)).ToList();
 private async Task<IActionResult> ReloadEdit(EditVeterinarianViewModel m,CancellationToken ct){var current=await service.FindAsync(m.Id,ct);if(current is null)return NotFound();m.DoctorCode=current.DoctorCode;m.FullName=current.FullName;m.IsActive=current.IsActive;m.RowVersion=Convert.ToBase64String(current.RowVersion);return View(m);}
 private string Current()=>User.FindFirstValue(ClaimTypes.NameIdentifier)??throw new InvalidOperationException();
 private static byte[] Decode(string value){try{return Convert.FromBase64String(value);}catch(FormatException){throw new VeterinarianManagementException("Dữ liệu phiên bản không hợp lệ. Hãy tải lại trang.");}}
}
