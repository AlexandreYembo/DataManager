using Migration.Repository.Models;

namespace Migration.Repository.Delegates
{
    public class JobsEventArgs : EventArgs
    {
        public Jobs Job { get; set; }
        public JobsEventArgs(Jobs job)
        {
            Job = job;
        }
    }
}