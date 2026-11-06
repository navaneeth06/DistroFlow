using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DistroFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedEmployeeRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("295f85df-e212-4cbc-8ee1-48b3c6ec2fee"), "295f85df-e212-4cbc-8ee1-48b3c6ec2fee", "Accounts Staff", "ACCOUNTS STAFF" },
                    { new Guid("42e5faac-8ca2-41e8-98bc-f842f205e9aa"), "42e5faac-8ca2-41e8-98bc-f842f205e9aa", "Approver", "APPROVER" },
                    { new Guid("a3a51997-11f6-480c-8c9d-28210ab7e385"), "a3a51997-11f6-480c-8c9d-28210ab7e385", "Dispatch Staff", "DISPATCH STAFF" },
                    { new Guid("b4f5e138-6741-45bb-a395-c971dc8e7962"), "b4f5e138-6741-45bb-a395-c971dc8e7962", "Sales Staff", "SALES STAFF" },
                    { new Guid("c2281575-f1a9-41f8-810d-f53174c2b4d1"), "c2281575-f1a9-41f8-810d-f53174c2b4d1", "Administrator", "ADMINISTRATOR" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("295f85df-e212-4cbc-8ee1-48b3c6ec2fee"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("42e5faac-8ca2-41e8-98bc-f842f205e9aa"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("a3a51997-11f6-480c-8c9d-28210ab7e385"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("b4f5e138-6741-45bb-a395-c971dc8e7962"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("c2281575-f1a9-41f8-810d-f53174c2b4d1"));
        }
    }
}
