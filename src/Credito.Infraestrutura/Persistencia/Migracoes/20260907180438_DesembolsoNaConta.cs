using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credito.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class DesembolsoNaConta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChaveDoDesembolso",
                table: "Contratos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContaId",
                table: "Contratos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DesembolsadoEm",
                table: "Contratos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LancamentoDoDesembolsoId",
                table: "Contratos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_ContaSemDesembolso",
                table: "Contratos",
                column: "ContaId",
                filter: "[ContaId] IS NOT NULL AND [DesembolsadoEm] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contratos_ContaSemDesembolso",
                table: "Contratos");

            migrationBuilder.DropColumn(
                name: "ChaveDoDesembolso",
                table: "Contratos");

            migrationBuilder.DropColumn(
                name: "ContaId",
                table: "Contratos");

            migrationBuilder.DropColumn(
                name: "DesembolsadoEm",
                table: "Contratos");

            migrationBuilder.DropColumn(
                name: "LancamentoDoDesembolsoId",
                table: "Contratos");
        }
    }
}
