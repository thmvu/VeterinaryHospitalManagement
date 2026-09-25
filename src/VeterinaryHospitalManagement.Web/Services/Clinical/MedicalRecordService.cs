using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Clinical;

public sealed class MedicalRecordService(ApplicationDbContext db, TimeProvider clock) : IMedicalRecordService
{
    public Task<MedicalRecordDetails?> FindByVisitAsync(int visitId, CancellationToken ct = default) =>
        db.MedicalRecords.AsNoTracking().Where(x => x.VisitId == visitId)
            .Select(x => new MedicalRecordDetails(x.Id, x.VisitId, x.Visit.VisitNumber,
                x.Visit.PetNameSnapshot, x.Visit.VeterinarianNameSnapshot, x.Visit.Status.ToString(),
                x.ChiefComplaint, x.Symptoms, x.WeightKg, x.TemperatureC, x.Diagnosis,
                x.TreatmentNotes, x.FollowUpDate, x.Status.ToString(), x.RowVersion))
            .SingleOrDefaultAsync(ct);

    public async Task<int> SaveDraftAsync(SaveMedicalRecordDraftRequest request, CancellationToken ct = default)
    {
        var chiefComplaint = MedicalRecordRules.NormalizeChiefComplaint(request.ChiefComplaint);
        MedicalRecordRules.ValidateMeasurements(request.WeightKg, request.TemperatureC);
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var visit = await db.Visits.Include(x => x.Veterinarian).ThenInclude(x => x.User)
                    .SingleOrDefaultAsync(x => x.Id == request.VisitId, ct)
                    ?? throw new MedicalRecordManagementException("Không tìm thấy lượt khám.");
                if (visit.Veterinarian.UserId != request.ActorUserId ||
                    !visit.Veterinarian.IsActive || !visit.Veterinarian.User.IsActive)
                    throw new MedicalRecordAccessException();
                if (visit.Status != VisitStatus.InProgress)
                    throw new MedicalRecordManagementException("Chỉ có thể ghi bệnh án khi lượt khám đang diễn ra.");

                var record = await db.MedicalRecords.SingleOrDefaultAsync(x => x.VisitId == visit.Id, ct);
                if (record is null)
                {
                    if (request.ExpectedRowVersion is { Length: > 0 })
                        throw new MedicalRecordManagementException("Bệnh án đã thay đổi. Hãy tải lại trang.");
                    record = new MedicalRecord { VisitId = visit.Id, Status = ClinicalDocumentStatus.Draft };
                    db.MedicalRecords.Add(record);
                }
                else
                {
                    if (record.Status != ClinicalDocumentStatus.Draft)
                        throw new MedicalRecordManagementException("Bệnh án đã chốt, không thể sửa bằng biểu mẫu này.");
                    if (request.ExpectedRowVersion is null ||
                        !record.RowVersion.AsSpan().SequenceEqual(request.ExpectedRowVersion))
                        throw new MedicalRecordManagementException("Bệnh án đã thay đổi. Hãy tải lại trang.");
                    db.Entry(record).Property(x => x.RowVersion).OriginalValue = request.ExpectedRowVersion;
                }

                record.ChiefComplaint = chiefComplaint;
                record.Symptoms = NormalizeOptional(request.Symptoms);
                record.WeightKg = request.WeightKg;
                record.TemperatureC = request.TemperatureC;
                record.Diagnosis = NormalizeOptional(request.Diagnosis);
                record.TreatmentNotes = NormalizeOptional(request.TreatmentNotes);
                record.FollowUpDate = request.FollowUpDate;
                await db.SaveChangesAsync(ct);
                db.AuditLogs.Add(new AuditLog
                {
                    ActorType = "Internal", UserId = request.ActorUserId,
                    Action = "MedicalRecord.DraftSaved", EntityName = "MedicalRecord",
                    EntityId = record.Id.ToString(CultureInfo.InvariantCulture),
                    Description = $"Saved draft medical record for visit {visit.VisitNumber}.",
                    CreatedAt = clock.GetUtcNow()
                });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return record.Id;
            });
        }
        catch (DbUpdateConcurrencyException)
        { throw new MedicalRecordManagementException("Bệnh án đã thay đổi. Hãy tải lại trang."); }
        catch (DbUpdateException ex) when (FindSql(ex) is { Number: 2601 or 2627 })
        { throw new MedicalRecordManagementException("Bệnh án vừa được tạo bởi yêu cầu khác. Hãy tải lại trang."); }
        catch (SqlException ex) when (ex.Number == 1205)
        { throw new MedicalRecordManagementException("Dữ liệu bệnh án đang được cập nhật. Hãy thử lại."); }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static SqlException? FindSql(Exception? ex)
    { while (ex is not null) { if (ex is SqlException sql) return sql; ex = ex.InnerException; } return null; }
}
