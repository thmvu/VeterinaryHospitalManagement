using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Services.Owners;

public sealed class OwnerService(
    ApplicationDbContext dbContext,
    IOwnerPhoneNormalizer phoneNormalizer,
    TimeProvider timeProvider) : IOwnerService
{
    public async Task<IReadOnlyList<OwnerSearchItem>> SearchAsync(OwnerSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var phoneNumber = string.IsNullOrWhiteSpace(criteria.PhoneNumber)
            ? null
            : criteria.PhoneNumber.Trim();
        var ownerCode = string.IsNullOrWhiteSpace(criteria.OwnerCode)
            ? null
            : criteria.OwnerCode.Trim().ToUpperInvariant();

        if (phoneNumber is null && ownerCode is null)
        {
            return [];
        }

        var query = dbContext.Owners.AsNoTracking();
        if (phoneNumber is not null)
        {
            var canonicalPhone = NormalizePhone(phoneNumber);
            query = query.Where(owner => owner.PhoneNumber == canonicalPhone);
        }

        if (ownerCode is not null)
        {
            query = query.Where(owner => owner.OwnerCode == ownerCode);
        }

        return await query
            .OrderBy(owner => owner.OwnerCode)
            .Select(owner => new OwnerSearchItem(
                owner.Id,
                owner.OwnerCode,
                owner.FullName,
                owner.PhoneNumber,
                owner.IsActive,
                owner.Pets.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<OwnerDetails?> FindAsync(int ownerId, CancellationToken cancellationToken = default)
    {
        var owner = await dbContext.Owners
            .AsNoTracking()
            .Include(candidate => candidate.Pets)
                .ThenInclude(pet => pet.Species)
            .Include(candidate => candidate.Pets)
                .ThenInclude(pet => pet.Breed!)
            .SingleOrDefaultAsync(candidate => candidate.Id == ownerId, cancellationToken);
        if (owner is null)
        {
            return null;
        }

        var pets = owner.Pets
            .OrderBy(pet => pet.PetCode)
            .Select(pet => new OwnerPetSummary(
                pet.Id,
                pet.PetCode,
                pet.Name,
                pet.Species.Name,
                pet.Breed?.Name,
                pet.Sex,
                pet.BirthDate,
                pet.IsActive))
            .ToList();

        return new OwnerDetails(
            owner.Id,
            owner.OwnerCode,
            owner.FullName,
            owner.PhoneNumber,
            owner.Email,
            owner.Address,
            owner.IsActive,
            owner.CreatedAt,
            owner.RowVersion.ToArray(),
            pets);
    }

    public Task<string> CreateAsync(CreateOwnerRequest request, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var fullName = NormalizeFullName(request.FullName);
            var canonicalPhone = NormalizePhone(request.PhoneNumber);
            var email = NormalizeOptional(request.Email, 254, "Email");
            var address = NormalizeOptional(request.Address, 500, "Địa chỉ");

            var sequenceValue = await ReserveNextOwnerCodeAsync(cancellationToken);
            var owner = new Owner
            {
                OwnerCode = OwnerRules.FormatOwnerCode(sequenceValue),
                FullName = fullName,
                PhoneNumber = canonicalPhone,
                Email = email,
                Address = address,
                IsActive = true,
                CreatedAt = timeProvider.GetUtcNow()
            };
            dbContext.Owners.Add(owner);
            await dbContext.SaveChangesAsync(cancellationToken);
            AddAudit(request.ActorUserId, "Owner.Created", owner.Id, $"Created owner {owner.OwnerCode}.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return owner.OwnerCode;
        }, cancellationToken);

    public Task UpdateAsync(UpdateOwnerRequest request, CancellationToken cancellationToken = default) =>
        GuardConcurrentMutationAsync(() => InSerializableTransactionAsync(async () =>
        {
            var owner = await LoadOwnerAsync(request.OwnerId, request.ExpectedRowVersion, cancellationToken);
            owner.FullName = NormalizeFullName(request.FullName);
            owner.PhoneNumber = NormalizePhone(request.PhoneNumber);
            owner.Email = NormalizeOptional(request.Email, 254, "Email");
            owner.Address = NormalizeOptional(request.Address, 500, "Địa chỉ");
            AddAudit(request.ActorUserId, "Owner.Updated", owner.Id, $"Updated owner {owner.OwnerCode}.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken));

    public Task SetActiveAsync(OwnerActivationRequest request, CancellationToken cancellationToken = default) =>
        GuardConcurrentMutationAsync(() => InSerializableTransactionAsync(async () =>
        {
            var owner = await LoadOwnerAsync(request.OwnerId, request.ExpectedRowVersion, cancellationToken);
            owner.IsActive = request.IsActive;
            AddAudit(
                request.ActorUserId,
                request.IsActive ? "Owner.Activated" : "Owner.Deactivated",
                owner.Id,
                request.IsActive
                    ? $"Activated owner {owner.OwnerCode}."
                    : $"Deactivated owner {owner.OwnerCode}; existing pets keep their own status.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken));

    private async Task<Owner> LoadOwnerAsync(int ownerId, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        var owner = await dbContext.Owners.SingleOrDefaultAsync(candidate => candidate.Id == ownerId, cancellationToken)
            ?? throw new OwnerManagementException("Không tìm thấy chủ nuôi.");
        if (!owner.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
        {
            throw new OwnerConcurrencyException();
        }

        return owner;
    }

    /// <summary>
    /// Sequence numbers are reserved inside the caller's transaction; a rollback discards the number
    /// and leaves an intentional gap instead of reusing codes.
    /// </summary>
    private async Task<long> ReserveNextOwnerCodeAsync(CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("A transaction is required to reserve owner codes.");
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT NEXT VALUE FOR OwnerCodeSequence;";
        try
        {
            return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }
        catch (SqlException exception) when (exception.Number == 11728)
        {
            throw new OwnerManagementException("Đã cấp hết mã chủ nuôi. Hãy liên hệ quản trị viên.");
        }
    }

    private string NormalizePhone(string? input)
    {
        var result = phoneNormalizer.Normalize(input);
        if (!result.IsValid || result.CanonicalNumber is null)
        {
            throw new OwnerManagementException(OwnerRules.DescribePhoneError(result.ErrorCode));
        }

        return result.CanonicalNumber;
    }

    private static string NormalizeFullName(string? input)
    {
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new OwnerManagementException("Họ tên chủ nuôi là bắt buộc.");
        }

        return value.Length > 150
            ? throw new OwnerManagementException("Họ tên chủ nuôi không được vượt quá 150 ký tự.")
            : value;
    }

    private static string? NormalizeOptional(string? input, int maxLength, string fieldName)
    {
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length > maxLength
            ? throw new OwnerManagementException($"{fieldName} không được vượt quá {maxLength} ký tự.")
            : value;
    }

    private void AddAudit(string actorUserId, string action, int ownerId, string description) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorType = "Internal",
            UserId = actorUserId,
            Action = action,
            EntityName = "Owner",
            EntityId = ownerId.ToString(CultureInfo.InvariantCulture),
            Description = description,
            CreatedAt = timeProvider.GetUtcNow()
        });

    private async Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static async Task GuardConcurrentMutationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception) when (IsConcurrentMutation(exception))
        {
            throw new OwnerConcurrencyException();
        }
    }

    private static bool IsConcurrentMutation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (current is SqlException { Number: 1205 or 1222 or 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }
}
