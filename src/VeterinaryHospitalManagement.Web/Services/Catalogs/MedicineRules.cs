namespace VeterinaryHospitalManagement.Web.Services.Catalogs;

public static class MedicineRules
{
    public static string NormalizeCode(string? value) => Required(value, 30, "Mã thuốc").ToUpperInvariant();
    public static string NormalizeName(string? value) => Required(value, 150, "Tên thuốc");
    public static string NormalizeUnit(string? value) => Required(value, 50, "Đơn vị tính");
    public static string? NormalizeActiveIngredient(string? value) => Optional(value, 200, "Hoạt chất");
    public static string? NormalizeStrength(string? value) => Optional(value, 100, "Hàm lượng");

    private static string Required(string? input, int max, string field)
    {
        var value=input?.Trim();
        if(string.IsNullOrWhiteSpace(value)) throw new MedicineManagementException($"{field} là bắt buộc.");
        return value.Length<=max?value:throw new MedicineManagementException($"{field} không được vượt quá {max} ký tự.");
    }
    private static string? Optional(string? input,int max,string field)
    {
        var value=input?.Trim();
        if(string.IsNullOrWhiteSpace(value)) return null;
        return value.Length<=max?value:throw new MedicineManagementException($"{field} không được vượt quá {max} ký tự.");
    }
}
public class MedicineManagementException(string message):InvalidOperationException(message);
public sealed class MedicineConcurrencyException():MedicineManagementException("Thuốc đã được thay đổi. Hãy tải lại trang.");
