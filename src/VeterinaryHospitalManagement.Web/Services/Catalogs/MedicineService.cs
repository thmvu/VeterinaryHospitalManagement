using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Services.Catalogs;
public sealed class MedicineService(ApplicationDbContext db,TimeProvider clock):IMedicineService
{
    public async Task<IReadOnlyList<MedicineListItem>> ListAsync(CancellationToken ct=default)=>await db.Medicines.AsNoTracking().OrderBy(x=>x.Code).Select(x=>new MedicineListItem(x.Id,x.Code,x.Name,x.ActiveIngredient,x.Strength,x.Unit,x.IsActive)).ToListAsync(ct);
    public async Task<MedicineDetails?> FindAsync(int id,CancellationToken ct=default)=>await db.Medicines.AsNoTracking().Where(x=>x.Id==id).Select(x=>new MedicineDetails(x.Id,x.Code,x.Name,x.ActiveIngredient,x.Strength,x.Unit,x.IsActive,x.RowVersion)).SingleOrDefaultAsync(ct);
    public Task<int> CreateAsync(CreateMedicineRequest r,CancellationToken ct=default)=>Transaction(async()=>
    {
        var code=MedicineRules.NormalizeCode(r.Code);
        if(await db.Medicines.AnyAsync(x=>x.Code==code,ct)) throw new MedicineManagementException("Mã thuốc đã tồn tại.");
        var item=new Medicine{Code=code,Name=MedicineRules.NormalizeName(r.Name),ActiveIngredient=MedicineRules.NormalizeActiveIngredient(r.ActiveIngredient),Strength=MedicineRules.NormalizeStrength(r.Strength),Unit=MedicineRules.NormalizeUnit(r.Unit),IsActive=true};
        db.Add(item);await db.SaveChangesAsync(ct);Audit(r.ActorUserId,"Medicine.Created",item);await db.SaveChangesAsync(ct);return item.Id;
    },ct);
    public Task UpdateAsync(UpdateMedicineRequest r,CancellationToken ct=default)=>Guard(()=>Transaction(async()=>
    {
        var item=await Load(r.Id,r.ExpectedRowVersion,ct);item.Name=MedicineRules.NormalizeName(r.Name);item.ActiveIngredient=MedicineRules.NormalizeActiveIngredient(r.ActiveIngredient);item.Strength=MedicineRules.NormalizeStrength(r.Strength);item.Unit=MedicineRules.NormalizeUnit(r.Unit);Audit(r.ActorUserId,"Medicine.Updated",item);await db.SaveChangesAsync(ct);return 0;
    },ct));
    public Task SetActiveAsync(MedicineActivationRequest r,CancellationToken ct=default)=>Guard(()=>Transaction(async()=>
    {
        var item=await Load(r.Id,r.ExpectedRowVersion,ct);item.IsActive=r.IsActive;Audit(r.ActorUserId,r.IsActive?"Medicine.Activated":"Medicine.Deactivated",item);await db.SaveChangesAsync(ct);return 0;
    },ct));
    private async Task<Medicine> Load(int id,byte[] version,CancellationToken ct){var item=await db.Medicines.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new MedicineManagementException("Không tìm thấy thuốc.");if(!item.RowVersion.AsSpan().SequenceEqual(version))throw new MedicineConcurrencyException();return item;}
    private void Audit(string actor,string action,Medicine item)=>db.AuditLogs.Add(new AuditLog{ActorType="Internal",UserId=actor,Action=action,EntityName="Medicine",EntityId=item.Id.ToString(CultureInfo.InvariantCulture),Description=$"Changed medicine {item.Code}.",CreatedAt=clock.GetUtcNow()});
    private async Task<T> Transaction<T>(Func<Task<T>> operation,CancellationToken ct)
    {
        var strategy=db.Database.CreateExecutionStrategy();try{return await strategy.ExecuteAsync(async()=>{await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);try{var result=await operation();await tx.CommitAsync(ct);return result;}catch(DbUpdateException e)when(e is not DbUpdateConcurrencyException){throw new MedicineManagementException("Không thể lưu thuốc; mã có thể đã tồn tại.");}});}catch(Exception e)when(Deadlock(e)){throw new MedicineManagementException("Không thể lưu thuốc; mã có thể đã tồn tại.");}
    }
    private static bool Deadlock(Exception e){for(Exception? x=e;x is not null;x=x.InnerException)if(x is SqlException{Number:1205})return true;return false;}
    private static async Task Guard(Func<Task> op){try{await op();}catch(DbUpdateConcurrencyException){throw new MedicineConcurrencyException();}}
}
