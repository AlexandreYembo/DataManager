namespace Migration.Models
{
    public class DataSettings
    {
        public string Name { get; set; }
        public ConnectionType ConnectionType { get; set; }

        public List<CustomAttributes> Parameters { get; set; } = new();

        public List<Entity> Entities { get; set; } = new();

        public Entity CurrentEntity { get; set; } = new();

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
                case ConnectionType.File:
                    Parameters.Add(new CustomAttributes());
                    AllowAddCustomParameters = false;
                    break;
                case ConnectionType.TableStorage:
                    AllowAddCustomParameters = false;
                    Parameters.Add(new CustomAttributes()
                    {
                        Key = "Is Emulator",
                        Value = "False",
                        Type = "bool"
                    });

                    Parameters.Add(new CustomAttributes()
                    {
                        Key = "AccountName"
                    });
                    Parameters.Add(new CustomAttributes()
                    {
                        Key = "AuthKey"
                    });
                    break;
            }
        }

        public string FullName => $"{ConnectionType}-{Name}";

        public string GetEndpoint() => Parameters?.FirstOrDefault(f => f.Key == "Endpoint")?.Value ?? string.Empty;

        public string GetAuthKey() => Parameters?.FirstOrDefault(f => f.Key == "AuthKey")?.Value ?? string.Empty;

        public string GetDataBase() => Parameters?.FirstOrDefault(f => f.Key == "Database")?.Value ?? string.Empty;
        public string GetContainer() => Parameters?.FirstOrDefault(f => f.Key == "Container")?.Value ?? string.Empty;

        public string GetFileName() => Parameters?.FirstOrDefault(f => f.Key == "FileName")?.Value ?? string.Empty;

        public string GetAccountName() => Parameters?.FirstOrDefault(f => f.Key == "AccountName")?.Value ?? string.Empty;
    }

    public class Entity
    {
        public Entity() { }

        public Entity(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
        public List<CustomAttributes> Attributes { get; set; }
    }

    public class CustomAttributes
    {
        public string Key { get; set; }
        public string? Value { get; set; }
        public string Type { get; set; }
    }

    public enum ConnectionType
    {
        CosmosDb,
        File,
        TableStorage
    }
}
