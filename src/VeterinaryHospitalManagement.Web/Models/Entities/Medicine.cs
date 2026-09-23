namespace VeterinaryHospitalManagement.Web.Models.Entities;

/// <summary>
/// Danh mục thuốc – chỉ là tham chiếu kê đơn (không có giá bán/tồn kho).
/// </summary>
public sealed class Medicine
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ActiveIngredient { get; set; }

    public string? Strength { get; set; }

    public string Unit { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = [];
}
