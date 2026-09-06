using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Comum;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Propostas;

public sealed class CadastrarProposta
{
    private readonly IRepositorioDePropostas repositorio;
    private readonly IProtetorDeCpf protetor;
    private readonly TimeProvider relogio;

    public CadastrarProposta(IRepositorioDePropostas repositorio, IProtetorDeCpf protetor, TimeProvider relogio)
    {
        this.repositorio = repositorio;
        this.protetor = protetor;
        this.relogio = relogio;
    }

    public async Task<PropostaCadastrada> Executar(CadastroDeProposta cadastro, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(cadastro);

        var impressao = ImpressaoDoPedido.Calcular(cadastro);
        var novaProposta = Proposta.Rascunho(Montar(cadastro, impressao), relogio.GetUtcNow());

        var resultado = await repositorio.InserirSeNova(novaProposta, cancelamento).ConfigureAwait(false);

        if (!resultado.Nova && !string.Equals(resultado.Proposta.ImpressaoDoPedido, impressao, StringComparison.Ordinal))
        {
            throw new ChaveDeIdempotenciaReutilizadaException();
        }

        return new PropostaCadastrada(resultado.Proposta.Id, resultado.Proposta.Estado, !resultado.Nova);
    }

    private DadosDaProposta Montar(CadastroDeProposta cadastro, string impressao) =>
        new(
            cadastro.ChaveIdempotencia,
            impressao,
            protetor.Proteger(Cpf.Criar(cadastro.Cpf)),
            cadastro.NomeSolicitante,
            cadastro.DataDeNascimento,
            cadastro.RendaMensal,
            cadastro.ValorSolicitado,
            cadastro.PrazoEmMeses,
            cadastro.Sistema);
}
