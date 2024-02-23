namespace Migration.Models.Profile
{
    public class Configuration
    {
        public DataSettings Settings { get; set; } = new();
        public string? Query { get; set; }
    }
}