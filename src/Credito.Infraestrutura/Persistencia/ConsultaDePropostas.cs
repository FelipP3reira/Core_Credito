using Credito.Aplicacao.Consultas;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Propostas;
using Microsoft.EntityFrameworkCore;

namespace Credito.Infraestrutura.Persistencia;

public sealed class ConsultaDePropostas : IConsultaDePropostas
{
    private readonly ContextoDeCredito contexto;

    public ConsultaDePropostas(ContextoDeCredito contexto) => this.contexto = contexto;

    public async Task<IReadOnlyList<LinhaDaProposta>> Listar(
        FiltroDeLeitura filtro,
        CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = Filtrada(filtro);

        if (filtro.Continuacao is { } marcador)
        {
            // Mesma ordem do ORDER BY, escrita como corte: tudo que vem estritamente
            // depois da ultima linha entregue. O desempate pelo id nao precisa ser
            // cronologico — precisa so ser o mesmo criterio dos dois lados.
            consulta = consulta.Where(proposta =>
                proposta.CriadaEm < marcador.CriadaEm
                || (proposta.CriadaEm == marcador.CriadaEm
                    && proposta.Id.CompareTo(marcador.Id) < 0));
        }

        return await consulta
            .OrderByDescending(proposta => proposta.CriadaEm)
            .ThenByDescending(proposta => proposta.Id)
            .Take(filtro.Tamanho)
            .Select(proposta => new LinhaDaProposta(
                proposta.Id,
                proposta.Estado,
                proposta.NomeSolicitante,
                proposta.ValorSolicitado,
                proposta.PrazoEmMeses,
                proposta.Sistema,
                proposta.CriadaEm,
                proposta.AtualizadaEm))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ContagemPorEstado>> Resumir(
        DateTimeOffset? de,
        DateTimeOffset? ate,
        CancellationToken cancelamento)
    {
        // Agrupado no banco: trazer as linhas para contar em memoria colocaria a carteira
        // inteira dentro do processo so para devolver oito numeros.
        var grupos = await NoPeriodo(contexto.Propostas.AsNoTracking(), de, ate)
            .GroupBy(proposta => proposta.Estado)
            .Select(grupo => new
            {
                Estado = grupo.Key,
                Quantidade = grupo.Count(),
                ValorSolicitado = grupo.Sum(proposta => proposta.ValorSolicitado),
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        // Ordenar aqui e barato: sao no maximo oito linhas, uma por estado possivel.
        return [.. grupos
            .OrderBy(grupo => grupo.Estado)
            .Select(grupo => new ContagemPorEstado(
                grupo.Estado,
                grupo.Quantidade,
                grupo.ValorSolicitado))];
    }

    private IQueryable<Proposta> Filtrada(FiltroDeLeitura filtro)
    {
        var consulta = NoPeriodo(contexto.Propostas.AsNoTracking(), filtro.De, filtro.Ate);

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(proposta => proposta.Estado == estado);
        }

        if (filtro.CpfHash is { } hash)
        {
            consulta = consulta.Where(proposta => proposta.Cpf.Hash == hash);
        }

        return consulta;
    }

    private static IQueryable<Proposta> NoPeriodo(
        IQueryable<Proposta> consulta,
        DateTimeOffset? de,
        DateTimeOffset? ate)
    {
        if (de is { } inicio)
        {
            consulta = consulta.Where(proposta => proposta.CriadaEm >= inicio);
        }

        if (ate is { } fim)
        {
            consulta = consulta.Where(proposta => proposta.CriadaEm <= fim);
        }

        return consulta;
    }
}
