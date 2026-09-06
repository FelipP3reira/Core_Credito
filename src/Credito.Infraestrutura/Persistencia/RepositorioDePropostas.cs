using Credito.Aplicacao.Portas;
using Credito.Dominio.Propostas;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Credito.Infraestrutura.Persistencia;

public sealed class RepositorioDePropostas : IRepositorioDePropostas
{
    private const int ViolacaoDeChavePrimaria = 2627;
    private const int ViolacaoDeIndiceUnico = 2601;

    private readonly ContextoDeCredito contexto;

    public RepositorioDePropostas(ContextoDeCredito contexto) => this.contexto = contexto;

    public Task<Proposta?> PorId(Guid id, CancellationToken cancelamento) =>
        contexto.Propostas
            .Include(proposta => proposta.Transicoes)
            .FirstOrDefaultAsync(proposta => proposta.Id == id, cancelamento);

    /// <remarks>
    /// Tenta inserir e trata a colisao, em vez de consultar antes: entre a consulta e o
    /// insert cabem duas requisicoes simultaneas com a mesma chave, e as duas passariam.
    /// O indice unico e o unico ponto do sistema onde essa corrida se resolve de verdade.
    /// </remarks>
    public async Task<ResultadoDaInsercao> InserirSeNova(Proposta proposta, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        contexto.Propostas.Add(proposta);

        try
        {
            await contexto.SaveChangesAsync(cancelamento).ConfigureAwait(false);
            return new ResultadoDaInsercao(proposta, Nova: true);
        }
        catch (DbUpdateException erro) when (EhViolacaoDeUnicidade(erro))
        {
            // A proposta recusada continua marcada como Added e voltaria no proximo
            // SaveChanges desta requisicao. Nada mais estava pendente aqui, entao
            // limpar o rastreamento inteiro e mais seguro que caçar entrada por entrada.
            contexto.ChangeTracker.Clear();

            var existente = await PorChaveIdempotencia(proposta.ChaveIdempotencia, cancelamento)
                .ConfigureAwait(false);

            // Sem linha com a mesma chave, a colisao foi em outro indice — nao e
            // reenvio, e engolir o erro esconderia um defeito de verdade.
            if (existente is null)
            {
                throw;
            }

            return new ResultadoDaInsercao(existente, Nova: false);
        }
    }

    public Task Salvar(CancellationToken cancelamento) => contexto.SaveChangesAsync(cancelamento);

    private Task<Proposta?> PorChaveIdempotencia(string chave, CancellationToken cancelamento) =>
        contexto.Propostas
            .Include(proposta => proposta.Transicoes)
            .FirstOrDefaultAsync(proposta => proposta.ChaveIdempotencia == chave, cancelamento);

    private static bool EhViolacaoDeUnicidade(DbUpdateException erro) =>
        erro.InnerException is SqlException falha
        && falha.Number is ViolacaoDeChavePrimaria or ViolacaoDeIndiceUnico;
}
