using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmbeddronicsBackend.Migrations
{
    /// <summary>
    /// Migration to add performance-optimized indexes for frequently accessed data
    /// </summary>
    public partial class AddPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ==========================================
            // User indexes for authentication and lookup
            // ==========================================
            
            // Email is already unique indexed, add composite indexes for common queries
            migrationBuilder.CreateIndex(
                name: "IX_Users_Role_Status",
                table: "Users",
                columns: new[] { "Role", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Status_CreatedAt",
                table: "Users",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Company",
                table: "Users",
                column: "Company");

            // ==========================================
            // Order indexes for dashboard and client queries
            // ==========================================

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientId_Status",
                table: "Orders",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientId_CreatedAt",
                table: "Orders",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedAt_Status",
                table: "Orders",
                columns: new[] { "CreatedAt", "Status" });

            // ==========================================
            // Quote indexes for workflow and client queries
            // ==========================================

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Status",
                table: "Quotes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ClientId_Status",
                table: "Quotes",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ExpiresAt",
                table: "Quotes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Status_ExpiresAt",
                table: "Quotes",
                columns: new[] { "Status", "ExpiresAt" });

            // ==========================================
            // Product indexes for catalog queries
            // ==========================================

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category_IsActive",
                table: "Products",
                columns: new[] { "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_CreatedAt",
                table: "Products",
                columns: new[] { "IsActive", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            // ==========================================
            // Invoice indexes for financial queries
            // ==========================================

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status_DueDate",
                table: "Invoices",
                columns: new[] { "Status", "DueDate" });

            // ==========================================
            // Message indexes for chat performance
            // ==========================================

            migrationBuilder.CreateIndex(
                name: "IX_Messages_OrderId_CreatedAt",
                table: "Messages",
                columns: new[] { "OrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId_CreatedAt",
                table: "Messages",
                columns: new[] { "SenderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_IsRead",
                table: "Messages",
                column: "IsRead");

            // ==========================================
            // Document indexes for file management
            // ==========================================

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OrderId_FileType",
                table: "Documents",
                columns: new[] { "OrderId", "FileType" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedById_CreatedAt",
                table: "Documents",
                columns: new[] { "UploadedById", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop User indexes
            migrationBuilder.DropIndex(name: "IX_Users_Role_Status", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_Status_CreatedAt", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_Company", table: "Users");

            // Drop Order indexes
            migrationBuilder.DropIndex(name: "IX_Orders_Status", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Orders_ClientId_Status", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Orders_ClientId_CreatedAt", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Orders_CreatedAt_Status", table: "Orders");

            // Drop Quote indexes
            migrationBuilder.DropIndex(name: "IX_Quotes_Status", table: "Quotes");
            migrationBuilder.DropIndex(name: "IX_Quotes_ClientId_Status", table: "Quotes");
            migrationBuilder.DropIndex(name: "IX_Quotes_ExpiresAt", table: "Quotes");
            migrationBuilder.DropIndex(name: "IX_Quotes_Status_ExpiresAt", table: "Quotes");

            // Drop Product indexes
            migrationBuilder.DropIndex(name: "IX_Products_Category_IsActive", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Products_IsActive_CreatedAt", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Products_Name", table: "Products");

            // Drop Invoice indexes
            migrationBuilder.DropIndex(name: "IX_Invoices_Status", table: "Invoices");
            migrationBuilder.DropIndex(name: "IX_Invoices_DueDate", table: "Invoices");
            migrationBuilder.DropIndex(name: "IX_Invoices_Status_DueDate", table: "Invoices");

            // Drop Message indexes
            migrationBuilder.DropIndex(name: "IX_Messages_OrderId_CreatedAt", table: "Messages");
            migrationBuilder.DropIndex(name: "IX_Messages_SenderId_CreatedAt", table: "Messages");
            migrationBuilder.DropIndex(name: "IX_Messages_IsRead", table: "Messages");

            // Drop Document indexes
            migrationBuilder.DropIndex(name: "IX_Documents_OrderId_FileType", table: "Documents");
            migrationBuilder.DropIndex(name: "IX_Documents_UploadedById_CreatedAt", table: "Documents");
        }
    }
}
