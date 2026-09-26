using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Web.Services.Billing;

public sealed class CheckoutService(
    ApplicationDbContext db, IUserPermissionStore permissionStore, TimeProvider clock) : ICheckoutService
{
    private const decimal MaxSqlMoney = 9999999999999999.99m;

    public async Task<CheckoutPreview> PreviewAsync(int visitId, CancellationToken ct = default)
    {
        var visit = await db.Visits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == visitId, ct)
            ?? throw new CheckoutManagementException("Không tìm thấy lượt khám.");
        if (visit.Status != VisitStatus.Completed)
            throw new CheckoutManagementException("Chỉ có thể thu tiền cho lượt khám đã hoàn tất.");
        var existingId = await db.Invoices.AsNoTracking().Where(x => x.VisitId == visitId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
        var lines = await db.VisitServices.AsNoTracking()
            .Where(x => x.VisitId == visitId && x.Status == VisitServiceStatus.Performed)
            .OrderBy(x => x.Id).ToListAsync(ct);
        var items = lines.Select(ToCheckoutLine).ToList();
        return new CheckoutPreview(visit.Id, visit.VisitNumber, visit.OwnerNameSnapshot,
            visit.OwnerPhoneSnapshot, visit.PetNameSnapshot, items, Total(items), existingId);
    }

    public async Task<int> ConfirmAsync(ConfirmCheckoutRequest request, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.PaymentMethod))
            throw new CheckoutManagementException("Phương thức thanh toán không hợp lệ.");
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                // Khóa Visit trước khi kiểm tra Invoice để hai request đồng thời không cùng tạo hóa đơn.
                var visit = await db.Visits.FromSqlInterpolated(
                    $"SELECT * FROM [Visits] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {request.VisitId}")
                    .SingleOrDefaultAsync(ct)
                    ?? throw new CheckoutManagementException("Không tìm thấy lượt khám.");
                var actor = await db.Users.SingleOrDefaultAsync(x => x.Id == request.ActorUserId, ct);
                var permission = await permissionStore.FindByUserIdAsync(request.ActorUserId, ct);
                if (actor is null || !actor.IsActive || permission is null || !permission.IsActive ||
                    (permission.RoleName != SystemRoleNames.Admin &&
                     !permission.PermissionCodes.Contains(PermissionCodes.InvoiceCheckout)))
                    throw new CheckoutManagementException("Tài khoản không có quyền xác nhận thanh toán.");
                if (visit.Status != VisitStatus.Completed)
                    throw new CheckoutManagementException("Chỉ có thể thu tiền cho lượt khám đã hoàn tất.");
                var existingId = await db.Invoices.Where(x => x.VisitId == visit.Id)
                    .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
                if (existingId.HasValue) return existingId.Value;

                var services = await db.VisitServices
                    .Where(x => x.VisitId == visit.Id && x.Status == VisitServiceStatus.Performed)
                    .OrderBy(x => x.Id).ToListAsync(ct);
                if (await db.VisitServices.AnyAsync(x => x.VisitId == visit.Id &&
                    x.Status == VisitServiceStatus.Pending, ct))
                    throw new CheckoutManagementException("Còn dịch vụ chưa được xử lý.");
                var items = services.Select(ToCheckoutLine).ToList();
                var now = clock.GetUtcNow();
                var invoice = new Invoice
                {
                    InvoiceNumber = await NextInvoiceNumberAsync(ct),
                    VisitId = visit.Id,
                    OwnerNameSnapshot = visit.OwnerNameSnapshot,
                    OwnerPhoneSnapshot = visit.OwnerPhoneSnapshot,
                    PetNameSnapshot = visit.PetNameSnapshot,
                    TotalAmount = Total(items),
                    PaymentMethod = request.PaymentMethod,
                    PaidAt = now,
                    ProcessedByUserId = actor.Id,
                    ProcessedByNameSnapshot = actor.FullName,
                    Items = items.Select(x => new InvoiceItem
                    {
                        VisitServiceId = x.VisitServiceId, DescriptionSnapshot = x.Description,
                        Quantity = x.Quantity, UnitPrice = x.UnitPrice, LineTotal = x.LineTotal
                    }).ToList()
                };
                db.Invoices.Add(invoice);
                await db.SaveChangesAsync(ct);
                db.AuditLogs.Add(new AuditLog
                {
                    ActorType = "Internal", UserId = actor.Id, Action = "Invoice.Paid",
                    EntityName = "Invoice", EntityId = invoice.Id.ToString(CultureInfo.InvariantCulture),
                    Description = $"Invoice {invoice.InvoiceNumber} paid for visit {visit.VisitNumber}.",
                    CreatedAt = now
                });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return invoice.Id;
            });
        }
        catch (SqlException ex) when (ex.Number == 1205)
        { throw new CheckoutManagementException("Thanh toán đang được xử lý. Hãy tải lại trang."); }
        catch (OverflowException)
        { throw new CheckoutManagementException("Tổng tiền vượt giới hạn lưu trữ."); }
    }

    public async Task<InvoiceDetails?> FindAsync(int invoiceId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.AsNoTracking().Include(x => x.Visit).Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == invoiceId, ct);
        if (invoice is null) return null;
        return new InvoiceDetails(invoice.Id, invoice.InvoiceNumber, invoice.VisitId, invoice.Visit.VisitNumber,
            invoice.OwnerNameSnapshot, invoice.OwnerPhoneSnapshot, invoice.PetNameSnapshot,
            invoice.ProcessedByNameSnapshot, invoice.PaidAt, invoice.PaymentMethod,
            invoice.Items.OrderBy(x => x.Id).Select(x => new CheckoutLine(
                x.VisitServiceId, x.DescriptionSnapshot, x.Quantity, x.UnitPrice, x.LineTotal)).ToList(),
            invoice.TotalAmount);
    }

    public async Task<IReadOnlyList<UnpaidVisit>> ListUnpaidAsync(CancellationToken ct = default)
    {
        var visits = await db.Visits.AsNoTracking()
            .Where(x => x.Status == VisitStatus.Completed && x.Invoice == null)
            .Include(x => x.Services.Where(s => s.Status == VisitServiceStatus.Performed))
            .OrderBy(x => x.CompletedAt).ToListAsync(ct);
        return visits.Select(x => new UnpaidVisit(x.Id, x.VisitNumber, x.OwnerNameSnapshot,
            x.PetNameSnapshot, x.CompletedAt!.Value,
            Total(x.Services.Select(ToCheckoutLine)))).ToList();
    }

    public async Task<IReadOnlyList<PaidInvoice>> ListRecentAsync(CancellationToken ct = default) =>
        await db.Invoices.AsNoTracking().OrderByDescending(x => x.PaidAt).Take(50)
            .Select(x => new PaidInvoice(x.Id, x.InvoiceNumber, x.Visit.VisitNumber,
                x.OwnerNameSnapshot, x.TotalAmount, x.PaidAt)).ToListAsync(ct);

    private static CheckoutLine ToCheckoutLine(VisitService service)
    {
        try
        {
            var total = decimal.Round(checked(service.Quantity * service.UnitPrice), 0, MidpointRounding.AwayFromZero);
            if (total < 0 || total > MaxSqlMoney)
                throw new CheckoutManagementException("Tổng tiền dòng dịch vụ vượt giới hạn lưu trữ.");
            return new CheckoutLine(service.Id, service.ServiceNameSnapshot, service.Quantity, service.UnitPrice, total);
        }
        catch (OverflowException)
        { throw new CheckoutManagementException("Tổng tiền dòng dịch vụ vượt giới hạn lưu trữ."); }
    }

    private static decimal Total(IEnumerable<CheckoutLine> lines)
    {
        try
        {
            var total = lines.Aggregate(0m, (amount, line) => checked(amount + line.LineTotal));
            if (total > MaxSqlMoney) throw new CheckoutManagementException("Tổng hóa đơn vượt giới hạn lưu trữ.");
            return total;
        }
        catch (OverflowException) { throw new CheckoutManagementException("Tổng hóa đơn vượt giới hạn lưu trữ."); }
    }

    private async Task<string> NextInvoiceNumberAsync(CancellationToken ct)
    {
        var parameter = new SqlParameter("@SequenceValue", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlRawAsync(
            "SELECT @SequenceValue = NEXT VALUE FOR [InvoiceNumberSequence];", [parameter], ct);
        var sequence = Convert.ToInt64(parameter.Value, CultureInfo.InvariantCulture);
        return $"INV-{clock.GetUtcNow():yyyyMMdd}-{sequence:D6}";
    }
}
