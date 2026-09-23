using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.ServiceCatalogs;

public sealed class InvariantDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
        var value = result.FirstValue;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            bindingContext.Result = ModelBindingResult.Success(number);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Đơn giá không đúng định dạng.");
        return Task.CompletedTask;
    }
}
