using VeterinaryHospitalManagement.Web.Models.Enums;
namespace VeterinaryHospitalManagement.Web.Models.Entities;
public sealed class Appointment
{
 public int Id{get;set;} public string AppointmentNumber{get;set;}=string.Empty; public int PetId{get;set;} public int VeterinarianId{get;set;} public DateTimeOffset StartAt{get;set;} public DateTimeOffset EndAt{get;set;} public string Reason{get;set;}=string.Empty; public AppointmentStatus Status{get;set;}=AppointmentStatus.Scheduled; public string? CancellationReason{get;set;} public string CreatedByUserId{get;set;}=string.Empty; public DateTimeOffset CreatedAt{get;set;} public byte[] RowVersion{get;set;}=[]; public Pet Pet{get;set;}=null!; public VeterinarianProfile Veterinarian{get;set;}=null!; public ApplicationUser CreatedByUser{get;set;}=null!;
}
