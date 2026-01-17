using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Results;
using Boxty.SharedBase.Interfaces;
using Boxty.SharedBase.Enums;
using Boxty.SharedBase.Validation;

namespace Boxty.SharedBase.Helpers
{
    public static class ValidatorExtensions
    {
        public static async Task<ValidationResult> ValidateAsync<T>(
            this IValidator<T> @this,
            T instance,
            IEnumerable<string> properties,
            IEnumerable<string> ruleSets,
            CancellationToken cancellationToken)
        {
            var memberNameValidator = ValidatorOptions
                .Global
                .ValidatorSelectors
                .MemberNameValidatorSelectorFactory(properties);

            var ruleSetValidator = ValidatorOptions
                .Global
                .ValidatorSelectors
                .RulesetValidatorSelectorFactory(ruleSets);

            var validationContext = new ValidationContext<T>(
                instance,
                new PropertyChain(),
                new FluentValidatorCompositeValidatorSelector(new[] {
                memberNameValidator,
                ruleSetValidator
                }));
            Console.WriteLine($"ValidatorExtensions: Validating properties [{string.Join(", ", properties)}] with rule sets [{string.Join(", ", ruleSets)}]");
            var validationResult = await @this.ValidateAsync(validationContext, cancellationToken);

            return validationResult;
        }

        /// <summary>
        /// Extension method to provide backward compatibility for ValidateValue with ValidationModeEnum
        /// </summary>
        public static async Task<IEnumerable<string>> ValidateValue<T>(
            this BaseValidator<T> validator,
            T instance,
            string propertyName,
            ValidationModeEnum validationMode)
            where T : class
        {
            var validationModes = new List<string> { validationMode.ToString() };

            // If validationMode is Default, also ensure we include default behavior
            if (validationMode == ValidationModeEnum.Default)
            {
                validationModes = new List<string> { "Default" };
            }

            return await ((IValidateValue)validator).ValidateValue(instance, propertyName, validationModes);
        }

        public class FluentValidatorCompositeValidatorSelector : IValidatorSelector
        {
            private IEnumerable<IValidatorSelector> _selectors;

            public FluentValidatorCompositeValidatorSelector(IEnumerable<IValidatorSelector> selectors)
            {
                _selectors = selectors;
            }

            public bool CanExecute(IValidationRule rule, string propertyPath, IValidationContext context)
            {
                return _selectors.All(s => s.CanExecute(rule, propertyPath, context));
            }
        }
    }
}