using VeterinaryHospitalManagement.Web.Services.Veterinarians;
namespace VeterinaryHospitalManagement.Tests.Unit.Veterinarians;
public sealed class VeterinarianProfileRulesTests
{
 [Fact] public void Doctor_code_is_trimmed_and_uppercased()=>Assert.Equal("VET-001",VeterinarianProfileRules.NormalizeDoctorCode(" vet-001 "));
 [Fact] public void Blank_doctor_code_is_rejected()=>Assert.Throws<VeterinarianManagementException>(()=>VeterinarianProfileRules.NormalizeDoctorCode(" "));
 [Fact] public void Long_doctor_code_is_rejected()=>Assert.Throws<VeterinarianManagementException>(()=>VeterinarianProfileRules.NormalizeDoctorCode(new string('A',31)));
 [Fact] public void Blank_specialty_becomes_null()=>Assert.Null(VeterinarianProfileRules.NormalizeSpecialty("  "));
 [Fact] public void Specialty_is_trimmed()=>Assert.Equal("Nội khoa",VeterinarianProfileRules.NormalizeSpecialty(" Nội khoa "));
}
