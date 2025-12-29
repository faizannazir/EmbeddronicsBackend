using FluentValidation;
using EmbeddronicsBackend.Models.DTOs;

namespace EmbeddronicsBackend.Validators
{
    public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
    {
        public CreateProductRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Product name is required")
                .MinimumLength(2).WithMessage("Product name must be at least 2 characters long")
                .MaximumLength(255).WithMessage("Product name must not exceed 255 characters");

            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("Product category is required")
                .MinimumLength(2).WithMessage("Category must be at least 2 characters long")
                .MaximumLength(100).WithMessage("Category must not exceed 100 characters")
                .Must(BeAValidCategory).WithMessage("Category must contain only letters, numbers, spaces, and hyphens");

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

            RuleForEach(x => x.Features)
                .NotEmpty().WithMessage("Feature cannot be empty")
                .MaximumLength(200).WithMessage("Each feature must not exceed 200 characters")
                .When(x => x.Features != null && x.Features.Any());

            RuleFor(x => x.Features)
                .Must(features => features == null || features.Count <= 20)
                .WithMessage("Product cannot have more than 20 features");
        }

        private bool BeAValidCategory(string category)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(category, @"^[a-zA-Z0-9\s\-]+$");
        }

        private bool BeAValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
    }

    public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
    {
        public UpdateProductRequestValidator()
        {
            RuleFor(x => x.Name)
                .MinimumLength(2).WithMessage("Product name must be at least 2 characters long")
                .MaximumLength(255).WithMessage("Product name must not exceed 255 characters")
                .When(x => !string.IsNullOrEmpty(x.Name));

            RuleFor(x => x.Category)
                .MinimumLength(2).WithMessage("Category must be at least 2 characters long")
                .MaximumLength(100).WithMessage("Category must not exceed 100 characters")
                .Must(BeAValidCategory).WithMessage("Category must contain only letters, numbers, spaces, and hyphens")
                .When(x => !string.IsNullOrEmpty(x.Category));

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

            RuleForEach(x => x.Features)
                .NotEmpty().WithMessage("Feature cannot be empty")
                .MaximumLength(200).WithMessage("Each feature must not exceed 200 characters")
                .When(x => x.Features != null && x.Features.Any());

            RuleFor(x => x.Features)
                .Must(features => features == null || features.Count <= 20)
                .WithMessage("Product cannot have more than 20 features");
        }

        private bool BeAValidCategory(string category)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(category, @"^[a-zA-Z0-9\s\-]+$");
        }

        private bool BeAValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
    }
}