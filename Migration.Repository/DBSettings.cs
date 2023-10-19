namespace Migration.Repository
{
    public class DBSettings
    {
        public string Name { get; set; }
        public string Endpoint { get; set; }
        public string AuthKey { get; set; }
        public string? Database { get; set; }
        public string? Container { get; set; }
        public List<string> ListOfContainer { get; set; } = new();
        public string ConnectionString { get; set; }
        public DbType DbType { get; set; }
    }

    public enum DbType
    {
        Cosmos,
        TableStorage
    }
}