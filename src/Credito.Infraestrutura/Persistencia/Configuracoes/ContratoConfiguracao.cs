using Credito.Dominio.Contratos;
using Credito.Dominio.Propostas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Credito.Infraestrutura.Persistencia.Configuracoes;

internal sealed class ContratoConfiguracao : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> contrato)
    {
        ArgumentNullException.ThrowIfNull(contrato);

        contrato.ToTable("Contratos");
        contrato.HasKey(linha => linha.Id);
        contrato.Property(linha => linha.Id).ValueGeneratedNever();

        // Um contrato por proposta. A maquina de estados ja impede o segundo, mas quem
        // garante mesmo com duas requisicoes simultaneas e o indice.
        contrato.HasIndex(linha => linha.PropostaId)
            .IsUnique()
            .HasDatabaseName("IX_Contratos_Proposta");

        // Chave estrangeira sem navegacao do outro lado de proposito: carregar uma proposta
        // nao pode arrastar noventa e seis parcelas junto.
        contrato.HasOne<Proposta>()
            .WithMany()
            .HasForeignKey(linha => linha.PropostaId)
            .OnDelete(DeleteBehavior.Restrict);

        contrato.Property(linha => linha.ValorFinanciado).HasPrecision(18, 2);
        contrato.Property(linha => linha.TaxaMensal).HasPrecision(9, 6);
        contrato.Property(linha => linha.Sistema).HasConversion<int>();
        contrato.Property(linha => linha.PrimeiroVencimento).HasColumnType("date");

        contrato.HasMany(linha => linha.Parcelas)
            .WithOne()
            .HasForeignKey(parcela => parcela.ContratoId)
            .OnDelete(DeleteBehavior.Cascade);

        contrato.Metadata
            .FindNavigation(nameof(Contrato.Parcelas))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Contas sobre as parcelas ja carregadas, nao colunas.
        contrato.Ignore(linha => linha.EstaQuitado);
        contrato.Ignore(linha => linha.TotalPago);
        contrato.Ignore(linha => linha.SaldoAberto);
    }
}

internal sealed class ParcelaContratadaConfiguracao : IEntityTypeConfiguration<ParcelaContratada>
{
    public void Configure(EntityTypeBuilder<ParcelaContratada> parcela)
    {
        ArgumentNullException.ThrowIfNull(parcela);

        parcela.ToTable("ParcelasContratadas");
        parcela.HasKey(linha => linha.Id);
        parcela.Property(linha => linha.Id).ValueGeneratedNever();

        parcela.Property(linha => linha.Vencimento).HasColumnType("date");
        parcela.Property(linha => linha.Amortizacao).HasPrecision(18, 2);
        parcela.Property(linha => linha.Juros).HasPrecision(18, 2);
        parcela.Property(linha => linha.Valor).HasPrecision(18, 2);
        parcela.Property(linha => linha.SaldoDevedor).HasPrecision(18, 2);
        parcela.Property(linha => linha.ChaveDoPagamento).HasMaxLength(64);

        parcela.Ignore(linha => linha.EstaPaga);

        // O cronograma e sempre lido inteiro e em ordem; o par tambem impede duas linhas
        // com o mesmo numero de parcela no mesmo contrato.
        parcela.HasIndex(linha => new { linha.ContratoId, linha.Numero })
            .IsUnique()
            .HasDatabaseName("IX_ParcelasContratadas_Contrato_Numero");

        // Quem procura parcela vencida e nao paga varre por data.
        parcela.HasIndex(linha => new { linha.Vencimento, linha.PagaEm })
            .HasDatabaseName("IX_ParcelasContratadas_Vencimento_Pagamento");
    }
}
