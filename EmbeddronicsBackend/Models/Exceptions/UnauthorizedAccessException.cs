namespace EmbeddronicsBackend.Models.Exceptions
{
    /// <summary>
    /// Exception thrown when unauthorized access is attempted
    /// </summary>
    public class UnauthorizedOperationException : Exception
    {
        public UnauthorizedOperationException() : base("Access denied. You do not have permission to perform this action.")
        {
        }

        public UnauthorizedOperationException(string message) : base(message)
        {
        }

        public UnauthorizedOperationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public UnauthorizedOperationException(string resource, string action) 
            : base($"Access denied. You do not have permission to {action} {resource}.")
        {
        }
    }
}