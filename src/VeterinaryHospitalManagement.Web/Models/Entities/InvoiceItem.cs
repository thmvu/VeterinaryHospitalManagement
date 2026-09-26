namespace VeterinaryHospitalManagement.Web.Models.Entities;

public sealed class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int VisitServiceId { get; set; }
    public string DescriptionSnapshot { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public VisitService VisitService { get; set; } = null!;
}
