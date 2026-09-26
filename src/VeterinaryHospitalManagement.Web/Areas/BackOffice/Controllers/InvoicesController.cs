using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Invoices;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Billing;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[Authorize]
public sealed class InvoicesController(ICheckoutService checkout, IAuthorizationService authorization) : Controller
{
    [HttpGet, PermissionAuthorize(PermissionCodes.InvoiceView)]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new InvoiceIndexViewModel
    {
        Unpaid = await checkout.ListUnpaidAsync(ct),
        Recent = await checkout.ListRecentAsync(ct)
    });

    [HttpGet, PermissionAuthorize(PermissionCodes.InvoiceView)]
    public async Task<IActionResult> Preview(int id, CancellationToken ct)
    {
        try
        {
            var preview = await checkout.PreviewAsync(id, ct);
            if (preview.ExistingInvoiceId is { } existingId)
                return RedirectToAction(nameof(Details), new { id = existingId });
            var canCheckout = (await authorization.AuthorizeAsync(User,
                PermissionPolicyName.For(PermissionCodes.InvoiceCheckout))).Succeeded;
            return View(new CheckoutPageViewModel { Preview = preview, CanCheckout = canCheckout });
        }
        catch (CheckoutManagementException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.InvoiceCheckout)]
    public async Task<IActionResult> Confirm(ConfirmCheckoutViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Preview), new { id = model.VisitId });
        }
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var invoiceId = await checkout.ConfirmAsync(new(model.VisitId, userId, model.PaymentMethod), ct);
            TempData["StatusMessage"] = "Đã xác nhận thanh toán.";
            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }
        catch (CheckoutManagementException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Preview), new { id = model.VisitId });
        }
    }

    [HttpGet, PermissionAuthorize(PermissionCodes.InvoiceView)]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var invoice = await checkout.FindAsync(id, ct);
        return invoice is null ? NotFound() : View(invoice);
    }
}
