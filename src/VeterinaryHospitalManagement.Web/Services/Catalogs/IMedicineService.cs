namespace VeterinaryHospitalManagement.Web.Services.Catalogs;
public interface IMedicineService
{
    Task<IReadOnlyList<MedicineListItem>> ListAsync(CancellationToken ct=default);
    Task<MedicineDetails?> FindAsync(int id,CancellationToken ct=default);
    Task<int> CreateAsync(CreateMedicineRequest request,CancellationToken ct=default);
    Task UpdateAsync(UpdateMedicineRequest request,CancellationToken ct=default);
    Task SetActiveAsync(MedicineActivationRequest request,CancellationToken ct=default);
}
public sealed record MedicineListItem(int Id,string Code,string Name,string? ActiveIngredient,string? Strength,string Unit,bool IsActive);
public sealed record MedicineDetails(int Id,string Code,string Name,string? ActiveIngredient,string? Strength,string Unit,bool IsActive,byte[] RowVersion);
public sealed record CreateMedicineRequest(string ActorUserId,string? Code,string? Name,string? ActiveIngredient,string? Strength,string? Unit);
public sealed record UpdateMedicineRequest(string ActorUserId,int Id,byte[] ExpectedRowVersion,string? Name,string? ActiveIngredient,string? Strength,string? Unit);
public sealed record MedicineActivationRequest(string ActorUserId,int Id,byte[] ExpectedRowVersion,bool IsActive);
