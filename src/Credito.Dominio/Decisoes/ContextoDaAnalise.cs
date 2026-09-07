using Credito.Dominio.Amortizacao;
using Credito.Dominio.Politicas;

namespace Credito.Dominio.Decisoes;

/// <summary>
/// Tudo que as regras precisam saber, congelado antes de qualquer uma rodar.
/// </summary>
/// <remarks>
/// A analise acontece em duas fases porque as regras tem dependencia entre si se
/// deixadas soltas: o comprometimento de renda precisa da parcela, que precisa da taxa,
/// que sai da faixa de score. Montar tudo antes e avaliar depois faz de cada regra uma
/// funcao pura deste registro — o teste unitario e montar o contexto na mao e chamar
/// Avaliar, sem simulacao de dependencia, sem banco, sem ordem.
/// <para>
/// <see cref="Cronograma"/> e nulo quando valor, taxa e prazo pedidos nao se representam
/// em centavos. Nesse caso a regra de comprometimento reprova dizendo isso, em vez de
/// derrubar a analise inteira — proposta impossivel merece laudo, nao erro de servidor.
/// </para>
/// </remarks>
public sealed record ContextoDaAnalise(
    Guid PropostaId,
    decimal ValorSolicitado,
    int PrazoEmMeses,
    decimal RendaMensal,
    int Score,
    bool TemRestricaoCadastral,
    decimal TaxaMensalAplicada,
    Cronograma? Cronograma,
    PoliticaDeCredito Politica);
