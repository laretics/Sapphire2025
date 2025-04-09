using ZafiroGmao.Data.Models;

namespace ZafiroGmao.Data
{
    public class MontefaroSessionContainer
    {
        private Common.UserRole mvarCurrentRole;
        public Common.UserRole CurrentRole //Rol seleccionado.
        {
            get => mvarCurrentRole;
            set
            {
                mvarCurrentRole = value;
                NotifyStateChanged();
            }
        } 

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
