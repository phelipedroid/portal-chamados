using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalSanMarco.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPrazoChamado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PrazoEm",
                table: "Chamados",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrazoEm",
                table: "Chamados");
        }
    }
}
