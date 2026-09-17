using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Services.Pets;

public sealed class PetService(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider,
    IVietnamTimeProvider vietnamTimeProvider) : IPetService
{
    public async Task<PetDetails?> FindAsync(int petId, CancellationToken cancellationToken = default)
    {
        var pet = await dbContext.Pets
            .AsNoTracking()
            .Include(p => p.Owner)
            .Include(p => p.Species)
            .Include(p => p.Breed)
            .SingleOrDefaultAsync(p => p.Id == petId, cancellationToken);

        if (pet is null)
        {
            return null;
        }

        return new PetDetails(
            pet.Id,
            pet.PetCode,
            pet.OwnerId,
            pet.Owner.OwnerCode,
            pet.Owner.FullName,
            pet.Owner.PhoneNumber,
            pet.Name,
            pet.SpeciesId,
            pet.Species.Name,
            pet.BreedId,
            pet.Breed?.Name,
            pet.Sex,
            pet.BirthDate,
            pet.Color,
            pet.Notes,
            pet.IsActive,
            pet.CreatedAt,
            pet.RowVersion.ToArray());
    }

    public Task<string> CreateAsync(CreatePetRequest request, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var name = PetRules.NormalizeName(request.Name);
            var color = PetRules.NormalizeOptional(request.Color, 100, "Màu lông");
            var notes = PetRules.NormalizeOptional(request.Notes, 1000, "Ghi chú");
            var vietnamToday = DateOnly.FromDateTime(vietnamTimeProvider.LocalNow.DateTime);
            PetRules.ValidateBirthDate(request.BirthDate, vietnamToday);

            var owner = await dbContext.Owners.SingleOrDefaultAsync(o => o.Id == request.OwnerId, cancellationToken)
                ?? throw new PetManagementException("Không tìm thấy chủ nuôi.");

            if (!owner.IsActive)
            {
                throw new PetManagementException("Không thể thêm thú cưng cho chủ nuôi đang bị khóa.");
            }

            var species = await dbContext.Species.SingleOrDefaultAsync(s => s.Id == request.SpeciesId, cancellationToken)
                ?? throw new PetManagementException("Loài thú cưng không tồn tại.");

            if (!species.IsActive)
            {
                throw new PetManagementException("Loài thú cưng đã ngừng hoạt động.");
            }

            if (request.BreedId.HasValue)
            {
                var breed = await dbContext.Breeds.SingleOrDefaultAsync(b => b.Id == request.BreedId.Value, cancellationToken)
                    ?? throw new PetManagementException("Không tìm thấy giống thú cưng.");

                if (breed.SpeciesId != request.SpeciesId)
                {
                    throw new PetManagementException("Giống không thuộc loài đã chọn.");
                }

                if (!breed.IsActive)
                {
                    throw new PetManagementException("Giống thú cưng đã ngừng hoạt động.");
                }
            }

            var sequenceValue = await ReserveNextPetCodeAsync(cancellationToken);
            var pet = new Pet
            {
                PetCode = PetRules.FormatPetCode(sequenceValue),
                OwnerId = request.OwnerId,
                Name = name,
                SpeciesId = request.SpeciesId,
                BreedId = request.BreedId,
                Sex = request.Sex,
                BirthDate = request.BirthDate,
                Color = color,
                Notes = notes,
                IsActive = true,
                CreatedAt = timeProvider.GetUtcNow()
            };

            dbContext.Pets.Add(pet);
            await dbContext.SaveChangesAsync(cancellationToken);

            AddAudit(request.ActorUserId, "Pet.Created", pet.Id, $"Created pet {pet.PetCode} ({pet.Name}) for owner {owner.OwnerCode}.");
            await dbContext.SaveChangesAsync(cancellationToken);

            return pet.PetCode;
        }, cancellationToken);

    public Task UpdateAsync(UpdatePetRequest request, CancellationToken cancellationToken = default) =>
        GuardConcurrentMutationAsync(() => InSerializableTransactionAsync(async () =>
        {
            var pet = await LoadPetAsync(request.PetId, request.ExpectedRowVersion, cancellationToken);

            var name = PetRules.NormalizeName(request.Name);
            var color = PetRules.NormalizeOptional(request.Color, 100, "Màu lông");
            var notes = PetRules.NormalizeOptional(request.Notes, 1000, "Ghi chú");
            var vietnamToday = DateOnly.FromDateTime(vietnamTimeProvider.LocalNow.DateTime);
            PetRules.ValidateBirthDate(request.BirthDate, vietnamToday);

            var species = await dbContext.Species.SingleOrDefaultAsync(s => s.Id == request.SpeciesId, cancellationToken)
                ?? throw new PetManagementException("Loài thú cưng không tồn tại.");

            if (!species.IsActive && pet.SpeciesId != request.SpeciesId)
            {
                throw new PetManagementException("Không thể chuyển thú cưng sang loài đã ngừng hoạt động.");
            }

            if (request.BreedId.HasValue)
            {
                var breed = await dbContext.Breeds.SingleOrDefaultAsync(b => b.Id == request.BreedId.Value, cancellationToken)
                    ?? throw new PetManagementException("Không tìm thấy giống thú cưng.");

                if (breed.SpeciesId != request.SpeciesId)
                {
                    throw new PetManagementException("Giống không thuộc loài đã chọn.");
                }

                if (!breed.IsActive && pet.BreedId != request.BreedId)
                {
                    throw new PetManagementException("Không thể chọn giống thú cưng đã ngừng hoạt động.");
                }
            }

            pet.Name = name;
            pet.SpeciesId = request.SpeciesId;
            pet.BreedId = request.BreedId;
            pet.Sex = request.Sex;
            pet.BirthDate = request.BirthDate;
            pet.Color = color;
            pet.Notes = notes;

            AddAudit(request.ActorUserId, "Pet.Updated", pet.Id, $"Updated pet {pet.PetCode} ({pet.Name}).");
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken));

    public Task SetActiveAsync(PetActivationRequest request, CancellationToken cancellationToken = default) =>
        GuardConcurrentMutationAsync(() => InSerializableTransactionAsync(async () =>
        {
            var pet = await LoadPetAsync(request.PetId, request.ExpectedRowVersion, cancellationToken);
            pet.IsActive = request.IsActive;

            AddAudit(
                request.ActorUserId,
                request.IsActive ? "Pet.Activated" : "Pet.Deactivated",
                pet.Id,
                request.IsActive ? $"Activated pet {pet.PetCode}." : $"Deactivated pet {pet.PetCode}.");

            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken));

    public async Task<IReadOnlyList<SpeciesOption>> GetActiveSpeciesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Species
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SpeciesOption(s.Id, s.Code, s.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BreedOption>> GetBreedsBySpeciesAsync(int speciesId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Breeds
            .AsNoTracking()
            .Where(b => b.SpeciesId == speciesId && b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BreedOption(b.Id, b.SpeciesId, b.Name))
            .ToListAsync(cancellationToken);
    }

    private async Task<Pet> LoadPetAsync(int petId, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        var pet = await dbContext.Pets.SingleOrDefaultAsync(p => p.Id == petId, cancellationToken)
            ?? throw new PetManagementException("Không tìm thấy thú cưng.");

        if (!pet.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
        {
            throw new PetConcurrencyException();
        }

        return pet;
    }

    private async Task<long> ReserveNextPetCodeAsync(CancellationToken cancellationToken)
    {
        var parameter = new SqlParameter
        {
            ParameterName = "@SequenceValue",
            SqlDbType = SqlDbType.BigInt,
            Direction = ParameterDirection.Output
        };

        await dbContext.Database.ExecuteSqlRawAsync(
            "SELECT @SequenceValue = NEXT VALUE FOR [PetCodeSequence];",
            [parameter],
            cancellationToken);

        return Convert.ToInt64(parameter.Value, CultureInfo.InvariantCulture);
    }

    private async Task<TResult> InSerializableTransactionAsync<TResult>(
        Func<Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static async Task<TResult> GuardConcurrentMutationAsync<TResult>(Func<Task<TResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (IsConcurrentMutation(exception))
        {
            throw new PetConcurrencyException();
        }
    }

    private static bool IsConcurrentMutation(Exception exception) =>
        exception is DbUpdateConcurrencyException
        || (exception is DbUpdateException dbUpdate
            && dbUpdate.InnerException is SqlException sql
            && sql.Number is 1205 or 2601 or 2627);

    private void AddAudit(string actorUserId, string action, int petId, string description)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorType = "Internal",
            UserId = actorUserId,
            Action = action,
            EntityName = "Pet",
            EntityId = petId.ToString(CultureInfo.InvariantCulture),
            Description = description,
            CreatedAt = timeProvider.GetUtcNow()
        });
    }
}
