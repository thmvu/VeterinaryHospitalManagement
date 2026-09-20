namespace VeterinaryHospitalManagement.Web.Services.Veterinarians;

public interface IVeterinarianProfileService
{
    Task<IReadOnlyList<VeterinarianListItem>> ListAsync(CancellationToken cancellationToken = default);
    Task<VeterinarianDetails?> FindAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EligibleVeterinarianUser>> EligibleUsersAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CreateVeterinarianRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateVeterinarianRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(VeterinarianActivationRequest request, CancellationToken cancellationToken = default);
}
public sealed record VeterinarianListItem(int Id,string DoctorCode,string FullName,string? Specialty,bool IsActive);
public sealed record VeterinarianDetails(int Id,string UserId,string DoctorCode,string FullName,string? Specialty,bool IsActive,byte[] RowVersion);
public sealed record EligibleVeterinarianUser(string Id,string FullName,string Email);
public sealed record CreateVeterinarianRequest(string ActorUserId,string UserId,string DoctorCode,string? Specialty);
public sealed record UpdateVeterinarianRequest(string ActorUserId,int Id,byte[] ExpectedRowVersion,string? Specialty);
public sealed record VeterinarianActivationRequest(string ActorUserId,int Id,byte[] ExpectedRowVersion,bool IsActive);
public class VeterinarianManagementException(string message) : InvalidOperationException(message);
public sealed class VeterinarianConcurrencyException() : VeterinarianManagementException("Hồ sơ bác sĩ đã được thay đổi. Hãy tải lại trang.");
