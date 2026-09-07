using Credito.Dominio.Politicas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Credito.Infraestrutura.Persistencia.Configuracoes;

internal sealed class PoliticaDeCreditoConfiguracao : IEntityTypeConfiguration<PoliticaDeCredito>
{
    public void Configure(EntityTypeBuilder<PoliticaDeCredito> politica)
    {
        ArgumentNullException.ThrowIfNull(politica);

        politica.ToTable("Politicas");
        politica.HasKey(linha => linha.Id);
        politica.Property(linha => linha.Id).ValueGeneratedNever();

        // Duas politicas com a mesma versao tornariam ambiguo sob qual regra uma decisao
        // antiga foi tomada — que e exatamente o que versionar existe para evitar.
        politica.HasIndex(linha => linha.Versao)
            .IsUnique()
            .HasDatabaseName("IX_Politicas_Versao");

        politica.Property(linha => linha.ComprometimentoMaximo).HasPrecision(9, 6);
        politica.Property(linha => linha.ValorMinimo).HasPrecision(18, 2);
        politica.Property(linha => linha.ValorMaximo).HasPrecision(18, 2);

        politica.HasMany(linha => linha.Faixas)
            .WithOne()
            .HasForeignKey(faixa => faixa.PoliticaId)
            .OnDelete(DeleteBehavior.Cascade);

        politica.Metadata
            .FindNavigation(nameof(PoliticaDeCredito.Faixas))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        SemearPoliticaInicial(politica);
    }

    /// <summary>
    /// Politica versao 1, para o sistema subir analisando.
    /// </summary>
    /// <remarks>
    /// Sem ela a primeira analise falharia por falta de politica vigente, e o sistema so
    /// funcionaria depois de um cadastro manual que nao esta documentado em lugar nenhum.
    /// Identificadores fixos porque semente precisa ser reaplicavel: GUID sorteado geraria
    /// uma politica nova a cada migracao.
    /// </remarks>
    private static void SemearPoliticaInicial(EntityTypeBuilder<PoliticaDeCredito> politica) =>
        politica.HasData(new
        {
            Id = PoliticaInicial,
            Versao = 1,
            ScoreMinimo = 500,
            ComprometimentoMaximo = 0.30m,
            ValorMinimo = 1_000m,
            ValorMaximo = 100_000m,
            PrazoMinimoEmMeses = 6,
            PrazoMaximoEmMeses = 96,
            VigenteDesde = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });

    internal static readonly Guid PoliticaInicial = new("01930000-0000-7000-8000-000000000001");
}

internal sealed class FaixaDeTaxaConfiguracao : IEntityTypeConfiguration<FaixaDeTaxa>
{
    public void Configure(EntityTypeBuilder<FaixaDeTaxa> faixa)
    {
        ArgumentNullException.ThrowIfNull(faixa);

        faixa.ToTable("FaixasDeTaxa");
        faixa.HasKey(linha => linha.Id);
        faixa.Property(linha => linha.Id).ValueGeneratedNever();

        // Seis casas porque a taxa e fracao ao mes: 0,0189 e 1,89% a.m.
        faixa.Property(linha => linha.TaxaMensal).HasPrecision(9, 6);

        faixa.HasIndex(linha => new { linha.PoliticaId, linha.ScoreMinimo })
            .HasDatabaseName("IX_FaixasDeTaxa_Politica_Score");

        // Quanto pior o score, mais cara a taxa. As faixas cobrem a escala inteira para
        // que score abaixo do minimo ainda tenha taxa e o cronograma possa ser montado —
        // a proposta e negada pela regra de score, nao por faltar taxa no meio da analise.
        faixa.HasData(
            Semear("0011", 0, 499, 0.049m),
            Semear("0012", 500, 699, 0.029m),
            Semear("0013", 700, 849, 0.019m),
            Semear("0014", 850, PoliticaDeCredito.ScoreMaximoPossivel, 0.012m));
    }

    private static object Semear(string sufixo, int scoreMinimo, int scoreMaximo, decimal taxaMensal) => new
    {
        Id = new Guid($"01930000-0000-7000-8000-00000000{sufixo}"),
        PoliticaId = PoliticaDeCreditoConfiguracao.PoliticaInicial,
        ScoreMinimo = scoreMinimo,
        ScoreMaximo = scoreMaximo,
        TaxaMensal = taxaMensal,
    };
}
