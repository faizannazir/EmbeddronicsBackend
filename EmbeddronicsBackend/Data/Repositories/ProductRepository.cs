using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Data.Repositories;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(EmbeddronicsDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Product>> GetActiveProductsAsync()
    {
        return await _dbSet
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        return await _dbSet
            .Where(p => p.Category == category && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetProductsByPriceRangeAsync(decimal minPrice, decimal maxPrice)
    {
        return await _dbSet
            .Where(p => p.Price >= minPrice && p.Price <= maxPrice && p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync()
    {
        return await _dbSet
            .Where(p => p.IsActive)
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task<bool> ToggleProductStatusAsync(int id)
    {
        var product = await _dbSet.FindAsync(id);
        if (product == null)
            return false;

        product.IsActive = !product.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm)
    {
        return await _dbSet
            .Where(p => p.IsActive && 
                       (p.Name.Contains(searchTerm) || 
                        p.Description!.Contains(searchTerm) || 
                        p.Category.Contains(searchTerm)))
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Product?> GetProductWithQuoteItemsAsync(int id)
    {
        return await _dbSet
            .Include(p => p.QuoteItems)
                .ThenInclude(qi => qi.Quote)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count)
    {
        return await _dbSet
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}