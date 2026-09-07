using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credito.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class ContratacaoELiquidacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ValidadeDaAprovacaoEmDias",
                table: "Politicas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Contratos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValorFinanciado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxaMensal = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Sistema = table.Column<int>(type: "int", nullable: false),
                    PrazoEmMeses = table.Column<int>(type: "int", nullable: false),
                    PrimeiroVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    AssinadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contratos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contratos_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParcelasContratadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContratoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Amortizacao = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Juros = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoDevedor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PagaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ChaveDoPagamento = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelasContratadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParcelasContratadas_Contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Politicas",
                keyColumn: "Id",
                keyValue: new Guid("01930000-0000-7000-8000-000000000001"),
                column: "ValidadeDaAprovacaoEmDias",
                value: 30);

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_Proposta",
                table: "Contratos",
                column: "PropostaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasContratadas_Contrato_Numero",
                table: "ParcelasContratadas",
                columns: new[] { "ContratoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasContratadas_Vencimento_Pagamento",
                table: "ParcelasContratadas",
                columns: new[] { "Vencimento", "PagaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParcelasContratadas");

            migrationBuilder.DropTable(
                name: "Contratos");

            migrationBuilder.DropColumn(
                name: "ValidadeDaAprovacaoEmDias",
                table: "Politicas");
        }
    }
}
