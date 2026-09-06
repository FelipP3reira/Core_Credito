using Credito.Dominio.Propostas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Credito.Infraestrutura.Persistencia.Configuracoes;

internal sealed class TransicaoDeEstadoConfiguracao : IEntityTypeConfiguration<TransicaoDeEstado>
{
    public void Configure(EntityTypeBuilder<TransicaoDeEstado> transicao)
    {
        ArgumentNullException.ThrowIfNull(transicao);

        transicao.ToTable("TransicoesDeEstado");
        transicao.HasKey(linha => linha.Id);

        transicao.Property(linha => linha.De).HasConversion<int>();
        transicao.Property(linha => linha.Para).HasConversion<int>();
        transicao.Property(linha => linha.Origem).HasMaxLength(64).IsRequired();

        // Consulta de auditoria e sempre "o que aconteceu com esta proposta, em ordem".
        transicao.HasIndex(linha => new { linha.PropostaId, linha.OcorridaEm })
            .HasDatabaseName("IX_TransicoesDeEstado_Proposta_Ocorrencia");
    }
}
