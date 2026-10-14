using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalSanMarco.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificacoesEmailChamados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailsNotificacoes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChamadoId = table.Column<int>(type: "int", nullable: true),
                    Destinatario = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Assunto = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ConteudoHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProximaTentativaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    EnviadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoErro = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailsNotificacoes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailsNotificacoes_ChamadoId",
                table: "EmailsNotificacoes",
                column: "ChamadoId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailsNotificacoes_EnviadoEm_ProximaTentativaEm",
                table: "EmailsNotificacoes",
                columns: new[] { "EnviadoEm", "ProximaTentativaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailsNotificacoes");
        }
    }
}
