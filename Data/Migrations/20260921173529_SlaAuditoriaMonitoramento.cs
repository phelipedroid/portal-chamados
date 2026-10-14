using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalSanMarco.Data.Migrations
{
    /// <inheritdoc />
    public partial class SlaAuditoriaMonitoramento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlaMinutosPausados",
                table: "Chamados",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaPausadoEm",
                table: "Chamados",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [Chamados] SET [SlaPausadoEm] = GETDATE() " +
                "WHERE [Status] IN (3, 4) AND [ResolvidoEm] IS NULL;");

            migrationBuilder.CreateTable(
                name: "AuditoriasAdministrativas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UsuarioEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Acao = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Entidade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntidadeId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EnderecoIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriasAdministrativas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasAdministrativas_CriadoEm",
                table: "AuditoriasAdministrativas",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasAdministrativas_Entidade_EntidadeId",
                table: "AuditoriasAdministrativas",
                columns: new[] { "Entidade", "EntidadeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriasAdministrativas");

            migrationBuilder.DropColumn(
                name: "SlaMinutosPausados",
                table: "Chamados");

            migrationBuilder.DropColumn(
                name: "SlaPausadoEm",
                table: "Chamados");
        }
    }
}
