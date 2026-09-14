namespace VeterinaryHospitalManagement.Web.Services.Time;

public interface IVietnamTimeProvider
{
    DateTimeOffset UtcNow { get; }

    DateTimeOffset LocalNow { get; }
}
