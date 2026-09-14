using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Unit;

public class ApplicationDbContextTests
{
    [Fact]
    public void CanBeConfiguredWithSqlServerProvider()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=VeterinaryHospitalManagement_Test;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new ApplicationDbContext(options);

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
    }
}
