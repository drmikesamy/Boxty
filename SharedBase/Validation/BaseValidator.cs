using FluentValidation;
using Boxty.SharedBase.Interfaces;
using Boxty.SharedBase.Enums;
using Boxty.SharedBase.Helpers;

namespace Boxty.SharedBase.Validation
{
    public abstract class BaseValidator<TDto> : AbstractValidator<TDto>, IValidateValue
        where TDto : class
    {
        public Func<object, string, List<string>, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName, validationModes) =>
        {
            var dto = (TDto)model;

            Console.WriteLine($"BaseValidator: Validating property {propertyName} with modes {string.Join(", ", validationModes)}");
            var ruleSetResult = await this.ValidateAsync(dto, new List<string> { propertyName }, validationModes, CancellationToken.None);
            return ruleSetResult.Errors.Select(e => e.ErrorMessage);
        };
    }
}