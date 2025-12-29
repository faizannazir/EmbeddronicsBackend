using FluentValidation;
using EmbeddronicsBackend.Data;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Validators
{
    /// <summary>
    /// Custom validator for PCB specifications
    /// </summary>
    public static class PcbSpecsValidationRules
    {
        public static bool IsValidLayerCount(string? layers)
        {
            if (string.IsNullOrEmpty(layers))
                return true;

            // Valid layer counts: 1, 2, 4, 6, 8, 10, 12, etc. (even numbers after 2)
            if (int.TryParse(layers, out int layerCount))
            {
                return layerCount == 1 || layerCount == 2 || (layerCount >= 4 && layerCount % 2 == 0 && layerCount <= 32);
            }

            // Also allow ranges like "4-6"
            if (layers.Contains('-'))
            {
                var parts = layers.Split('-');
                if (parts.Length == 2 && 
                    int.TryParse(parts[0].Trim(), out int min) && 
                    int.TryParse(parts[1].Trim(), out int max))
                {
                    return IsValidLayerCount(min.ToString()) && IsValidLayerCount(max.ToString()) && min <= max;
                }
            }

            return false;
        }

        public static bool IsValidDimensions(string? dimensions)
        {
            if (string.IsNullOrEmpty(dimensions))
                return true;

            // Match patterns like "100x50mm", "10x5cm", "4x3in", "100 x 50 mm"
            var pattern = @"^\d+(\.\d+)?\s*[xX×]\s*\d+(\.\d+)?\s*(mm|cm|in|MM|CM|IN)?$";
            return System.Text.RegularExpressions.Regex.IsMatch(dimensions, pattern);
        }

        public static bool IsValidThickness(string? thickness)
        {
            if (string.IsNullOrEmpty(thickness))
                return true;

            // Match patterns like "1.6mm", "62mil", "0.8", "1.6 mm"
            var pattern = @"^\d+(\.\d+)?\s*(mm|mil|μm|um|MM|MIL|UM)?$";
            return System.Text.RegularExpressions.Regex.IsMatch(thickness, pattern);
        }
    }

    /// <summary>
    /// Custom validator for business rules around quotes
    /// </summary>
    public static class QuoteBusinessRules
    {
        public static bool IsValidQuoteAmount(decimal amount, string? budgetRange)
        {
            if (string.IsNullOrEmpty(budgetRange))
                return true;

            // Extract budget range and validate quote is within reasonable bounds
            var pattern = @"\$?(\d+(?:\.\d{2})?)\s*-\s*\$?(\d+(?:\.\d{2})?)|^\$?(\d+(?:\.\d{2})?)\+?$";
            var match = System.Text.RegularExpressions.Regex.Match(budgetRange, pattern);

            if (match.Success)
            {
                if (match.Groups[3].Success) // Format like "$1000+"
                {
                    if (decimal.TryParse(match.Groups[3].Value, out decimal minBudget))
                    {
                        return amount >= minBudget * 0.8m; // Allow 20% below minimum
                    }
                }
                else if (match.Groups[1].Success && match.Groups[2].Success) // Format like "$1000-$5000"
                {
                    if (decimal.TryParse(match.Groups[1].Value, out decimal minBudget) &&
                        decimal.TryParse(match.Groups[2].Value, out decimal maxBudget))
                    {
                        return amount >= minBudget * 0.8m && amount <= maxBudget * 1.5m; // Allow some flexibility
                    }
                }
            }

            return true; // If we can't parse the budget range, don't restrict
        }

        public static bool IsValidQuoteExpiration(DateTime validUntil)
        {
            var now = DateTime.UtcNow;
            var maxExpiration = now.AddYears(1);
            var minExpiration = now.AddDays(1);

            return validUntil >= minExpiration && validUntil <= maxExpiration;
        }
    }

    /// <summary>
    /// Custom validator for order status transitions
    /// </summary>
    public static class OrderStatusRules
    {
        private static readonly Dictionary<string, string[]> ValidTransitions = new()
        {
            { "new", new[] { "in_progress", "cancelled" } },
            { "in_progress", new[] { "completed", "cancelled" } },
            { "completed", new string[] { } }, // No transitions from completed
            { "cancelled", new string[] { } }  // No transitions from cancelled
        };

        public static bool IsValidStatusTransition(string currentStatus, string newStatus)
        {
            if (currentStatus == newStatus)
                return true;

            if (ValidTransitions.TryGetValue(currentStatus.ToLower(), out var allowedTransitions))
            {
                return allowedTransitions.Contains(newStatus.ToLower());
            }

            return false;
        }

        public static string[] GetValidNextStatuses(string currentStatus)
        {
            return ValidTransitions.TryGetValue(currentStatus.ToLower(), out var transitions) 
                ? transitions 
                : Array.Empty<string>();
        }
    }
}