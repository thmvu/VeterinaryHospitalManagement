namespace VeterinaryHospitalManagement.Web.Services.Owners;

public interface IOwnerPhoneNormalizer
{
    OwnerPhoneNormalizationResult Normalize(string? input);
}
