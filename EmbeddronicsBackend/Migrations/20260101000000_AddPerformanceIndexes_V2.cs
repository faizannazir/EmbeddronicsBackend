using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmbeddronicsBackend.Migrations
{
    /// <summary>
    /// Migration to add performance indexes for optimized queries.
    /// Run this after the initial database setup.
    /// </summary>
    public partial class AddPerformanceIndexes_V2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================================
            // Users Table Indexes
            // ============================================================================
            
            // Index for role-based filtering (admin dashboards, role checks)
            migrationBuilder.CreateIndex(
                name: "IX_Users_Role",
                table: "Users",
                column: "Role");
            
            // Index for status-based filtering (pending approvals, active users)
            migrationBuilder.CreateIndex(
                name: "IX_Users_Status",
                table: "Users",
                column: "Status");
            
            // Composite index for role + status queries
            migrationBuilder.CreateIndex(
                name: "IX_Users_Role_Status",
                table: "Users",
                columns: new[] { "Role", "Status" });
            
            // Index for creation date ordering
            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAt",
                table: "Users",
                column: "CreatedAt");
            
            // Full-text search support (if using SQL Server)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Users'))
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'EmbeddronicsFullTextCatalog')
                        CREATE FULLTEXT CATALOG EmbeddronicsFullTextCatalog AS DEFAULT;
                    
                    CREATE FULLTEXT INDEX ON Users(Name, Email, Company)
                    KEY INDEX PK_Users
                    ON EmbeddronicsFullTextCatalog;
                END
            ");

            // ============================================================================
            // Orders Table Indexes
            // ============================================================================
            
            // Index for status filtering (most common query pattern)
            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");
            
            // Composite index for client + status (client dashboard queries)
            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientId_Status",
                table: "Orders",
                columns: new[] { "ClientId", "Status" });
            
            // Index for date-based queries and ordering
            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedAt_Desc",
                table: "Orders",
                column: "CreatedAt",
                descending: new[] { true });
            
            // Composite index for dashboard overview (recent orders by status)
            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            // ============================================================================
            // Products Table Indexes
            // ============================================================================
            
            // Index for category filtering (product listing page)
            migrationBuilder.CreateIndex(
                name: "IX_Products_Category",
                table: "Products",
                column: "Category");
            
            // Index for active products only
            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive",
                table: "Products",
                column: "IsActive");
            
            // Composite index for active products by category
            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_Category",
                table: "Products",
                columns: new[] { "IsActive", "Category" });
            
            // Index for price range queries
            migrationBuilder.CreateIndex(
                name: "IX_Products_Price",
                table: "Products",
                column: "Price");

            // ============================================================================
            // Quotes Table Indexes
            // ============================================================================
            
            // Index for status filtering
            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Status",
                table: "Quotes",
                column: "Status");
            
            // Composite index for client quotes
            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ClientId_Status",
                table: "Quotes",
                columns: new[] { "ClientId", "Status" });
            
            // Index for date-based sorting
            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CreatedAt",
                table: "Quotes",
                column: "CreatedAt",
                descending: new[] { true });

            // ============================================================================
            // Invoices Table Indexes
            // ============================================================================
            
            // Index for status filtering
            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");
            
            // Composite index for financial reporting
            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status_CreatedAt",
                table: "Invoices",
                columns: new[] { "Status", "CreatedAt" });
            
            // Index for due date tracking
            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices",
                column: "DueDate");

            // ============================================================================
            // Messages Table Indexes
            // ============================================================================
            
            // Index for unread messages query
            migrationBuilder.CreateIndex(
                name: "IX_Messages_IsRead",
                table: "Messages",
                column: "IsRead");
            
            // Composite index for order messages (chat feature)
            migrationBuilder.CreateIndex(
                name: "IX_Messages_OrderId_CreatedAt",
                table: "Messages",
                columns: new[] { "OrderId", "CreatedAt" });

            // ============================================================================
            // Documents Table Indexes
            // ============================================================================
            
            // Index for file type filtering
            migrationBuilder.CreateIndex(
                name: "IX_Documents_FileType",
                table: "Documents",
                column: "FileType");
            
            // Index for order documents
            migrationBuilder.CreateIndex(
                name: "IX_Documents_OrderId_CreatedAt",
                table: "Documents",
                columns: new[] { "OrderId", "CreatedAt" });

            // ============================================================================
            // Database Statistics and Query Optimization
            // ============================================================================
            
            // Enable auto-create and auto-update statistics
            migrationBuilder.Sql(@"
                ALTER DATABASE CURRENT SET AUTO_CREATE_STATISTICS ON;
                ALTER DATABASE CURRENT SET AUTO_UPDATE_STATISTICS ON;
                ALTER DATABASE CURRENT SET AUTO_UPDATE_STATISTICS_ASYNC ON;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all custom indexes
            migrationBuilder.DropIndex(name: "IX_Users_Role", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_Status", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_Role_Status", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_CreatedAt", table: "Users");
            
            migrationBuilder.DropIndex(name: "IX_Orders_Status", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Orders_ClientId_Status", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Orders_CreatedAt_Desc", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Orders_Status_CreatedAt", table: "Orders");
            
            migrationBuilder.DropIndex(name: "IX_Products_Category", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Products_IsActive", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Products_IsActive_Category", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Products_Price", table: "Products");
            
            migrationBuilder.DropIndex(name: "IX_Quotes_Status", table: "Quotes");
            migrationBuilder.DropIndex(name: "IX_Quotes_ClientId_Status", table: "Quotes");
            migrationBuilder.DropIndex(name: "IX_Quotes_CreatedAt", table: "Quotes");
            
            migrationBuilder.DropIndex(name: "IX_Invoices_Status", table: "Invoices");
            migrationBuilder.DropIndex(name: "IX_Invoices_Status_CreatedAt", table: "Invoices");
            migrationBuilder.DropIndex(name: "IX_Invoices_DueDate", table: "Invoices");
            
            migrationBuilder.DropIndex(name: "IX_Messages_IsRead", table: "Messages");
            migrationBuilder.DropIndex(name: "IX_Messages_OrderId_CreatedAt", table: "Messages");
            
            migrationBuilder.DropIndex(name: "IX_Documents_FileType", table: "Documents");
            migrationBuilder.DropIndex(name: "IX_Documents_OrderId_CreatedAt", table: "Documents");
            
            // Drop full-text index
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Users'))
                    DROP FULLTEXT INDEX ON Users;
            ");
        }
    }
}
