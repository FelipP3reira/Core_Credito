using Credito.Dominio.Erros;

namespace Credito.Dominio.Comum;

/// <summary>
/// CPF em forma protegida: o que a proposta guarda e o que vai para o banco.
/// </summary>
/// <remarks>
/// O agregado nunca segura o CPF em texto claro — nem em memoria. Quem precisa do
/// numero real decifra sob demanda na borda, o que reduz o vazamento por despejo
/// de memoria, serializacao acidental e log de objeto inteiro a zero.
/// <para>
/// Em troca, a validade do CPF deixa de ser reconferivel dentro do agregado: ela e
/// garantida no unico caminho que produz esta classe, que exige um <see cref="Cpf"/>
/// ja validado.
/// </para>
/// <para>
/// <see cref="Hash"/> e deterministico e serve de indice — e o que permite achar
/// proposta por CPF sem decifrar nada. <see cref="Cifrado"/> e reversivel e nao
/// se repete entre duas cifragens do mesmo numero.
/// </para>
/// </remarks>
public sealed record CpfProtegido
{
    public CpfProtegido(string hash, string cifrado)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new CpfInvalidoException("Hash do CPF ausente.");
        }

        if (string.IsNullOrWhiteSpace(cifrado))
        {
            throw new CpfInvalidoException("CPF cifrado ausente.");
        }

        Hash = hash;
        Cifrado = cifrado;
    }

    public string Hash { get; }

    public string Cifrado { get; }
}
