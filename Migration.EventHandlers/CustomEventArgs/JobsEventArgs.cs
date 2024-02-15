using Migration.Models;
namespace Migration.EventHandlers.CustomEventArgs
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