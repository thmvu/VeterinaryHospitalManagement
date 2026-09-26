using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Clinical;

public sealed class PrescriptionService(ApplicationDbContext db, TimeProvider clock) : IPrescriptionService
{
    public async Task<PrescriptionDetails?> FindByVisitAsync(int visitId, CancellationToken ct = default)
    {
        var prescription = await db.Prescriptions.AsNoTracking()
            .Include(x => x.Visit)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.VisitId == visitId, ct);
        if (prescription is null) return null;
        return new PrescriptionDetails(prescription.Id, visitId, prescription.Visit.VisitNumber,
            prescription.Visit.PetNameSnapshot, prescription.Visit.VeterinarianNameSnapshot,
            prescription.Visit.Status.ToString(), prescription.Status.ToString(), prescription.Instructions,
            prescription.RowVersion,
            prescription.Items.OrderBy(x => x.Id).Select(x => new PrescriptionItemDetails(x.Id, x.MedicineId,
                x.MedicineNameSnapshot, x.UnitSnapshot, x.Dosage, x.Route, x.Frequency,
                x.Duration, x.Quantity, x.Instructions)).ToList());
    }

    public async Task<int> SaveDraftAsync(SavePrescriptionDraftRequest request, CancellationToken ct = default)
    {
        if (request.Items is null) throw new PrescriptionManagementException("Danh sách thuốc không hợp lệ.");
        var instructions = Optional(request.Instructions, 2000, "Hướng dẫn chung");
        var itemIds = request.Items.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToList();
        if (itemIds.Count != itemIds.Distinct().Count())
            throw new PrescriptionManagementException("Dòng thuốc bị lặp. Hãy tải lại trang.");
        foreach (var item in request.Items) ValidateItem(item);

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var visit = await db.Visits.Include(x => x.Veterinarian).ThenInclude(x => x.User)
                    .SingleOrDefaultAsync(x => x.Id == request.VisitId, ct)
                    ?? throw new PrescriptionManagementException("Không tìm thấy lượt khám.");
                var hasVetRole = await db.UserRoles.AnyAsync(link => link.UserId == request.ActorUserId &&
                    db.Roles.Any(role => role.Id == link.RoleId && role.Name == SystemRoleNames.Veterinarian), ct);
                if (visit.Veterinarian.UserId != request.ActorUserId ||
                    !visit.Veterinarian.IsActive || !visit.Veterinarian.User.IsActive || !hasVetRole)
                    throw new PrescriptionAccessException();
                if (visit.Status != VisitStatus.InProgress)
                    throw new PrescriptionManagementException("Chỉ có thể sửa đơn khi lượt khám đang diễn ra.");

                var prescription = await db.Prescriptions.Include(x => x.Items)
                    .SingleOrDefaultAsync(x => x.VisitId == request.VisitId, ct);
                if (prescription is null)
                {
                    if (request.ExpectedRowVersion is { Length: > 0 } || itemIds.Count > 0)
                        throw new PrescriptionManagementException("Đơn thuốc đã thay đổi. Hãy tải lại trang.");
                    prescription = new Prescription { VisitId = visit.Id, CreatedAt = clock.GetUtcNow() };
                    db.Prescriptions.Add(prescription);
                }
                else
                {
                    if (prescription.Status != ClinicalDocumentStatus.Draft)
                        throw new PrescriptionManagementException("Đơn thuốc đã chốt, không thể sửa.");
                    if (request.ExpectedRowVersion is null ||
                        !prescription.RowVersion.AsSpan().SequenceEqual(request.ExpectedRowVersion))
                        throw new PrescriptionManagementException("Đơn thuốc đã thay đổi. Hãy tải lại trang.");
                    db.Entry(prescription).Property(x => x.RowVersion).OriginalValue = request.ExpectedRowVersion;
                }

                var existing = prescription.Items.ToDictionary(x => x.Id);
                if (itemIds.Any(id => !existing.ContainsKey(id)))
                    throw new PrescriptionManagementException("Dòng thuốc không thuộc đơn này. Hãy tải lại trang.");
                var medicineIds = request.Items.Select(x => x.MedicineId).Distinct().ToList();
                var medicines = await db.Medicines.Where(x => medicineIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);
                if (medicines.Count != medicineIds.Count)
                    throw new PrescriptionManagementException("Thuốc đã chọn không tồn tại.");

                foreach (var oldItem in existing.Values.Where(x => !itemIds.Contains(x.Id)))
                    db.PrescriptionItems.Remove(oldItem);
                foreach (var draft in request.Items)
                {
                    var medicine = medicines[draft.MedicineId];
                    var item = draft.Id.HasValue ? existing[draft.Id.Value] : new PrescriptionItem();
                    if (!medicine.IsActive && (!draft.Id.HasValue || item.MedicineId != draft.MedicineId))
                        throw new PrescriptionManagementException("Không thể thêm thuốc đã ngừng sử dụng.");
                    if (!draft.Id.HasValue) prescription.Items.Add(item);
                    if (!draft.Id.HasValue || item.MedicineId != draft.MedicineId)
                    {
                        item.MedicineId = medicine.Id;
                        item.MedicineNameSnapshot = medicine.Name;
                        item.UnitSnapshot = medicine.Unit;
                    }
                    item.Dosage = draft.Dosage.Trim();
                    item.Route = draft.Route.Trim();
                    item.Frequency = draft.Frequency.Trim();
                    item.Duration = draft.Duration.Trim();
                    item.Quantity = draft.Quantity;
                    item.Instructions = Optional(draft.Instructions, 1000, "Lưu ý dùng thuốc");
                }

                prescription.Instructions = instructions;
                if (prescription.Id != 0)
                    db.Entry(prescription).Property(x => x.Instructions).IsModified = true;
                await db.SaveChangesAsync(ct);
                db.AuditLogs.Add(new AuditLog { ActorType = "Internal", UserId = request.ActorUserId,
                    Action = "Prescription.DraftSaved", EntityName = "Prescription",
                    EntityId = prescription.Id.ToString(CultureInfo.InvariantCulture),
                    Description = $"Saved prescription draft for visit {visit.VisitNumber}.", CreatedAt = clock.GetUtcNow() });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return prescription.Id;
            });
        }
        catch (DbUpdateConcurrencyException)
        { throw new PrescriptionManagementException("Đơn thuốc đã thay đổi. Hãy tải lại trang."); }
        catch (DbUpdateException ex) when (FindSql(ex) is { Number: 2601 or 2627 })
        { throw new PrescriptionManagementException("Đơn thuốc vừa được tạo bởi yêu cầu khác. Hãy tải lại trang."); }
        catch (SqlException ex) when (ex.Number == 1205)
        { throw new PrescriptionManagementException("Đơn thuốc đang được cập nhật. Hãy thử lại."); }
    }

    private static void ValidateItem(PrescriptionItemDraft item)
    {
        if (item.MedicineId <= 0) throw new PrescriptionManagementException("Vui lòng chọn thuốc.");
        Required(item.Dosage, 200, "Liều dùng");
        Required(item.Route, 100, "Đường dùng");
        Required(item.Frequency, 200, "Tần suất");
        Required(item.Duration, 200, "Thời gian dùng");
        if (item.Quantity <= 0 || item.Quantity > 99999999.99m || decimal.Round(item.Quantity, 2) != item.Quantity)
            throw new PrescriptionManagementException("Số lượng thuốc phải lớn hơn 0 và có tối đa 2 chữ số thập phân.");
        Optional(item.Instructions, 1000, "Lưu ý dùng thuốc");
    }
    private static string Required(string? value, int max, string name) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > max
            ? throw new PrescriptionManagementException($"{name} là bắt buộc và tối đa {max} ký tự.") : value.Trim();
    private static string? Optional(string? value, int max, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max
            ? throw new PrescriptionManagementException($"{name} tối đa {max} ký tự.") : value.Trim();
    private static SqlException? FindSql(Exception? ex)
    { while (ex is not null) { if (ex is SqlException sql) return sql; ex = ex.InnerException; } return null; }
}
