namespace VeterinaryHospitalManagement.Web.Services.Visits;

/// <summary>Exception cho lỗi nghiệp vụ Visit.</summary>
public sealed class VisitManagementException : Exception
{
    public VisitManagementException(string message) : base(message) { }
}
