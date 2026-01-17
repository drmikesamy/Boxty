namespace Boxty.SharedBase.Interfaces
{
    public interface IContextValidator<T, TContext>
    {
        TContext? Context { get; set; }
    }
}