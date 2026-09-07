using Credito.Aplicacao.Portas;
using Credito.Dominio.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Credito.Infraestrutura.Persistencia;

public sealed class RepositorioDeContratos : IRepositorioDeContratos
{
    private readonly ContextoDeCredito contexto;

    public RepositorioDeContratos(ContextoDeCredito contexto) => this.contexto = contexto;

    /// <remarks>
    /// Traz as parcelas junto porque nao existe operacao de contrato que dispense o
    /// cronograma: pagar precisa achar a parcela, e consultar precisa mostrar todas.
    /// </remarks>
    public Task<Contrato?> PorProposta(Guid propostaId, CancellationToken cancelamento) =>
        contexto.Contratos
            .Include(contrato => contrato.Parcelas)
            .FirstOrDefaultAsync(contrato => contrato.PropostaId == propostaId, cancelamento);

    public void Adicionar(Contrato contrato) => contexto.Contratos.Add(contrato);
}

public sealed class UnidadeDeTrabalho : IUnidadeDeTrabalho
{
    private readonly ContextoDeCredito contexto;

    public UnidadeDeTrabalho(ContextoDeCredito contexto) => this.contexto = contexto;

    public Task Salvar(CancellationToken cancelamento) => contexto.SaveChangesAsync(cancelamento);
}
