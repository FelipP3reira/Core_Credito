using Credito.Dominio.Decisoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Credito.Infraestrutura.Persistencia.Configuracoes;

internal sealed class DecisaoConfiguracao : IEntityTypeConfiguration<Decisao>
{
    public void Configure(EntityTypeBuilder<Decisao> decisao)
    {
        ArgumentNullException.ThrowIfNull(decisao);

        decisao.ToTable("Decisoes");
        decisao.HasKey(linha => linha.Id);
        decisao.Property(linha => linha.Id).ValueGeneratedNever();

        decisao.Property(linha => linha.TaxaMensalAplicada).HasPrecision(9, 6);

        decisao.HasMany(linha => linha.Avaliacoes)
            .WithOne()
            .HasForeignKey(avaliacao => avaliacao.DecisaoId)
            .OnDelete(DeleteBehavior.Restrict);

        decisao.Metadata
            .FindNavigation(nameof(Decisao.Avaliacoes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Reprovacoes e projecao sobre a colecao ja carregada, nao coluna.
        decisao.Ignore(linha => linha.Reprovacoes);

        decisao.HasIndex(linha => linha.PropostaId).HasDatabaseName("IX_Decisoes_Proposta");
    }
}

internal sealed class AvaliacaoDeRegraConfiguracao : IEntityTypeConfiguration<AvaliacaoDeRegra>
{
    public void Configure(EntityTypeBuilder<AvaliacaoDeRegra> avaliacao)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);

        avaliacao.ToTable("AvaliacoesDeRegra");
        avaliacao.HasKey(linha => linha.Id);
        avaliacao.Property(linha => linha.Id).ValueGeneratedNever();

        avaliacao.Property(linha => linha.Codigo).HasMaxLength(64).IsRequired();
        avaliacao.Property(linha => linha.Motivo).HasMaxLength(300).IsRequired();

        // Seis casas cobrem tanto score inteiro quanto fracao de comprometimento.
        avaliacao.Property(linha => linha.ValorObservado).HasPrecision(18, 6);
        avaliacao.Property(linha => linha.LimiteExigido).HasPrecision(18, 6);

        // O laudo e sempre lido inteiro e em ordem.
        avaliacao.HasIndex(linha => new { linha.DecisaoId, linha.Ordem })
            .HasDatabaseName("IX_AvaliacoesDeRegra_Decisao_Ordem");
    }
}
