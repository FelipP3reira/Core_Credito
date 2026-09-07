using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credito.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class IndiceDeListagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Propostas_CriadaEm_Id",
                table: "Propostas",
                columns: new[] { "CriadaEm", "Id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Propostas_CriadaEm_Id",
                table: "Propostas");
        }
    }
}
