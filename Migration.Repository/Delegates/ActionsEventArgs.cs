namespace Migration.Repository.Delegates
{
    public class ActionsEventArgs : EventArgs
    {
        public Actions Actions { get; set; }
        public ActionsEventArgs(Actions actions)
        {
            Actions = actions;
        }
    }

    public class Actions
    {
        public string Message { get; set; }
        public ActionEventType ActionType { get; set; }
    }

    public enum ActionEventType
    {
        Success,
        Error
    }
}