using Credito.Dominio.Contratos;
using Credito.Dominio.Politicas;
using Credito.Dominio.Propostas;
using Microsoft.EntityFrameworkCore;

namespace Credito.Infraestrutura.Persistencia;

public sealed class ContextoDeCredito : DbContext
{
    public ContextoDeCredito(DbContextOptions<ContextoDeCredito> opcoes) : base(opcoes)
    {
    }

    public DbSet<Proposta> Propostas => Set<Proposta>();

    public DbSet<PoliticaDeCredito> Politicas => Set<PoliticaDeCredito>();

    public DbSet<Contrato> Contratos => Set<Contrato>();

    // Nome do parametro imposto pela assinatura da classe base.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContextoDeCredito).Assembly);
    }
}
