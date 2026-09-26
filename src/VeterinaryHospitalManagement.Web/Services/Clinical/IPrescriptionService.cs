namespace VeterinaryHospitalManagement.Web.Services.Clinical;

public sealed record PrescriptionItemDraft(int? Id, int MedicineId, string Dosage, string Route,
    string Frequency, string Duration, decimal Quantity, string? Instructions);

public sealed record SavePrescriptionDraftRequest(int VisitId, string ActorUserId, byte[]? ExpectedRowVersion,
    string? Instructions, IReadOnlyList<PrescriptionItemDraft> Items);

public sealed record PrescriptionItemDetails(int Id, int MedicineId, string MedicineName, string Unit,
    string Dosage, string Route, string Frequency, string Duration, decimal Quantity, string? Instructions);

public sealed record PrescriptionDetails(int Id, int VisitId, string VisitNumber, string PetName,
    string VeterinarianName, string VisitStatus, string Status, string? Instructions,
    byte[] RowVersion, IReadOnlyList<PrescriptionItemDetails> Items);

public interface IPrescriptionService
{
    Task<PrescriptionDetails?> FindByVisitAsync(int visitId, CancellationToken ct = default);
    Task<int> SaveDraftAsync(SavePrescriptionDraftRequest request, CancellationToken ct = default);
}

public sealed class PrescriptionManagementException(string message) : Exception(message);
public sealed class PrescriptionAccessException() : Exception("Chỉ bác sĩ phụ trách mới được sửa đơn thuốc.");
