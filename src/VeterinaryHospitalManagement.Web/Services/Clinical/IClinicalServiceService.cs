using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Clinical;

public sealed record AddClinicalServiceRequest(int VisitId, string ActorUserId, int ServiceCatalogId, decimal Quantity);
public sealed record PerformClinicalServiceRequest(int LineId, string ActorUserId, byte[] ExpectedRowVersion);
public sealed record CancelClinicalServiceRequest(int LineId, string ActorUserId, byte[] ExpectedRowVersion, string Reason);
public sealed record ClinicalServiceLine(int Id, int VisitId, string Name, decimal Quantity, decimal UnitPrice,
    VisitServiceStatus Status, string? CancellationReason, DateTimeOffset? PerformedAt, byte[] RowVersion);

public interface IClinicalServiceService
{
    Task<IReadOnlyList<ClinicalServiceLine>> ListAsync(int visitId, CancellationToken ct = default);
    Task<int> AddAsync(AddClinicalServiceRequest request, CancellationToken ct = default);
    Task PerformAsync(PerformClinicalServiceRequest request, CancellationToken ct = default);
    Task CancelAsync(CancelClinicalServiceRequest request, CancellationToken ct = default);
}

public sealed class ClinicalServiceManagementException(string message) : Exception(message);
public sealed class ClinicalServiceAccessException() : Exception("Chỉ bác sĩ phụ trách mới được thay đổi dịch vụ của lượt khám.");
