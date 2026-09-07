using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Comum;
using Credito.Dominio.Politicas;

namespace Credito.Infraestrutura.Bureau;

/// <summary>
/// Substituto do birô de crédito enquanto não há integração de verdade.
/// </summary>
/// <remarks>
/// Deriva score e restrição de um resumo do próprio CPF, então o mesmo solicitante recebe
/// sempre a mesma resposta. Determinismo aqui não é detalhe: sem ele, reanalisar a mesma
/// proposta daria resultado diferente a cada vez e nenhuma demonstração seria reproduzível.
/// <para>
/// Não é aleatoriedade com fins estatísticos — é um espaço reservado com forma de contrato.
/// A integração real entra trocando esta classe no registro de dependências, sem tocar em
/// nada do domínio.
/// </para>
/// </remarks>
public sealed class BureauSimulado : IConsultaDeBureau
{
    private const int UmEmCadaDezTemRestricao = 10;

    private readonly TimeProvider relogio;

    public BureauSimulado(TimeProvider relogio) => this.relogio = relogio;

    public Task<InformacaoDeBureau> Consultar(Cpf cpf, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        var resumo = SHA256.HashData(Encoding.UTF8.GetBytes(cpf.Numero));

        var score = (int)(BinaryPrimitives.ReadUInt32BigEndian(resumo) % (PoliticaDeCredito.ScoreMaximoPossivel + 1));
        var temRestricao = resumo[^1] % UmEmCadaDezTemRestricao == 0;

        return Task.FromResult(new InformacaoDeBureau(score, temRestricao, relogio.GetUtcNow()));
    }
}
