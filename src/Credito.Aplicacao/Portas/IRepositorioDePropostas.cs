using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Portas;

public sealed record ResultadoDaInsercao(Proposta Proposta, bool Nova);

public interface IRepositorioDePropostas
{
    Task<Proposta?> PorId(Guid id, CancellationToken cancelamento);

    /// <summary>
    /// Grava a proposta, ou devolve a que ja existe com a mesma chave de idempotencia.
    /// A decisao acontece no banco, contra o indice unico — checar antes e inserir depois
    /// deixaria uma janela entre as duas operacoes por onde dois envios simultaneos passam.
    /// </summary>
    Task<ResultadoDaInsercao> InserirSeNova(Proposta proposta, CancellationToken cancelamento);

    Task Salvar(CancellationToken cancelamento);
}
