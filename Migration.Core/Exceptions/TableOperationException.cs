namespace Migration.Core.Exceptions
{
    public class TableOperationException : Exception
    {
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public TableOperationException(string message) : base(message)
        {
        }

        public TableOperationException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
            ErrorMessage = $"Error to operate changes in the table. Error Message = {message}";
        }
    }
}