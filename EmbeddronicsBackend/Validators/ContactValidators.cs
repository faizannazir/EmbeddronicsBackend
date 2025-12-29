using FluentValidation;
using EmbeddronicsBackend.Models.DTOs;

namespace EmbeddronicsBackend.Validators
{
    public class ContactFormRequestValidator : AbstractValidator<ContactFormRequest>
    {
        public ContactFormRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MinimumLength(2).WithMessage("Name must be at least 2 characters long")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters")
                .Matches(@"^[a-zA-Z\s\-'\.]+$").WithMessage("Name can only contain letters, spaces, hyphens, apostrophes, and periods");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

            RuleFor(x => x.Company)
                .MaximumLength(255).WithMessage("Company name must not exceed 255 characters")
                .When(x => !string.IsNullOrEmpty(x.Company));

            RuleFor(x => x.Phone)
                .MaximumLength(50).WithMessage("Phone number must not exceed 50 characters")
                .Matches(@"^[\+]?[1-9][\d]{0,15}$").WithMessage("Invalid phone number format")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.Subject)
                .NotEmpty().WithMessage("Subject is required")
                .MinimumLength(5).WithMessage("Subject must be at least 5 characters long")
                .MaximumLength(200).WithMessage("Subject must not exceed 200 characters");

            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("Message is required")
                .MinimumLength(10).WithMessage("Message must be at least 10 characters long")
                .MaximumLength(2000).WithMessage("Message must not exceed 2000 characters");

            RuleFor(x => x.ProjectType)
                .MaximumLength(100).WithMessage("Project type must not exceed 100 characters")
                .Must(BeAValidProjectType).WithMessage("Invalid project type")
                .When(x => !string.IsNullOrEmpty(x.ProjectType));

            RuleFor(x => x.BudgetRange)
                .MaximumLength(100).WithMessage("Budget range must not exceed 100 characters")
                .Matches(@"^\$?\d+(\.\d{2})?\s*-\s*\$?\d+(\.\d{2})?$|^\$?\d+(\.\d{2})?\+?$")
                .WithMessage("Budget range must be in format '$1000-$5000' or '$1000+'")
                .When(x => !string.IsNullOrEmpty(x.BudgetRange));

            RuleFor(x => x.Timeline)
                .MaximumLength(100).WithMessage("Timeline must not exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Timeline));
        }

        private bool BeAValidProjectType(string projectType)
        {
            var validTypes = new[]
            {
                "PCB Design",
                "PCB Assembly",
                "Product Development",
                "Prototyping",
                "Consulting",
                "Repair",
                "Other"
            };
            return validTypes.Contains(projectType);
        }
    }
}