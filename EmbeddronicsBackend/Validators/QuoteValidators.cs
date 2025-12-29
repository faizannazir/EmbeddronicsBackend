using FluentValidation;
using EmbeddronicsBackend.Models.DTOs;

namespace EmbeddronicsBackend.Validators
{
    public class CreateQuoteRequestValidator : AbstractValidator<CreateQuoteRequest>
    {
        public CreateQuoteRequestValidator()
        {
            RuleFor(x => x.ClientId)
                .GreaterThan(0).WithMessage("Client ID must be a positive number");

            RuleFor(x => x.OrderId)
                .GreaterThan(0).WithMessage("Order ID must be a positive number")
                .When(x => x.OrderId.HasValue);

            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Quote amount must be greater than zero")
                .LessThanOrEqualTo(9999999.99m).WithMessage("Quote amount must not exceed $9,999,999.99");

            RuleFor(x => x.Currency)
                .NotEmpty().WithMessage("Currency is required")
                .Length(3).WithMessage("Currency must be a 3-letter code")
                .Must(BeAValidCurrency).WithMessage("Currency must be a valid ISO currency code (e.g., USD, EUR, GBP)");

            RuleFor(x => x.ValidUntil)
                .Must(QuoteBusinessRules.IsValidQuoteExpiration).WithMessage("Quote expiration date must be between 1 day and 1 year from now");

            RuleForEach(x => x.Items)
                .SetValidator(new CreateQuoteItemRequestValidator())
                .When(x => x.Items != null && x.Items.Any());

            RuleFor(x => x.Items)
                .Must(items => items == null || items.Count <= 50)
                .WithMessage("Quote cannot have more than 50 items");
        }

        private bool BeAValidCurrency(string currency)
        {
            var validCurrencies = new[] { "USD", "EUR", "GBP", "CAD", "AUD", "JPY", "CHF", "CNY", "INR" };
            return validCurrencies.Contains(currency.ToUpper());
        }
    }

    public class UpdateQuoteRequestValidator : AbstractValidator<UpdateQuoteRequest>
    {
        public UpdateQuoteRequestValidator()
        {
            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Quote amount must be greater than zero")
                .LessThanOrEqualTo(9999999.99m).WithMessage("Quote amount must not exceed $9,999,999.99")
                .When(x => x.Amount.HasValue);

            RuleFor(x => x.Currency)
                .Length(3).WithMessage("Currency must be a 3-letter code")
                .Must(BeAValidCurrency).WithMessage("Currency must be a valid ISO currency code (e.g., USD, EUR, GBP)")
                .When(x => !string.IsNullOrEmpty(x.Currency));

            RuleFor(x => x.ValidUntil)
                .Must(validUntil => validUntil.HasValue && QuoteBusinessRules.IsValidQuoteExpiration(validUntil.Value)).WithMessage("Quote expiration date must be between 1 day and 1 year from now")
                .When(x => x.ValidUntil.HasValue);

            RuleFor(x => x.Status)
                .Must(status => new[] { "pending", "approved", "rejected", "expired" }.Contains(status))
                .WithMessage("Status must be one of: pending, approved, rejected, expired")
                .When(x => !string.IsNullOrEmpty(x.Status));

            RuleForEach(x => x.Items)
                .SetValidator(new CreateQuoteItemRequestValidator())
                .When(x => x.Items != null && x.Items.Any());

            RuleFor(x => x.Items)
                .Must(items => items == null || items.Count <= 50)
                .WithMessage("Quote cannot have more than 50 items");
        }

        private bool BeAValidCurrency(string currency)
        {
            var validCurrencies = new[] { "USD", "EUR", "GBP", "CAD", "AUD", "JPY", "CHF", "CNY", "INR" };
            return validCurrencies.Contains(currency.ToUpper());
        }
    }

    public class CreateQuoteItemRequestValidator : AbstractValidator<CreateQuoteItemRequest>
    {
        public CreateQuoteItemRequestValidator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0).WithMessage("Product ID must be a positive number")
                .When(x => x.ProductId.HasValue);

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Item description is required")
                .MinimumLength(3).WithMessage("Description must be at least 3 characters long")
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero")
                .LessThanOrEqualTo(999999).WithMessage("Quantity must not exceed 999,999");

            RuleFor(x => x.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Unit price must be zero or positive")
                .LessThanOrEqualTo(999999.99m).WithMessage("Unit price must not exceed $999,999.99");
        }
    }

    public class QuoteAcceptanceRequestValidator : AbstractValidator<QuoteAcceptanceRequest>
    {
        public QuoteAcceptanceRequestValidator()
        {
            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("Notes must not exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Notes));
        }
    }
}