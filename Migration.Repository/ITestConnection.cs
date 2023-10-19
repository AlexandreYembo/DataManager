namespace Migration.Repository
{
    public interface ITestConnection
    {
        Task<DBSettings> Test();
    }
}