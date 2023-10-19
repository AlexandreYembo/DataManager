namespace Migration.Repository.Exceptions
{
    public class DbOperationException : Exception
    {
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public DbOperationException(string message) : base(message)
        {
        }

        public DbOperationException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
            ErrorMessage = $"Error when try to update the DB {message}";
        }
    }
}
