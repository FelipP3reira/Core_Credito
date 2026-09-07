using Credito.Dominio.Propostas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Credito.Infraestrutura.Persistencia.Configuracoes;

internal sealed class PropostaConfiguracao : IEntityTypeConfiguration<Proposta>
{
    public void Configure(EntityTypeBuilder<Proposta> proposta)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        proposta.ToTable("Propostas");
        proposta.HasKey(linha => linha.Id);

        // O Id sai do GUID v7 gerado no construtor, nunca do banco. Sem dizer isso ao
        // EF, ele trata chave Guid como gerada na insercao e passa a usar a heuristica
        // "chave preenchida significa registro existente" — o que faz uma transicao
        // recem-criada virar UPDATE em vez de INSERT, atingir zero linhas e estourar
        // como conflito de concorrencia.
        proposta.Property(linha => linha.Id).ValueGeneratedNever();

        proposta.Property(linha => linha.ChaveIdempotencia).HasMaxLength(64).IsRequired();

        // SHA-256 em base64 tem 44 caracteres e nunca cresce: sem o limite o EF
        // criaria nvarchar(max), que o SQL Server pode guardar fora da linha.
        proposta.Property(linha => linha.ImpressaoDoPedido).HasMaxLength(64).IsRequired();

        // Indice unico e a garantia de verdade da idempotencia: reenvio simultaneo
        // do mesmo formulario bate aqui, no banco, e nao numa checagem antes do insert
        // que duas requisicoes concorrentes passariam juntas.
        proposta.HasIndex(linha => linha.ChaveIdempotencia)
            .IsUnique()
            .HasDatabaseName("IX_Propostas_ChaveIdempotencia");

        // A listagem ordena por CriadaEm e desempata pelo Id, e o marcador de pagina
        // corta pelos mesmos dois campos na mesma ordem. Sem um indice com essa forma
        // exata, toda pagina vira varredura da tabela seguida de ordenacao — paginacao
        // por cursor sem indice de apoio e a mesma leitura completa com outro nome.
        proposta.HasIndex(linha => new { linha.CriadaEm, linha.Id })
            .IsDescending(true, true)
            .HasDatabaseName("IX_Propostas_CriadaEm_Id");

        proposta.OwnsOne(linha => linha.Cpf, cpf =>
        {
            cpf.Property(valor => valor.Hash).HasColumnName("CpfHash").HasMaxLength(64).IsRequired();
            cpf.Property(valor => valor.Cifrado).HasColumnName("CpfCifrado").HasMaxLength(128).IsRequired();

            // Busca por CPF acontece pelo hash; a coluna cifrada nunca e criterio.
            cpf.HasIndex(valor => valor.Hash).HasDatabaseName("IX_Propostas_CpfHash");
        });

        proposta.Navigation(linha => linha.Cpf).IsRequired();

        proposta.Property(linha => linha.NomeSolicitante)
            .HasMaxLength(Proposta.TamanhoMaximoDoNome)
            .IsRequired();

        proposta.Property(linha => linha.DataDeNascimento).HasColumnType("date");
        proposta.Property(linha => linha.RendaMensal).HasPrecision(18, 2);
        proposta.Property(linha => linha.ValorSolicitado).HasPrecision(18, 2);
        proposta.Property(linha => linha.Estado).HasConversion<int>();
        proposta.Property(linha => linha.Sistema).HasConversion<int>();

        proposta.Property(linha => linha.Versao).IsRowVersion();

        proposta.HasMany(linha => linha.Transicoes)
            .WithOne()
            .HasForeignKey(transicao => transicao.PropostaId)
            .OnDelete(DeleteBehavior.Restrict);

        // O agregado expoe o historico como lista somente leitura: o EF precisa
        // escrever no campo por tras dela, nao na propriedade.
        proposta.Metadata
            .FindNavigation(nameof(Proposta.Transicoes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        proposta.HasMany(linha => linha.Decisoes)
            .WithOne()
            .HasForeignKey(decisao => decisao.PropostaId)
            .OnDelete(DeleteBehavior.Restrict);

        proposta.Metadata
            .FindNavigation(nameof(Proposta.Decisoes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
