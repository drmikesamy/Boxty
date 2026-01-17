namespace Boxty.SharedBase.Interfaces
{
    public interface IValidateValue
    {
        Func<object, string, List<string>, Task<IEnumerable<string>>> ValidateValue { get; }
    }
}