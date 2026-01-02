using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CnjProcessosApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Processos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumeroCNJ = table.Column<string>(type: "TEXT", nullable: false),
                    Classe = table.Column<string>(type: "TEXT", nullable: true),
                    Assunto = table.Column<string>(type: "TEXT", nullable: true),
                    OrgaoJulgador = table.Column<string>(type: "TEXT", nullable: true),
                    IsSigiloso = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    UltimaAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NomePartesAnonimizados = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Movimentacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movimentacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movimentacoes_Processos_ProcessoId",
                        column: x => x.ProcessoId,
                        principalTable: "Processos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movimentacoes_ProcessoId",
                table: "Movimentacoes",
                column: "ProcessoId");

            migrationBuilder.CreateIndex(
                name: "IX_Processos_NumeroCNJ",
                table: "Processos",
                column: "NumeroCNJ",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Movimentacoes");

            migrationBuilder.DropTable(
                name: "Processos");
        }
    }
}
