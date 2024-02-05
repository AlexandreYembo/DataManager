
namespace Migration.Repository.Models
{
    public class Jobs
    {
        public int JobId { get; set; }
        public string ProfileId { get; set; }
        public OperationType OperationType { get; set; }
        public JobStatus Status { get; set; }
        public int SourceProcessed { get; set; }
        public int DestinationProcessed { get; set; }
        public string JobCategory { get; set; }

        public void Start() => Status = JobStatus.InProgress;
        public void Complete() => Status = JobStatus.Completed;
        public void Waiting() => Status = JobStatus.Waiting;
    }

    public enum JobStatus
    {
        InProgress,
        Queued,
        Waiting,
        Completed,
        Error
    }
}