using FluentValidation;
using EmbeddronicsBackend.Models.DTOs;

namespace EmbeddronicsBackend.Validators
{
    public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
    {
        public CreateOrderRequestValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Order title is required")
                .MinimumLength(3).WithMessage("Order title must be at least 3 characters long")
                .MaximumLength(255).WithMessage("Order title must not exceed 255 characters");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.BudgetRange)
                .MaximumLength(100).WithMessage("Budget range must not exceed 100 characters")
                .Matches(@"^\$?\d+(\.\d{2})?\s*-\s*\$?\d+(\.\d{2})?$|^\$?\d+(\.\d{2})?\+?$")
                .WithMessage("Budget range must be in format '$1000-$5000' or '$1000+'")
                .When(x => !string.IsNullOrEmpty(x.BudgetRange));

            RuleFor(x => x.Timeline)
                .MaximumLength(100).WithMessage("Timeline must not exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Timeline));

            RuleFor(x => x.PcbSpecs)
                .SetValidator(new PcbSpecsDtoValidator())
                .When(x => x.PcbSpecs != null);
        }
    }

    public class UpdateOrderRequestValidator : AbstractValidator<UpdateOrderRequest>
    {
        public UpdateOrderRequestValidator()
        {
            RuleFor(x => x.Title)
                .MinimumLength(3).WithMessage("Order title must be at least 3 characters long")
                .MaximumLength(255).WithMessage("Order title must not exceed 255 characters")
                .When(x => !string.IsNullOrEmpty(x.Title));

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.BudgetRange)
                .MaximumLength(100).WithMessage("Budget range must not exceed 100 characters")
                .Matches(@"^\$?\d+(\.\d{2})?\s*-\s*\$?\d+(\.\d{2})?$|^\$?\d+(\.\d{2})?\+?$")
                .WithMessage("Budget range must be in format '$1000-$5000' or '$1000+'")
                .When(x => !string.IsNullOrEmpty(x.BudgetRange));

            RuleFor(x => x.Timeline)
                .MaximumLength(100).WithMessage("Timeline must not exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Timeline));

            RuleFor(x => x.Status)
                .Must(status => new[] { "new", "in_progress", "completed", "cancelled" }.Contains(status))
                .WithMessage("Status must be one of: new, in_progress, completed, cancelled")
                .When(x => !string.IsNullOrEmpty(x.Status));

            RuleFor(x => x.PcbSpecs)
                .SetValidator(new PcbSpecsDtoValidator())
                .When(x => x.PcbSpecs != null);
        }
    }

    public class PcbSpecsDtoValidator : AbstractValidator<PcbSpecsDto?>
    {
        public PcbSpecsDtoValidator()
        {
            RuleFor(x => x!.BoardType)
                .MaximumLength(100).WithMessage("Board type must not exceed 100 characters")
                .When(x => x != null && !string.IsNullOrEmpty(x.BoardType));

            RuleFor(x => x!.Layers)
                .MaximumLength(50).WithMessage("Layers specification must not exceed 50 characters")
                .Must(PcbSpecsValidationRules.IsValidLayerCount).WithMessage("Invalid layer count. Must be 1, 2, or even numbers from 4-32, or a range like '4-6'")
                .When(x => x != null && !string.IsNullOrEmpty(x.Layers));

            RuleFor(x => x!.Dimensions)
                .MaximumLength(100).WithMessage("Dimensions must not exceed 100 characters")
                .Must(PcbSpecsValidationRules.IsValidDimensions).WithMessage("Dimensions must be in format '100x50mm', '10x5cm', or '4x3in'")
                .When(x => x != null && !string.IsNullOrEmpty(x.Dimensions));

            RuleFor(x => x!.Material)
                .MaximumLength(100).WithMessage("Material must not exceed 100 characters")
                .When(x => x != null && !string.IsNullOrEmpty(x.Material));

            RuleFor(x => x!.Thickness)
                .MaximumLength(50).WithMessage("Thickness must not exceed 50 characters")
                .Must(PcbSpecsValidationRules.IsValidThickness).WithMessage("Thickness must be a number with optional unit (e.g., '1.6mm', '62mil')")
                .When(x => x != null && !string.IsNullOrEmpty(x.Thickness));

            RuleFor(x => x!.Quantity)
                .MaximumLength(50).WithMessage("Quantity must not exceed 50 characters")
                .Matches(@"^\d+$").WithMessage("Quantity must be a positive number")
                .When(x => x != null && !string.IsNullOrEmpty(x.Quantity));
        }
    }

    public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
    {
        public SendMessageRequestValidator()
        {
            RuleFor(x => x.OrderId)
                .GreaterThan(0).WithMessage("Order ID must be a positive number");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Message content is required")
                .MinimumLength(1).WithMessage("Message content cannot be empty")
                .MaximumLength(2000).WithMessage("Message content must not exceed 2000 characters");

            RuleForEach(x => x.Attachments)
                .SetValidator(new AttachmentDtoValidator())
                .When(x => x.Attachments != null && x.Attachments.Any());
        }
    }

    public class AttachmentDtoValidator : AbstractValidator<AttachmentDto>
    {
        public AttachmentDtoValidator()
        {
            RuleFor(x => x.FileName)
                .NotEmpty().WithMessage("File name is required")
                .MaximumLength(255).WithMessage("File name must not exceed 255 characters")
                .Matches(@"^[^<>:""/\\|?*]+$").WithMessage("File name contains invalid characters");

            RuleFor(x => x.FileUrl)
                .NotEmpty().WithMessage("File URL is required")
                .MaximumLength(500).WithMessage("File URL must not exceed 500 characters")
                .Must(BeAValidUrl).WithMessage("File URL must be a valid URL");

            RuleFor(x => x.FileType)
                .NotEmpty().WithMessage("File type is required")
                .MaximumLength(100).WithMessage("File type must not exceed 100 characters");

            RuleFor(x => x.FileSize)
                .GreaterThan(0).WithMessage("File size must be greater than 0")
                .LessThanOrEqualTo(50 * 1024 * 1024).WithMessage("File size must not exceed 50MB");
        }

        private bool BeAValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
    }
}