namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Appointments;

public sealed class CalendarEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public bool AllDay { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public CalendarEventExtendedProps ExtendedProps { get; set; } = new();
}

public sealed class CalendarEventExtendedProps
{
    public string PetName { get; set; } = string.Empty;
    public string VeterinarianName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Type { get; set; } = "appointment"; // "appointment" or "shift"
}
