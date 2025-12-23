namespace EmbeddronicsBackend.Models.Exceptions
{
    /// <summary>
    /// Exception thrown when a business rule is violated
    /// </summary>
    public class BusinessRuleException : Exception
    {
        public string RuleCode { get; }

        public BusinessRuleException() : base("A business rule was violated.")
        {
            RuleCode = "BUSINESS_RULE_VIOLATION";
        }

        public BusinessRuleException(string message) : base(message)
        {
            RuleCode = "BUSINESS_RULE_VIOLATION";
        }

        public BusinessRuleException(string message, Exception innerException) : base(message, innerException)
        {
            RuleCode = "BUSINESS_RULE_VIOLATION";
        }

        public BusinessRuleException(string ruleCode, string message) : base(message)
        {
            RuleCode = ruleCode ?? "BUSINESS_RULE_VIOLATION";
        }
    }
}