using System.ComponentModel.DataAnnotations;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Invoices;
using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Tests.Unit.Billing;

public sealed class ConfirmCheckoutViewModelTests
{
    [Fact]
    public void Missing_payment_method_is_invalid_but_explicit_cash_is_valid()
    {
        var model = new ConfirmCheckoutViewModel { VisitId = 1 };
        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.PaymentMethod)));

        model.PaymentMethod = PaymentMethod.Cash;
        errors.Clear();
        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
    }
}
