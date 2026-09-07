using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Credito.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class MotorDeDecisao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransicoesDeEstado_Proposta_Ocorrencia",
                table: "TransicoesDeEstado");

            migrationBuilder.AddColumn<int>(
                name: "Sequencia",
                table: "TransicoesDeEstado",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Decisoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aprovada = table.Column<bool>(type: "bit", nullable: false),
                    ScoreObservado = table.Column<int>(type: "int", nullable: false),
                    TaxaMensalAplicada = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    VersaoDaPolitica = table.Column<int>(type: "int", nullable: false),
                    AvaliadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Decisoes_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Politicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<int>(type: "int", nullable: false),
                    ScoreMinimo = table.Column<int>(type: "int", nullable: false),
                    ComprometimentoMaximo = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    ValorMinimo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorMaximo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrazoMinimoEmMeses = table.Column<int>(type: "int", nullable: false),
                    PrazoMaximoEmMeses = table.Column<int>(type: "int", nullable: false),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Politicas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AvaliacoesDeRegra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Aprovou = table.Column<bool>(type: "bit", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ValorObservado = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    LimiteExigido = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacoesDeRegra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvaliacoesDeRegra_Decisoes_DecisaoId",
                        column: x => x.DecisaoId,
                        principalTable: "Decisoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FaixasDeTaxa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PoliticaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreMinimo = table.Column<int>(type: "int", nullable: false),
                    ScoreMaximo = table.Column<int>(type: "int", nullable: false),
                    TaxaMensal = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaixasDeTaxa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaixasDeTaxa_Politicas_PoliticaId",
                        column: x => x.PoliticaId,
                        principalTable: "Politicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Politicas",
                columns: new[] { "Id", "ComprometimentoMaximo", "PrazoMaximoEmMeses", "PrazoMinimoEmMeses", "ScoreMinimo", "ValorMaximo", "ValorMinimo", "Versao", "VigenteDesde" },
                values: new object[] { new Guid("01930000-0000-7000-8000-000000000001"), 0.30m, 96, 6, 500, 100000m, 1000m, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                table: "FaixasDeTaxa",
                columns: new[] { "Id", "PoliticaId", "ScoreMaximo", "ScoreMinimo", "TaxaMensal" },
                values: new object[,]
                {
                    { new Guid("01930000-0000-7000-8000-000000000011"), new Guid("01930000-0000-7000-8000-000000000001"), 499, 0, 0.049m },
                    { new Guid("01930000-0000-7000-8000-000000000012"), new Guid("01930000-0000-7000-8000-000000000001"), 699, 500, 0.029m },
                    { new Guid("01930000-0000-7000-8000-000000000013"), new Guid("01930000-0000-7000-8000-000000000001"), 849, 700, 0.019m },
                    { new Guid("01930000-0000-7000-8000-000000000014"), new Guid("01930000-0000-7000-8000-000000000001"), 1000, 850, 0.012m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransicoesDeEstado_Proposta_Sequencia",
                table: "TransicoesDeEstado",
                columns: new[] { "PropostaId", "Sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesDeRegra_Decisao_Ordem",
                table: "AvaliacoesDeRegra",
                columns: new[] { "DecisaoId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_Decisoes_Proposta",
                table: "Decisoes",
                column: "PropostaId");

            migrationBuilder.CreateIndex(
                name: "IX_FaixasDeTaxa_Politica_Score",
                table: "FaixasDeTaxa",
                columns: new[] { "PoliticaId", "ScoreMinimo" });

            migrationBuilder.CreateIndex(
                name: "IX_Politicas_Versao",
                table: "Politicas",
                column: "Versao",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvaliacoesDeRegra");

            migrationBuilder.DropTable(
                name: "FaixasDeTaxa");

            migrationBuilder.DropTable(
                name: "Decisoes");

            migrationBuilder.DropTable(
                name: "Politicas");

            migrationBuilder.DropIndex(
                name: "IX_TransicoesDeEstado_Proposta_Sequencia",
                table: "TransicoesDeEstado");

            migrationBuilder.DropColumn(
                name: "Sequencia",
                table: "TransicoesDeEstado");

            migrationBuilder.CreateIndex(
                name: "IX_TransicoesDeEstado_Proposta_Ocorrencia",
                table: "TransicoesDeEstado",
                columns: new[] { "PropostaId", "OcorridaEm" });
        }
    }
}
