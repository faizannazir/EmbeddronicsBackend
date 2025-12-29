using FluentValidation;
using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Validators
{
    public class UserValidator : AbstractValidator<User>
    {
        public UserValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MinimumLength(2).WithMessage("Name must be at least 2 characters long")
                .MaximumLength(255).WithMessage("Name must not exceed 255 characters")
                .Matches(@"^[a-zA-Z\s\-'\.]+$").WithMessage("Name can only contain letters, spaces, hyphens, apostrophes, and periods");

            RuleFor(x => x.PasswordHash)
                .NotEmpty().WithMessage("Password hash is required")
                .MaximumLength(255).WithMessage("Password hash must not exceed 255 characters");

            RuleFor(x => x.Role)
                .NotEmpty().WithMessage("Role is required")
                .Must(role => new[] { "admin", "client" }.Contains(role))
                .WithMessage("Role must be either 'admin' or 'client'");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status is required")
                .Must(status => new[] { "active", "inactive", "pending" }.Contains(status))
                .WithMessage("Status must be one of: active, inactive, pending");

            RuleFor(x => x.Company)
                .MaximumLength(255).WithMessage("Company name must not exceed 255 characters")
                .When(x => !string.IsNullOrEmpty(x.Company));

            RuleFor(x => x.Phone)
                .MaximumLength(50).WithMessage("Phone number must not exceed 50 characters")
                .Matches(@"^[\+]?[1-9][\d]{0,15}$").WithMessage("Invalid phone number format")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.RefreshToken)
                .MaximumLength(500).WithMessage("Refresh token must not exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.RefreshToken));
        }
    }

    public class OrderValidator : AbstractValidator<Order>
    {
        public OrderValidator()
        {
            RuleFor(x => x.ClientId)
                .GreaterThan(0).WithMessage("Client ID must be a positive number");

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Order title is required")
                .MinimumLength(3).WithMessage("Order title must be at least 3 characters long")
                .MaximumLength(255).WithMessage("Order title must not exceed 255 characters");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status is required")
                .Must(status => new[] { "new", "in_progress", "completed", "cancelled" }.Contains(status))
                .WithMessage("Status must be one of: new, in_progress, completed, cancelled");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.BudgetRange)
                .MaximumLength(100).WithMessage("Budget range must not exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.BudgetRange));

            RuleFor(x => x.Timeline)
                .MaximumLength(100).WithMessage("Timeline must not exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Timeline));
        }
    }

    public class ProductValidator : AbstractValidator<Product>
    {
        public ProductValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Product name is required")
                .MinimumLength(2).WithMessage("Product name must be at least 2 characters long")
                .MaximumLength(255).WithMessage("Product name must not exceed 255 characters");

            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("Product category is required")
                .MinimumLength(2).WithMessage("Category must be at least 2 characters long")
                .MaximumLength(100).WithMessage("Category must not exceed 100 characters");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be zero or positive")
                .LessThanOrEqualTo(999999.99m).WithMessage("Price must not exceed $999,999.99")
                .When(x => x.Price.HasValue);

            RuleFor(x => x.ImageUrl)
                .MaximumLength(500).WithMessage("Image URL must not exceed 500 characters")
                .Must(BeAValidUrl).WithMessage("Image URL must be a valid URL")
                .When(x => !string.IsNullOrEmpty(x.ImageUrl));
        }

        private bool BeAValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
    }

    public class QuoteValidator : AbstractValidator<Quote>
    {
        public QuoteValidator()
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
                .Must(BeAValidCurrency).WithMessage("Currency must be a valid ISO currency code");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status is required")
                .Must(status => new[] { "pending", "approved", "rejected", "expired" }.Contains(status))
                .WithMessage("Status must be one of: pending, approved, rejected, expired");

            RuleFor(x => x.ValidUntil)
                .GreaterThan(DateTime.UtcNow).WithMessage("Quote expiration date must be in the future");
        }

        private bool BeAValidCurrency(string currency)
        {
            var validCurrencies = new[] { "USD", "EUR", "GBP", "CAD", "AUD", "JPY", "CHF", "CNY", "INR" };
            return validCurrencies.Contains(currency.ToUpper());
        }
    }

    public class MessageValidator : AbstractValidator<Message>
    {
        public MessageValidator()
        {
            RuleFor(x => x.OrderId)
                .GreaterThan(0).WithMessage("Order ID must be a positive number");

            RuleFor(x => x.SenderId)
                .GreaterThan(0).WithMessage("Sender ID must be a positive number");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Message content is required")
                .MinimumLength(1).WithMessage("Message content cannot be empty")
                .MaximumLength(2000).WithMessage("Message content must not exceed 2000 characters");
        }
    }
}