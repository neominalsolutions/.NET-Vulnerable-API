using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VulnerableAPI.Migrations
{
    /// <inheritdoc />
    public partial class First : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Password = table.Column<string>(type: "text", nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    CreditCardNumber = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "Name", "Price", "Stock" },
                values: new object[,]
                {
                    { 1, "Electronics", new DateTime(2026, 2, 15, 16, 55, 13, 923, DateTimeKind.Utc).AddTicks(2170), "High-performance laptop", "Laptop", 1299.99m, 50 },
                    { 2, "Electronics", new DateTime(2026, 2, 15, 16, 55, 13, 923, DateTimeKind.Utc).AddTicks(2173), "Wireless mouse", "Mouse", 29.99m, 200 },
                    { 3, "Electronics", new DateTime(2026, 2, 15, 16, 55, 13, 923, DateTimeKind.Utc).AddTicks(2175), "Mechanical keyboard", "Keyboard", 89.99m, 150 },
                    { 4, "Electronics", new DateTime(2026, 2, 15, 16, 55, 13, 923, DateTimeKind.Utc).AddTicks(2176), "4K Monitor 27 inch", "Monitor", 399.99m, 75 },
                    { 5, "Accessories", new DateTime(2026, 2, 15, 16, 55, 13, 923, DateTimeKind.Utc).AddTicks(2177), "USB-C cable 2m", "USB Cable", 12.99m, 500 }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Address", "CreatedAt", "CreditCardNumber", "Email", "FullName", "IsAdmin", "Password", "PhoneNumber", "Username" },
                values: new object[,]
                {
                    { 1, "123 Admin Street", new DateTime(2026, 2, 15, 16, 55, 13, 923, DateTimeKind.Utc).AddTicks(2021), "4532-1234-5678-9010", "admin@vulnerable.api", "System Administrator", true, "admin123", "555-0001", "admin" },
                    { 2, "456 User Lane", new DateTime(2026, 2, 15, 16, 55, 13, 923, DateTimeKind.Utc).AddTicks(2022), "4532-9876-5432-1098", "john@vulnerable.api", "John Doe", false, "12345", "555-0002", "john" },
                    { 3, "789 Customer Blvd", new DateTime(2026, 2, 15, 16, 55, 13, 923, DateTimeKind.Utc).AddTicks(2024), "4532-1111-2222-3333", "jane@vulnerable.api", "Jane Smith", false, "password", "555-0003", "jane" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
