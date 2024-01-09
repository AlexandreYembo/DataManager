namespace Migration.Services
{
    public enum ActionType
    {
        None,
        RevertToPreviousChange,
        BackupData,
        Insert,
        Delete
    }
}