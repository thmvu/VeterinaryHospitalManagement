using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Prescriptions;

public sealed class PrescriptionQuantityModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var result = context.ValueProvider.GetValue(context.ModelName);
        if (result == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, result);
        if (decimal.TryParse(result.FirstValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
            context.Result = ModelBindingResult.Success(quantity);
        else
            context.ModelState.TryAddModelError(context.ModelName, "Số lượng không đúng định dạng.");
        return Task.CompletedTask;
    }
}
