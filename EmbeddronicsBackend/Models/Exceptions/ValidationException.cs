namespace EmbeddronicsBackend.Models.Exceptions
{
    /// <summary>
    /// Exception thrown when validation fails
    /// </summary>
    public class ValidationException : Exception
    {
        public List<ValidationError> Errors { get; }

        public ValidationException() : base("One or more validation errors occurred.")
        {
            Errors = new List<ValidationError>();
        }

        public ValidationException(string message) : base(message)
        {
            Errors = new List<ValidationError>();
        }

        public ValidationException(string message, Exception innerException) : base(message, innerException)
        {
            Errors = new List<ValidationError>();
        }

        public ValidationException(List<ValidationError> errors) : base("One or more validation errors occurred.")
        {
            Errors = errors ?? new List<ValidationError>();
        }

        public ValidationException(string field, string message) : base("Validation failed.")
        {
            Errors = new List<ValidationError>
            {
                new ValidationError(field, message)
            };
        }
    }

    /// <summary>
    /// Represents a validation error for a specific field
    /// </summary>
    public class ValidationError
    {
        public string Field { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }

        public ValidationError()
        {
            Field = string.Empty;
            Code = string.Empty;
            Message = string.Empty;
        }

        public ValidationError(string field, string message)
        {
            Field = field ?? string.Empty;
            Code = "ValidationError";
            Message = message ?? string.Empty;
        }

        public ValidationError(string field, string code, string message)
        {
            Field = field ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}