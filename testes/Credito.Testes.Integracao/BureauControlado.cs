using Credito.Aplicacao.Portas;
using Credito.Dominio.Comum;

namespace Credito.Testes.Integracao;

/// <summary>
/// Birô com resposta ditada pelo teste.
/// </summary>
/// <remarks>
/// O substituto de produção deriva o score do próprio CPF, o que o torna determinístico
/// mas não dirigível: para exercitar aprovação e negativa seria preciso garimpar CPFs que
/// caem na faixa desejada, e o teste passaria a depender de um resumo criptográfico.
/// </remarks>
public sealed class BureauControlado : IConsultaDeBureau
{
    public int Score { get; set; } = 800;

    public bool TemRestricao { get; set; }

    public void Responder(int score, bool temRestricao = false)
    {
        Score = score;
        TemRestricao = temRestricao;
    }

    public Task<InformacaoDeBureau> Consultar(Cpf cpf, CancellationToken cancelamento) =>
        Task.FromResult(new InformacaoDeBureau(Score, TemRestricao, DateTimeOffset.UnixEpoch));
}
