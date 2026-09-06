using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credito.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Propostas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChaveIdempotencia = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ImpressaoDoPedido = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CpfHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CpfCifrado = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NomeSolicitante = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DataDeNascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    RendaMensal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorSolicitado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrazoEmMeses = table.Column<int>(type: "int", nullable: false),
                    Sistema = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Propostas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransicoesDeEstado",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    De = table.Column<int>(type: "int", nullable: false),
                    Para = table.Column<int>(type: "int", nullable: false),
                    OcorridaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransicoesDeEstado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransicoesDeEstado_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_ChaveIdempotencia",
                table: "Propostas",
                column: "ChaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_CpfHash",
                table: "Propostas",
                column: "CpfHash");

            migrationBuilder.CreateIndex(
                name: "IX_TransicoesDeEstado_Proposta_Ocorrencia",
                table: "TransicoesDeEstado",
                columns: new[] { "PropostaId", "OcorridaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransicoesDeEstado");

            migrationBuilder.DropTable(
                name: "Propostas");
        }
    }
}
