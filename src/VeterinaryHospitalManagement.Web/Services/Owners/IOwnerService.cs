using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Owners;

public interface IOwnerService
{
    Task<IReadOnlyList<OwnerSearchItem>> SearchAsync(OwnerSearchCriteria criteria, CancellationToken cancellationToken = default);

    Task<OwnerDetails?> FindAsync(int ownerId, CancellationToken cancellationToken = default);

    Task<string> CreateAsync(CreateOwnerRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(UpdateOwnerRequest request, CancellationToken cancellationToken = default);

    Task SetActiveAsync(OwnerActivationRequest request, CancellationToken cancellationToken = default);
}

public sealed record OwnerSearchCriteria(string? PhoneNumber, string? OwnerCode);

public sealed record OwnerSearchItem(
    int Id,
    string OwnerCode,
    string FullName,
    string PhoneNumber,
    bool IsActive,
    int TotalPets);

public sealed record OwnerPetSummary(
    int Id,
    string PetCode,
    string Name,
    string SpeciesName,
    string? BreedName,
    PetSex Sex,
    DateOnly? BirthDate,
    bool IsActive);

public sealed record OwnerDetails(
    int Id,
    string OwnerCode,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? Address,
    bool IsActive,
    DateTimeOffset CreatedAt,
    byte[] RowVersion,
    IReadOnlyList<OwnerPetSummary> Pets);

public sealed record CreateOwnerRequest(
    string ActorUserId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? Address);

public sealed record UpdateOwnerRequest(
    string ActorUserId,
    int OwnerId,
    byte[] ExpectedRowVersion,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? Address);

public sealed record OwnerActivationRequest(
    string ActorUserId,
    int OwnerId,
    byte[] ExpectedRowVersion,
    bool IsActive);

public class OwnerManagementException(string message) : InvalidOperationException(message);

public sealed class OwnerConcurrencyException() :
    OwnerManagementException("Chủ nuôi đã được người khác thay đổi. Hãy tải lại trang và thử lại.");
