namespace VeterinaryHospitalManagement.Web.Services.Catalogs;

public static class ServiceCatalogRules
{
    public static string NormalizeCode(string? input) =>
        NormalizeRequired(input, 30, "Mã dịch vụ").ToUpperInvariant();

    public static string NormalizeName(string? input) =>
        NormalizeRequired(input, 150, "Tên dịch vụ");

    public static string NormalizeCategory(string? input) =>
        NormalizeRequired(input, 100, "Nhóm dịch vụ");

    public static string? NormalizeDescription(string? input)
    {
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= 1000
            ? value
            : throw new ServiceCatalogManagementException("Mô tả không được vượt quá 1000 ký tự.");
    }

    public static decimal NormalizePrice(decimal price)
    {
        if (price < 0)
        {
            throw new ServiceCatalogManagementException("Đơn giá không được âm.");
        }

        if (decimal.Round(price, 2) != price)
        {
            throw new ServiceCatalogManagementException("Đơn giá chỉ được có tối đa 2 chữ số thập phân.");
        }

        return price;
    }

    private static string NormalizeRequired(string? input, int maxLength, string fieldName)
    {
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ServiceCatalogManagementException($"{fieldName} là bắt buộc.");
        }

        return value.Length <= maxLength
            ? value
            : throw new ServiceCatalogManagementException($"{fieldName} không được vượt quá {maxLength} ký tự.");
    }
}

public class ServiceCatalogManagementException(string message) : InvalidOperationException(message);

public sealed class ServiceCatalogConcurrencyException()
    : ServiceCatalogManagementException("Dịch vụ đã được thay đổi. Hãy tải lại trang.");
