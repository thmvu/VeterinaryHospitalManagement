using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Medicines;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Catalogs;
namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;
[Area("BackOffice"),Authorize,PermissionAuthorize(PermissionCodes.CatalogManage)]
public sealed class MedicinesController(IMedicineService service):Controller
{
 [HttpGet]public async Task<IActionResult> Index(CancellationToken ct)=>View(await service.ListAsync(ct));
 [HttpGet]public IActionResult Create()=>View(new MedicineFormViewModel());
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Create(MedicineFormViewModel m,CancellationToken ct){if(!ModelState.IsValid)return View(m);try{await service.CreateAsync(new(Current(),m.Code,m.Name,m.ActiveIngredient,m.Strength,m.Unit),ct);TempData["StatusMessage"]="Đã tạo thuốc.";return RedirectToAction(nameof(Index));}catch(MedicineManagementException e){ModelState.AddModelError(string.Empty,e.Message);return View(m);}}
 [HttpGet]public async Task<IActionResult>Edit(int id,CancellationToken ct){var x=await service.FindAsync(id,ct);return x is null?NotFound():View(MedicineFormViewModel.From(x));}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Edit(MedicineFormViewModel m,CancellationToken ct){if(!ModelState.IsValid)return await Reload(m,ct);try{await service.UpdateAsync(new(Current(),m.Id,Decode(m.RowVersion),m.Name,m.ActiveIngredient,m.Strength,m.Unit),ct);TempData["StatusMessage"]="Đã cập nhật thuốc.";return RedirectToAction(nameof(Edit),new{id=m.Id});}catch(MedicineManagementException e){ModelState.AddModelError(string.Empty,e.Message);return await Reload(m,ct);}}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>SetActive(MedicineFormViewModel m,CancellationToken ct){try{await service.SetActiveAsync(new(Current(),m.Id,Decode(m.RowVersion),m.IsActive),ct);TempData["StatusMessage"]=m.IsActive?"Đã mở lại thuốc.":"Đã tạm ngưng thuốc.";}catch(MedicineManagementException e){TempData["ErrorMessage"]=e.Message;}return RedirectToAction(nameof(Edit),new{id=m.Id});}
 private async Task<IActionResult>Reload(MedicineFormViewModel m,CancellationToken ct){var x=await service.FindAsync(m.Id,ct);if(x is null)return NotFound();m.Code=x.Code;m.IsActive=x.IsActive;m.RowVersion=Convert.ToBase64String(x.RowVersion);return View(m);}
 private string Current()=>User.FindFirstValue(ClaimTypes.NameIdentifier)??throw new InvalidOperationException();
 private static byte[] Decode(string value){try{return Convert.FromBase64String(value);}catch(FormatException){throw new MedicineManagementException("Dữ liệu phiên bản không hợp lệ. Hãy tải lại trang.");}}
}
