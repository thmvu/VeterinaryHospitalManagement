namespace VeterinaryHospitalManagement.Web.Services.Clinical;

public sealed record MedicalRecordDetails(int Id, int VisitId, string VisitNumber, string PetName,
    string VeterinarianName, string VisitStatus, string ChiefComplaint, string? Symptoms,
    decimal? WeightKg, decimal? TemperatureC, string? Diagnosis, string? TreatmentNotes,
    DateOnly? FollowUpDate, string Status, byte[] RowVersion);

public sealed record SaveMedicalRecordDraftRequest(int VisitId, string ActorUserId,
    byte[]? ExpectedRowVersion, string ChiefComplaint, string? Symptoms, decimal? WeightKg,
    decimal? TemperatureC, string? Diagnosis, string? TreatmentNotes, DateOnly? FollowUpDate);

public interface IMedicalRecordService
{
    Task<MedicalRecordDetails?> FindByVisitAsync(int visitId, CancellationToken ct = default);
    Task<int> SaveDraftAsync(SaveMedicalRecordDraftRequest request, CancellationToken ct = default);
}

public sealed class MedicalRecordManagementException(string message) : Exception(message);
public sealed class MedicalRecordAccessException() : Exception("Chỉ bác sĩ được phân công mới được ghi bệnh án.");
