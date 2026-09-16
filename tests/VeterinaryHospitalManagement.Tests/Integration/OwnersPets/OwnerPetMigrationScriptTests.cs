using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Integration.OwnersPets;

public sealed class OwnerPetMigrationScriptTests
{
    [Fact]
    public void AddOwnersAndPets_migration_generates_the_required_schema_offline()
    {
        using var context = CreateContext();
        var initialMigration = FindMigration(context, "_InitialIdentityAndPermissions");
        var ownerPetMigration = FindMigration(context, "_AddOwnersAndPets");

        var script = context.GetService<IMigrator>().GenerateScript(initialMigration, ownerPetMigration);

        AssertSequenceScript(script, "OwnerCodeSequence");
        AssertSequenceScript(script, "PetCodeSequence");
        Assert.Contains("CREATE TABLE [Owners]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Species]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Breeds]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Pets]", script, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(script, "[RowVersion] rowversion NOT NULL"));
        Assert.Equal(2, CountOccurrences(script, "nvarchar(10)"));
        Assert.Contains("CONSTRAINT [CK_Owners_OwnerCode_Format] CHECK (DATALENGTH([OwnerCode]) = 20 AND [OwnerCode] COLLATE Latin1_General_100_BIN2 LIKE N'OWN-[0-9][0-9][0-9][0-9][0-9][0-9]')", script, StringComparison.Ordinal);
        Assert.Contains("CONSTRAINT [CK_Pets_PetCode_Format] CHECK (DATALENGTH([PetCode]) = 20 AND [PetCode] COLLATE Latin1_General_100_BIN2 LIKE N'PET-[0-9][0-9][0-9][0-9][0-9][0-9]')", script, StringComparison.Ordinal);
        Assert.Contains("CONSTRAINT [CK_Owners_PhoneNumber_Canonical] CHECK", script, StringComparison.Ordinal);
        Assert.Contains("DATALENGTH([PhoneNumber]) = 24", script, StringComparison.Ordinal);
        Assert.Contains("COLLATE Latin1_General_100_BIN2", script, StringComparison.Ordinal);
        Assert.Contains("+84", script, StringComparison.Ordinal);
        Assert.Contains("[35789]", script, StringComparison.Ordinal);
        Assert.Contains("[0-9]", script, StringComparison.Ordinal);
        Assert.Contains("CONSTRAINT [CK_Pets_Sex] CHECK", script, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX [IX_Owners_OwnerCode]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX [IX_Owners_PhoneNumber]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE UNIQUE INDEX [IX_Owners_PhoneNumber]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX [IX_Species_Code]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE UNIQUE INDEX [IX_Species_Name]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX [IX_Breeds_SpeciesId_Name]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX [IX_Pets_PetCode]", script, StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT [AK_Breeds_Id_SpeciesId] UNIQUE ([Id], [SpeciesId])",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT [FK_Pets_Species_SpeciesId] FOREIGN KEY ([SpeciesId]) REFERENCES [Species] ([Id]) ON DELETE NO ACTION",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT [FK_Pets_Breeds_BreedId_SpeciesId] FOREIGN KEY ([BreedId], [SpeciesId]) REFERENCES [Breeds] ([Id], [SpeciesId]) ON DELETE NO ACTION",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AddOwnersAndPets_migration_generates_down_and_up_scripts_offline()
    {
        using var context = CreateContext();
        var initialMigration = FindMigration(context, "_InitialIdentityAndPermissions");
        var ownerPetMigration = FindMigration(context, "_AddOwnersAndPets");
        var migrator = context.GetService<IMigrator>();

        var downScript = migrator.GenerateScript(ownerPetMigration, initialMigration);
        var upScript = migrator.GenerateScript(initialMigration, ownerPetMigration);

        Assert.Contains("DROP TABLE [Pets]", downScript, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE [Breeds]", downScript, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE [Owners]", downScript, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE [Species]", downScript, StringComparison.Ordinal);
        Assert.Contains("DROP SEQUENCE [OwnerCodeSequence]", downScript, StringComparison.Ordinal);
        Assert.Contains("DROP SEQUENCE [PetCodeSequence]", downScript, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Owners]", upScript, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Species]", upScript, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Breeds]", upScript, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Pets]", upScript, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOwnersAndPets_idempotent_script_guards_migration_execution()
    {
        using var context = CreateContext();
        var initialMigration = FindMigration(context, "_InitialIdentityAndPermissions");
        var ownerPetMigration = FindMigration(context, "_AddOwnersAndPets");

        var script = context.GetService<IMigrator>().GenerateScript(
            initialMigration,
            ownerPetMigration,
            MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("IF NOT EXISTS", script, StringComparison.Ordinal);
        Assert.Contains(ownerPetMigration, script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Owners]", script, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=VeterinaryHospitalManagement_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static string FindMigration(ApplicationDbContext context, string suffix) =>
        Assert.Single(
            context.Database.GetMigrations(),
            migration => migration.EndsWith(suffix, StringComparison.Ordinal));

    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(expected, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += expected.Length;
        }
        return count;
    }

    private static void AssertSequenceScript(string script, string sequenceName)
    {
        var sequenceStart = script.IndexOf($"CREATE SEQUENCE [{sequenceName}]", StringComparison.Ordinal);
        Assert.True(sequenceStart >= 0, $"Expected CREATE SEQUENCE for {sequenceName}.");
        var statementEnd = script.IndexOf(';', sequenceStart);
        Assert.True(statementEnd > sequenceStart, $"Expected terminated CREATE SEQUENCE for {sequenceName}.");
        var statement = script[sequenceStart..statementEnd];
        Assert.Contains("START WITH 1", statement, StringComparison.Ordinal);
        Assert.Contains("INCREMENT BY 1", statement, StringComparison.Ordinal);
        Assert.Contains("MINVALUE 1", statement, StringComparison.Ordinal);
        Assert.Contains("MAXVALUE 999999", statement, StringComparison.Ordinal);
        Assert.Contains("NO CYCLE", statement, StringComparison.Ordinal);
    }
}
