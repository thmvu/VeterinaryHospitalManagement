using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Pets;

public interface IPetService
{
    Task<PetDetails?> FindAsync(int petId, CancellationToken cancellationToken = default);

    Task<string> CreateAsync(CreatePetRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(UpdatePetRequest request, CancellationToken cancellationToken = default);

    Task SetActiveAsync(PetActivationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpeciesOption>> GetActiveSpeciesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BreedOption>> GetBreedsBySpeciesAsync(int speciesId, CancellationToken cancellationToken = default);
}

public sealed record SpeciesOption(int Id, string Code, string Name);

public sealed record BreedOption(int Id, int SpeciesId, string Name);

public sealed record PetDetails(
    int Id,
    string PetCode,
    int OwnerId,
    string OwnerCode,
    string OwnerFullName,
    string OwnerPhoneNumber,
    string Name,
    int SpeciesId,
    string SpeciesName,
    int? BreedId,
    string? BreedName,
    PetSex Sex,
    DateOnly? BirthDate,
    string? Color,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    byte[] RowVersion);

public sealed record CreatePetRequest(
    string ActorUserId,
    int OwnerId,
    string Name,
    int SpeciesId,
    int? BreedId,
    PetSex Sex,
    DateOnly? BirthDate,
    string? Color,
    string? Notes);

public sealed record UpdatePetRequest(
    string ActorUserId,
    int PetId,
    byte[] ExpectedRowVersion,
    string Name,
    int SpeciesId,
    int? BreedId,
    PetSex Sex,
    DateOnly? BirthDate,
    string? Color,
    string? Notes);

public sealed record PetActivationRequest(
    string ActorUserId,
    int PetId,
    byte[] ExpectedRowVersion,
    bool IsActive);

public class PetManagementException(string message) : InvalidOperationException(message);

public sealed class PetConcurrencyException() :
    PetManagementException("Thú cưng đã được người khác thay đổi. Hãy tải lại trang và thử lại.");
