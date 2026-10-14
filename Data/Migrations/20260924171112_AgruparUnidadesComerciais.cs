using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalSanMarco.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgruparUnidadesComerciais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE origem
                FROM GerentesUnidades AS origem
                WHERE origem.UnidadeId = 3
                  AND EXISTS (
                      SELECT 1
                      FROM GerentesUnidades AS destino
                      WHERE destino.UsuarioId = origem.UsuarioId
                        AND destino.UnidadeId = 2
                  );

                UPDATE GerentesUnidades SET UnidadeId = 2 WHERE UnidadeId = 3;
                UPDATE FuncionariosPerfis SET UnidadeId = 2 WHERE UnidadeId = 3;
                UPDATE Chamados SET UnidadeId = 2 WHERE UnidadeId = 3;

                DELETE origem
                FROM GerentesUnidades AS origem
                WHERE origem.UnidadeId = 5
                  AND EXISTS (
                      SELECT 1
                      FROM GerentesUnidades AS destino
                      WHERE destino.UsuarioId = origem.UsuarioId
                        AND destino.UnidadeId = 4
                  );

                UPDATE GerentesUnidades SET UnidadeId = 4 WHERE UnidadeId = 5;
                UPDATE FuncionariosPerfis SET UnidadeId = 4 WHERE UnidadeId = 5;
                UPDATE Chamados SET UnidadeId = 4 WHERE UnidadeId = 5;
                """);

            migrationBuilder.UpdateData(
                table: "Unidades",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nome",
                value: "Peugeot e Citroën");

            migrationBuilder.UpdateData(
                table: "Unidades",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Ativa", "Nome" },
                values: new object[] { false, "Citroën (agrupada)" });

            migrationBuilder.UpdateData(
                table: "Unidades",
                keyColumn: "Id",
                keyValue: 4,
                column: "Nome",
                value: "Jeep / RAM");

            migrationBuilder.UpdateData(
                table: "Unidades",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Ativa", "Nome" },
                values: new object[] { false, "RAM (agrupada)" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Unidades",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nome",
                value: "Peugeot");

            migrationBuilder.UpdateData(
                table: "Unidades",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Ativa", "Nome" },
                values: new object[] { true, "Citroën" });

            migrationBuilder.UpdateData(
                table: "Unidades",
                keyColumn: "Id",
                keyValue: 4,
                column: "Nome",
                value: "Jeep");

            migrationBuilder.UpdateData(
                table: "Unidades",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Ativa", "Nome" },
                values: new object[] { true, "RAM" });
        }
    }
}
