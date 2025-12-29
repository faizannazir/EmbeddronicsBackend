using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using EmbeddronicsBackend.Models;

namespace EmbeddronicsBackend.Filters
{
    public class ValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(e => new
                    {
                        Field = x.Key,
                        Message = e.ErrorMessage
                    }))
                    .ToList();

                var response = new ApiResponse<object>
                {
                    Success = false,
                    Message = "Validation failed",
                    Data = null,
                    Errors = errors.Select(e => $"{e.Field}: {e.Message}").ToList(),
                    StatusCode = 400
                };

                context.Result = new BadRequestObjectResult(response);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // No implementation needed
        }
    }
}