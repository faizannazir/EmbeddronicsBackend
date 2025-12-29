using Microsoft.AspNetCore.Mvc;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Models.Exceptions;
using EmbeddronicsBackend.Authorization.Attributes;
using Microsoft.AspNetCore.Authorization;
using Serilog;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : BaseApiController
    {
        private readonly IDataService<Product> _productService;

        public ProductsController(IDataService<Product> productService)
        {
            _productService = productService;
        }

        [HttpGet]
        [AllowAnonymous] // Public access for product catalog
        public async Task<ActionResult<ApiResponse<IEnumerable<Product>>>> GetAll()
        {
            Log.Information("Products list accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            
            var products = await _productService.GetAllAsync();
            return Success(products, "Products retrieved successfully");
        }

        [HttpGet("{id}")]
        [AllowAnonymous] // Public access for individual products
        public async Task<ActionResult<ApiResponse<Product>>> GetById(int id)
        {
            Log.Information("Product {Id} accessed by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            
            if (id <= 0)
            {
                throw new ValidationException("id", "Product ID must be greater than 0");
            }

            var product = await _productService.GetByIdAsync(id);
            if (product == null)
            {
                throw new NotFoundException("Product", id);
            }

            return Success(product, "Product retrieved successfully");
        }

        [HttpPost]
        [ManageProducts] // Admin-only access for creating products
        public async Task<ActionResult<ApiResponse<Product>>> Create([FromBody] Product product)
        {
            Log.Information("Creating new product by admin user: {User}", User?.Identity?.Name ?? "anonymous");
            
            if (product == null)
            {
                throw new ValidationException("product", "Product data is required");
            }

            if (string.IsNullOrWhiteSpace(product.Name))
            {
                throw new ValidationException("name", "Product name is required");
            }

            var created = await _productService.CreateAsync(product);
            Log.Information("Product {ProductId} created successfully by admin: {User}", created.Id, User?.Identity?.Name);
            
            return Success(created, "Product created successfully");
        }

        [HttpPut("{id}")]
        [ManageProducts] // Admin-only access for updating products
        public async Task<ActionResult<ApiResponse<Product>>> Update(int id, [FromBody] Product product)
        {
            Log.Information("Updating product {Id} by admin user: {User}", id, User?.Identity?.Name ?? "anonymous");
            
            if (id <= 0)
            {
                throw new ValidationException("id", "Product ID must be greater than 0");
            }

            if (product == null)
            {
                throw new ValidationException("product", "Product data is required");
            }

            var updated = await _productService.UpdateAsync(id, product);
            if (updated == null)
            {
                throw new NotFoundException("Product", id);
            }

            Log.Information("Product {ProductId} updated successfully by admin: {User}", id, User?.Identity?.Name);
            return Success(updated, "Product updated successfully");
        }

        [HttpDelete("{id}")]
        [ManageProducts] // Admin-only access for deleting products
        public async Task<ActionResult<ApiResponse>> Delete(int id)
        {
            Log.Information("Deleting product {Id} by admin user: {User}", id, User?.Identity?.Name ?? "anonymous");
            
            if (id <= 0)
            {
                throw new ValidationException("id", "Product ID must be greater than 0");
            }

            var result = await _productService.DeleteAsync(id);
            if (!result)
            {
                throw new NotFoundException("Product", id);
            }

            Log.Information("Product {ProductId} deleted successfully by admin: {User}", id, User?.Identity?.Name);
            return Success("Product deleted successfully");
        }
    }
}
