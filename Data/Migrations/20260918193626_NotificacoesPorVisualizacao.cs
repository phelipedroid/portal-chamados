using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalSanMarco.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificacoesPorVisualizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChamadosVisualizacoes",
                columns: table => new
                {
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChamadoId = table.Column<int>(type: "int", nullable: false),
                    VisualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChamadosVisualizacoes", x => new { x.UsuarioId, x.ChamadoId });
                    table.ForeignKey(
                        name: "FK_ChamadosVisualizacoes_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChamadosVisualizacoes_Chamados_ChamadoId",
                        column: x => x.ChamadoId,
                        principalTable: "Chamados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChamadosVisualizacoes_ChamadoId_VisualizadoEm",
                table: "ChamadosVisualizacoes",
                columns: new[] { "ChamadoId", "VisualizadoEm" });

            // As interações anteriores à implantação começam como lidas,
            // evitando que o sino seja preenchido com o histórico de testes.
            migrationBuilder.Sql(
                """
                INSERT INTO [ChamadosVisualizacoes]
                    ([UsuarioId], [ChamadoId], [VisualizadoEm])
                SELECT
                    [usuario].[Id],
                    [chamado].[Id],
                    GETDATE()
                FROM [AspNetUsers] AS [usuario]
                CROSS JOIN [Chamados] AS [chamado];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChamadosVisualizacoes");
        }
    }
}
