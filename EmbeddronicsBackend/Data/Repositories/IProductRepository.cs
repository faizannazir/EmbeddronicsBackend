using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Data.Repositories;

public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetActiveProductsAsync();
    Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category);
    Task<IEnumerable<Product>> GetProductsByPriceRangeAsync(decimal minPrice, decimal maxPrice);
    Task<IEnumerable<string>> GetCategoriesAsync();
    Task<bool> ToggleProductStatusAsync(int id);
    Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm);
    Task<Product?> GetProductWithQuoteItemsAsync(int id);
    Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count);
}