using EmbeddronicsBackend.Models;

namespace EmbeddronicsBackend.Services
{
    public class ProductService : JsonDataService<Product>
    {
        public ProductService() : base("products.json") { }
    }

    public class ServiceService : JsonDataService<Service>
    {
        public ServiceService() : base("services.json") { }
    }

    public class ProjectService : JsonDataService<Project>
    {
        public ProjectService() : base("projects.json") { }
    }

    public class BlogService : JsonDataService<BlogPost>
    {
        public BlogService() : base("blogposts.json") { }
    }

    public class OrderService : JsonDataService<Order>
    {
        public OrderService() : base("orders.json") { }
    }

    public class ClientService : JsonDataService<Client>
    {
        public ClientService() : base("clients.json") { }
    }

    public class LeadService : JsonDataService<Lead>
    {
        public LeadService() : base("leads.json") { }
    }

    public class ReviewService : JsonDataService<Review>
    {
        public ReviewService() : base("reviews.json") { }
    }

    public class FinancialService : JsonDataService<FinancialRecord>
    {
        public FinancialService() : base("financials.json") { }
    }

    public class QuoteService : JsonDataService<Quote>
    {
        public QuoteService() : base("quotes.json") { }
    }
}
