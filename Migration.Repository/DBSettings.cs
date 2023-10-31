namespace Migration.Repository
{
    public class DataSettings
    {
        public DataSettings()
        {
            ChangeType();
        }

        public string Name { get; set; }
        public ConnectionType ConnectionType { get; set; }

        public List<CustomAttributes> Parameters { get; set; } = new();

        public List<string> Entities { get; set; } = new();

        public string CurrentEntity { get; set; }

        public bool AllowAddCustomParameters { get; set; }

        public void ChangeType()
        {
            Parameters = new();
            switch (ConnectionType)
            {
                case ConnectionType.CosmosDb:
                    AllowAddCustomParameters = false;
                    Parameters.Add(new CustomAttributes()
                    {
                        Key = "Endpoint"
                    });
                    Parameters.Add(new CustomAttributes()
                    {
                        Key = "AuthKey"
                    });
                    Parameters.Add(new CustomAttributes()
                    {
                        Key = "Database"
                    });
                    break;
                case ConnectionType.TableStorage:
                    AllowAddCustomParameters = false;
                    Parameters.Add(new CustomAttributes());
                    break;
                case ConnectionType.Api:
                    AllowAddCustomParameters = true;
                    Parameters.Add(new CustomAttributes());
                    break;
            }
        }

        public string GetEndpoint() => Parameters?.FirstOrDefault(f => f.Key == "Endpoint")?.Value ?? string.Empty;

        public string GetAuthKey() => Parameters?.FirstOrDefault(f => f.Key == "AuthKey")?.Value ?? string.Empty;

        public string GetDataBase() => Parameters?.FirstOrDefault(f => f.Key == "Database")?.Value ?? string.Empty;

    }

    public class CustomAttributes
    {
        public string Key { get; set; }
        public string? Value { get; set; }
    }

    public enum ConnectionType
    {
        CosmosDb,
        TableStorage,
        Api
    }

    //To Be replaced
    public class DBSettings
    {
        public string Name { get; set; }
        public string Endpoint { get; set; }
        public string AuthKey { get; set; }
        public string Database { get; set; }
        public string Container { get; set; }
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