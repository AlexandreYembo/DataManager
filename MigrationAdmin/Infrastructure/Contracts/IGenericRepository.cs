namespace MigrationAdmin.Infrastructure.Contracts
{
    public interface IGenericRepository
    {
        Task<List<Dictionary<string, string>>> Get(string query);
    }
}