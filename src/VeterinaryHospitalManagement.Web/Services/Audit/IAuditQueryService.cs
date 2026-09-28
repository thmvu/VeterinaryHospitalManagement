using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Audit;

namespace VeterinaryHospitalManagement.Web.Services.Audit;

public interface IAuditQueryService
{
    Task<AuditLogIndexViewModel> QueryAuditLogsAsync(
        AuditLogIndexViewModel filter,
        CancellationToken cancellationToken = default);
}
