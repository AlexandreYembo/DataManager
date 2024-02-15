using Migration.Models;

namespace Migration.EventHandlers.CustomEventArgs
{
    public class ActionsEventArgs : EventArgs
    {
        public Actions Actions { get; set; }
        public ActionsEventArgs(Actions actions)
        {
            Actions = actions;
        }
    }
}