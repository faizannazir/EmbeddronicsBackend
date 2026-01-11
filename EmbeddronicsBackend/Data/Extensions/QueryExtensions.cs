using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Data.Extensions;

/// <summary>
/// Extension methods for optimized database queries.
/// Provides pagination, filtering, sorting, and performance optimizations.
/// </summary>
public static class QueryExtensions
{
    /// <summary>
    /// Apply pagination to a query with optimized counting.
    /// </summary>
    public static async Task<PaginatedResult<T>> ToPaginatedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) where T : class
    {
        // Validate parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Get total count (consider caching for large datasets)
        var totalCount = await query.CountAsync(cancellationToken);
        
        // Calculate pagination
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (page - 1) * pageSize;

        // Get items for current page
        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };
    }

    /// <summary>
    /// Apply cursor-based pagination for better performance on large datasets.
    /// </summary>
    public static async Task<CursorPaginatedResult<T>> ToCursorPaginatedResultAsync<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        TKey? afterCursor,
        int pageSize,
        CancellationToken cancellationToken = default) where T : class where TKey : IComparable<TKey>
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Apply cursor filter if provided
        if (afterCursor != null)
        {
            var parameter = keySelector.Parameters[0];
            var memberAccess = keySelector.Body;
            var constant = Expression.Constant(afterCursor, typeof(TKey));
            var comparison = Expression.GreaterThan(memberAccess, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
            query = query.Where(lambda);
        }

        // Take one extra to check for more items
        var items = await query
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items = items.Take(pageSize).ToList();
        }

        // Get cursor for next page
        TKey? nextCursor = default;
        if (items.Count > 0)
        {
            var compiled = keySelector.Compile();
            nextCursor = compiled(items[^1]);
        }

        return new CursorPaginatedResult<T>
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = nextCursor?.ToString()
        };
    }

    /// <summary>
    /// Apply dynamic sorting to a query.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sortBy,
        bool descending = false) where T : class
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = typeof(T).GetProperty(sortBy, 
            System.Reflection.BindingFlags.IgnoreCase | 
            System.Reflection.BindingFlags.Public | 
            System.Reflection.BindingFlags.Instance);

        if (property == null)
            return query;

        var propertyAccess = Expression.MakeMemberAccess(parameter, property);
        var orderByExp = Expression.Lambda(propertyAccess, parameter);

        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var resultExp = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { typeof(T), property.PropertyType },
            query.Expression,
            Expression.Quote(orderByExp));

        return query.Provider.CreateQuery<T>(resultExp);
    }

    /// <summary>
    /// Apply multiple sorts to a query.
    /// </summary>
    public static IQueryable<T> ApplySorts<T>(
        this IQueryable<T> query,
        IEnumerable<SortDescriptor> sorts) where T : class
    {
        var isFirst = true;
        
        foreach (var sort in sorts)
        {
            if (string.IsNullOrWhiteSpace(sort.Field))
                continue;

            var parameter = Expression.Parameter(typeof(T), "x");
            var property = typeof(T).GetProperty(sort.Field,
                System.Reflection.BindingFlags.IgnoreCase |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            if (property == null)
                continue;

            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var orderByExp = Expression.Lambda(propertyAccess, parameter);

            string methodName;
            if (isFirst)
            {
                methodName = sort.Descending ? "OrderByDescending" : "OrderBy";
                isFirst = false;
            }
            else
            {
                methodName = sort.Descending ? "ThenByDescending" : "ThenBy";
            }

            var resultExp = Expression.Call(
                typeof(Queryable),
                methodName,
                new[] { typeof(T), property.PropertyType },
                query.Expression,
                Expression.Quote(orderByExp));

            query = query.Provider.CreateQuery<T>(resultExp);
        }

        return query;
    }

    /// <summary>
    /// Apply search across multiple string fields.
    /// </summary>
    public static IQueryable<T> ApplySearch<T>(
        this IQueryable<T> query,
        string? searchTerm,
        params Expression<Func<T, string>>[] searchFields) where T : class
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchFields.Length == 0)
            return query;

        var searchTermLower = searchTerm.ToLower();
        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combinedExpression = null;

        foreach (var field in searchFields)
        {
            // Get the property access from the field expression
            var memberExp = (MemberExpression)field.Body;
            var propertyAccess = Expression.Property(parameter, memberExp.Member.Name);

            // Create: property != null && property.ToLower().Contains(searchTerm)
            var notNull = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            var toLower = Expression.Call(propertyAccess, typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
            var contains = Expression.Call(toLower, typeof(string).GetMethod("Contains", new[] { typeof(string) })!, 
                Expression.Constant(searchTermLower));
            var condition = Expression.AndAlso(notNull, contains);

            combinedExpression = combinedExpression == null 
                ? condition 
                : Expression.OrElse(combinedExpression, condition);
        }

        if (combinedExpression == null)
            return query;

        var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);
        return query.Where(lambda);
    }

    /// <summary>
    /// Apply date range filter.
    /// </summary>
    public static IQueryable<T> ApplyDateRange<T>(
        this IQueryable<T> query,
        Expression<Func<T, DateTime>> dateSelector,
        DateTime? startDate,
        DateTime? endDate) where T : class
    {
        if (startDate.HasValue)
        {
            var parameter = dateSelector.Parameters[0];
            var memberAccess = dateSelector.Body;
            var startConstant = Expression.Constant(startDate.Value);
            var startComparison = Expression.GreaterThanOrEqual(memberAccess, startConstant);
            var startLambda = Expression.Lambda<Func<T, bool>>(startComparison, parameter);
            query = query.Where(startLambda);
        }

        if (endDate.HasValue)
        {
            var parameter = dateSelector.Parameters[0];
            var memberAccess = dateSelector.Body;
            var endConstant = Expression.Constant(endDate.Value.AddDays(1)); // Include entire end day
            var endComparison = Expression.LessThan(memberAccess, endConstant);
            var endLambda = Expression.Lambda<Func<T, bool>>(endComparison, parameter);
            query = query.Where(endLambda);
        }

        return query;
    }

    /// <summary>
    /// Apply filter for items in a list of values.
    /// </summary>
    public static IQueryable<T> ApplyInFilter<T, TValue>(
        this IQueryable<T> query,
        Expression<Func<T, TValue>> valueSelector,
        IEnumerable<TValue>? values) where T : class
    {
        var valuesList = values?.ToList();
        if (valuesList == null || valuesList.Count == 0)
            return query;

        var parameter = valueSelector.Parameters[0];
        var memberAccess = valueSelector.Body;
        
        var containsMethod = typeof(List<TValue>).GetMethod("Contains", new[] { typeof(TValue) })!;
        var listConstant = Expression.Constant(valuesList);
        var containsCall = Expression.Call(listConstant, containsMethod, memberAccess);
        
        var lambda = Expression.Lambda<Func<T, bool>>(containsCall, parameter);
        return query.Where(lambda);
    }

    /// <summary>
    /// Select only specific fields for projection (reduces data transfer).
    /// </summary>
    public static IQueryable<TResult> SelectFields<T, TResult>(
        this IQueryable<T> query,
        Expression<Func<T, TResult>> selector) where T : class
    {
        return query.Select(selector);
    }

    /// <summary>
    /// Apply no-tracking for read-only queries (better performance).
    /// </summary>
    public static IQueryable<T> AsReadOnly<T>(this IQueryable<T> query) where T : class
    {
        return query.AsNoTracking();
    }

    /// <summary>
    /// Split query for better performance when loading related data.
    /// </summary>
    public static IQueryable<T> UseSplitQuery<T>(this IQueryable<T> query) where T : class
    {
        return query.AsSplitQuery();
    }

    /// <summary>
    /// Include related entities with explicit loading for better control.
    /// </summary>
    public static IQueryable<T> IncludeIf<T, TProperty>(
        this IQueryable<T> query,
        bool condition,
        Expression<Func<T, TProperty>> navigationPropertyPath) where T : class
    {
        return condition ? query.Include(navigationPropertyPath) : query;
    }
}

/// <summary>
/// Result model for paginated queries.
/// </summary>
public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

/// <summary>
/// Result model for cursor-based pagination.
/// </summary>
public class CursorPaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public bool HasMore { get; set; }
    public string? NextCursor { get; set; }
}

/// <summary>
/// Descriptor for sorting.
/// </summary>
public class SortDescriptor
{
    public string Field { get; set; } = string.Empty;
    public bool Descending { get; set; }
}
