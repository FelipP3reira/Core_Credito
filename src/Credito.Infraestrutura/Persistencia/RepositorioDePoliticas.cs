using Credito.Aplicacao.Portas;
using Credito.Dominio.Politicas;
using Microsoft.EntityFrameworkCore;

namespace Credito.Infraestrutura.Persistencia;

public sealed class RepositorioDePoliticas : IRepositorioDePoliticas
{
    private readonly ContextoDeCredito contexto;

    public RepositorioDePoliticas(ContextoDeCredito contexto) => this.contexto = contexto;

    /// <remarks>
    /// Ordena pela versao, e nao pela data de vigencia: duas politicas publicadas para o
    /// mesmo instante empatariam na data, e a mais nova e sempre a de versao maior.
    /// </remarks>
    public Task<PoliticaDeCredito?> Vigente(DateTimeOffset em, CancellationToken cancelamento) =>
        contexto.Politicas
            .AsNoTracking()
            .Include(politica => politica.Faixas)
            .Where(politica => politica.VigenteDesde <= em)
            .OrderByDescending(politica => politica.Versao)
            .FirstOrDefaultAsync(cancelamento);
}
