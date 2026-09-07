using Credito.Aplicacao.Portas;

namespace Credito.Aplicacao.Consultas;

/// <summary>
/// O que a area de emprestimo do banco mostra quando alguem abre a propria conta.
/// </summary>
/// <remarks>
/// Sem verificar se a conta existe: quem sabe isso e a Plataforma Bancaria, e perguntar a
/// ela aqui inverteria a direcao da dependencia entre os dois servicos. Conta inexistente e
/// conta sem emprestimo devolvem a mesma lista vazia, e a diferenca entre as duas nao muda
/// nada para quem pergunta.
/// </remarks>
public sealed class ListarContratosDaConta
{
    private readonly IConsultaDeContratos contratos;

    public ListarContratosDaConta(IConsultaDeContratos contratos) => this.contratos = contratos;

    public Task<IReadOnlyList<ContratoDaConta>> Executar(Guid contaId, CancellationToken cancelamento) =>
        contratos.DaConta(contaId, cancelamento);
}
