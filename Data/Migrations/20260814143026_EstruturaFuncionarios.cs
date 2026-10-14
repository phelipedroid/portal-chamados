using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PortalSanMarco.Data.Migrations
{
    /// <inheritdoc />
    public partial class EstruturaFuncionarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Setores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Setores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Unidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unidades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FuncionariosPerfis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NomeCompleto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadeId = table.Column<int>(type: "int", nullable: false),
                    SetorId = table.Column<int>(type: "int", nullable: false),
                    Matricula = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Cargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    DeveTrocarSenha = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuncionariosPerfis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuncionariosPerfis_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FuncionariosPerfis_Setores_SetorId",
                        column: x => x.SetorId,
                        principalTable: "Setores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FuncionariosPerfis_Unidades_UnidadeId",
                        column: x => x.UnidadeId,
                        principalTable: "Unidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Setores",
                columns: new[] { "Id", "Ativo", "Nome" },
                values: new object[,]
                {
                    { 1, true, "Tecnologia da Informação" },
                    { 2, true, "Administrativo" },
                    { 3, true, "Financeiro" },
                    { 4, true, "Comercial / Vendas" },
                    { 5, true, "Oficina" },
                    { 6, true, "Peças" },
                    { 7, true, "Pós-vendas" },
                    { 8, true, "Recursos Humanos" },
                    { 9, true, "Contabilidade" },
                    { 10, true, "Marketing" },
                    { 11, true, "Diretoria" },
                    { 12, true, "Recepção" },
                    { 13, true, "Locadora" },
                    { 14, true, "Consórcio" }
                });

            migrationBuilder.InsertData(
                table: "Unidades",
                columns: new[] { "Id", "Ativa", "Nome" },
                values: new object[,]
                {
                    { 1, true, "Fiat" },
                    { 2, true, "Peugeot" },
                    { 3, true, "Citroën" },
                    { 4, true, "Jeep" },
                    { 5, true, "RAM" },
                    { 6, true, "Caoa Chery" },
                    { 7, true, "Seminovos" },
                    { 8, true, "Muve Locadora" },
                    { 9, true, "Consórcio Guarapiranga" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosPerfis_SetorId",
                table: "FuncionariosPerfis",
                column: "SetorId");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosPerfis_UnidadeId",
                table: "FuncionariosPerfis",
                column: "UnidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosPerfis_UsuarioId",
                table: "FuncionariosPerfis",
                column: "UsuarioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FuncionariosPerfis");

            migrationBuilder.DropTable(
                name: "Setores");

            migrationBuilder.DropTable(
                name: "Unidades");
        }
    }
}
