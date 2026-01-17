namespace Boxty.ClientBase.Services
{
    public class GlobalStateService
    {
        public event Action OnStartLoading = default!;
        public event Action OnStopLoading = default!;

        public void StartLoading()
        {
            OnStartLoading?.Invoke();
        }

        public void StopLoading()
        {
            OnStopLoading?.Invoke();
        }
    }
}