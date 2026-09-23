namespace VeterinaryHospitalManagement.Web.Services.Catalogs;

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceCatalogListItem>> ListAsync(CancellationToken cancellationToken = default);
    Task<ServiceCatalogDetails?> FindAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CreateServiceCatalogRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateServiceCatalogRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(ServiceCatalogActivationRequest request, CancellationToken cancellationToken = default);
}

public sealed record ServiceCatalogListItem(int Id, string Code, string Name, string Category, decimal Price, bool IsActive);
public sealed record ServiceCatalogDetails(int Id, string Code, string Name, string Category, decimal Price, string? Description, bool IsActive, byte[] RowVersion);
public sealed record CreateServiceCatalogRequest(string ActorUserId, string? Code, string? Name, string? Category, decimal Price, string? Description);
public sealed record UpdateServiceCatalogRequest(string ActorUserId, int Id, byte[] ExpectedRowVersion, string? Name, string? Category, decimal Price, string? Description);
public sealed record ServiceCatalogActivationRequest(string ActorUserId, int Id, byte[] ExpectedRowVersion, bool IsActive);
