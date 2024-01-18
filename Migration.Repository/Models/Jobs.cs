namespace Migration.Repository.Models
{
    public class Jobs
    {
        public int JobId { get; set; }
        public string ProfileId { get; set; }
        public OperationType OperationType { get; set; }
        public string Status { get; set; }
        public int SourceProcessed { get; set; }
        public int DestinationProcessed { get; set; }
        public string JobCategory { get; set; }
    }
}
