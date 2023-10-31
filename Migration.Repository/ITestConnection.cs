namespace Migration.Repository
{
    public interface ITestConnection
    {
        Task<DataSettings> Test();
    }
}