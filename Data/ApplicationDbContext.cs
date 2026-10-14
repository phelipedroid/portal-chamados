using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Models;

namespace PortalSanMarco.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Unidade> Unidades => Set<Unidade>();

    public DbSet<Setor> Setores => Set<Setor>();

    public DbSet<FuncionarioPerfil> FuncionariosPerfis
        => Set<FuncionarioPerfil>();

    public DbSet<GerenteUnidade> GerentesUnidades
        => Set<GerenteUnidade>();

    public DbSet<CategoriaChamado> CategoriasChamados
        => Set<CategoriaChamado>();

    public DbSet<Chamado> Chamados => Set<Chamado>();

    public DbSet<ChamadoVisualizacao> ChamadosVisualizacoes
        => Set<ChamadoVisualizacao>();

    public DbSet<HistoricoChamado> HistoricosChamados
        => Set<HistoricoChamado>();

    public DbSet<AuditoriaAdministrativa> AuditoriasAdministrativas
        => Set<AuditoriaAdministrativa>();

    public DbSet<EmailNotificacao> EmailsNotificacoes
        => Set<EmailNotificacao>();

    public DbSet<ComentarioChamado> ComentariosChamados
    => Set<ComentarioChamado>();

public DbSet<AnexoChamado> AnexosChamados
    => Set<AnexoChamado>();

protected override void OnModelCreating(
    ModelBuilder builder)
{
    base.OnModelCreating(builder);

    ConfigurarFuncionarios(builder);
    ConfigurarGerentesUnidades(builder);
    ConfigurarChamados(builder);
    ConfigurarVisualizacoes(builder);
    ConfigurarHistoricos(builder);
    ConfigurarAuditorias(builder);
    ConfigurarEmailsNotificacoes(builder);
    ConfigurarComentarios(builder);
    ConfigurarAnexos(builder);
    CadastrarDadosIniciais(builder);
}

private static void ConfigurarEmailsNotificacoes(
    ModelBuilder builder)
{
    builder.Entity<EmailNotificacao>()
        .HasIndex(item => new
        {
            item.EnviadoEm,
            item.ProximaTentativaEm
        });

    builder.Entity<EmailNotificacao>()
        .HasIndex(item => item.ChamadoId);

    builder.Entity<EmailNotificacao>()
        .Property(item => item.ConteudoHtml)
        .HasColumnType("nvarchar(max)");
}

private static void ConfigurarAuditorias(ModelBuilder builder)
{
    builder.Entity<AuditoriaAdministrativa>()
        .HasIndex(item => item.CriadoEm);

    builder.Entity<AuditoriaAdministrativa>()
        .HasIndex(item => new
        {
            item.Entidade,
            item.EntidadeId
        });
}

private static void ConfigurarVisualizacoes(
    ModelBuilder builder)
{
    builder.Entity<ChamadoVisualizacao>()
        .HasKey(item => new
        {
            item.UsuarioId,
            item.ChamadoId
        });

    builder.Entity<ChamadoVisualizacao>()
        .HasOne(item => item.Usuario)
        .WithMany()
        .HasForeignKey(item => item.UsuarioId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<ChamadoVisualizacao>()
        .HasOne(item => item.Chamado)
        .WithMany()
        .HasForeignKey(item => item.ChamadoId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<ChamadoVisualizacao>()
        .HasIndex(item => new
        {
            item.ChamadoId,
            item.VisualizadoEm
        });
}

private static void ConfigurarGerentesUnidades(
    ModelBuilder builder)
{
    builder.Entity<GerenteUnidade>()
        .HasKey(item => new
        {
            item.UsuarioId,
            item.UnidadeId
        });

    builder.Entity<GerenteUnidade>()
        .HasOne(item => item.Usuario)
        .WithMany()
        .HasForeignKey(item => item.UsuarioId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<GerenteUnidade>()
        .HasOne(item => item.Unidade)
        .WithMany(unidade => unidade.Gerentes)
        .HasForeignKey(item => item.UnidadeId)
        .OnDelete(DeleteBehavior.Cascade);
}

private static void ConfigurarComentarios(
    ModelBuilder builder)
{
    builder.Entity<ComentarioChamado>()
        .HasOne(comentario => comentario.Chamado)
        .WithMany()
        .HasForeignKey(comentario =>
            comentario.ChamadoId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<ComentarioChamado>()
        .HasOne(comentario => comentario.Autor)
        .WithMany()
        .HasForeignKey(comentario =>
            comentario.AutorId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.Entity<ComentarioChamado>()
        .HasIndex(comentario => new
        {
            comentario.ChamadoId,
            comentario.CriadoEm
        });
}

private static void ConfigurarAnexos(
    ModelBuilder builder)
{
    builder.Entity<AnexoChamado>()
        .HasOne(anexo => anexo.Chamado)
        .WithMany()
        .HasForeignKey(anexo => anexo.ChamadoId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<AnexoChamado>()
        .HasOne(anexo => anexo.Autor)
        .WithMany()
        .HasForeignKey(anexo => anexo.AutorId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.Entity<AnexoChamado>()
        .Property(anexo => anexo.Conteudo)
        .HasColumnType("varbinary(max)");

    builder.Entity<AnexoChamado>()
        .HasIndex(anexo => new
        {
            anexo.ChamadoId,
            anexo.CriadoEm
        });
}
    private static void ConfigurarFuncionarios(
        ModelBuilder builder)
    {
        builder.Entity<FuncionarioPerfil>()
            .HasIndex(funcionario => funcionario.UsuarioId)
            .IsUnique();

        builder.Entity<FuncionarioPerfil>()
            .HasOne(funcionario => funcionario.Usuario)
            .WithOne()
            .HasForeignKey<FuncionarioPerfil>(
                funcionario => funcionario.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<FuncionarioPerfil>()
            .HasOne(funcionario => funcionario.Unidade)
            .WithMany(unidade => unidade.Funcionarios)
            .HasForeignKey(funcionario => funcionario.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FuncionarioPerfil>()
            .HasOne(funcionario => funcionario.Setor)
            .WithMany(setor => setor.Funcionarios)
            .HasForeignKey(funcionario => funcionario.SetorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurarChamados(
        ModelBuilder builder)
    {
        builder.Entity<Chamado>()
            .HasOne(chamado => chamado.Categoria)
            .WithMany(categoria => categoria.Chamados)
            .HasForeignKey(chamado => chamado.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Chamado>()
            .HasOne(chamado => chamado.Unidade)
            .WithMany()
            .HasForeignKey(chamado => chamado.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Chamado>()
            .HasOne(chamado => chamado.Setor)
            .WithMany()
            .HasForeignKey(chamado => chamado.SetorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Chamado>()
            .HasOne(chamado => chamado.Solicitante)
            .WithMany()
            .HasForeignKey(chamado => chamado.SolicitanteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Chamado>()
            .HasOne(chamado =>
                chamado.TecnicoResponsavel)
            .WithMany()
            .HasForeignKey(chamado =>
                chamado.TecnicoResponsavelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Chamado>()
            .Property(chamado => chamado.Prioridade)
            .HasConversion<int>();

        builder.Entity<Chamado>()
            .Property(chamado => chamado.Status)
            .HasConversion<int>();
    }

    private static void ConfigurarHistoricos(
        ModelBuilder builder)
    {
        builder.Entity<HistoricoChamado>()
            .HasOne(historico => historico.Chamado)
            .WithMany()
            .HasForeignKey(historico =>
                historico.ChamadoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<HistoricoChamado>()
            .HasOne(historico => historico.Usuario)
            .WithMany()
            .HasForeignKey(historico =>
                historico.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<HistoricoChamado>()
            .HasIndex(historico => new
            {
                historico.ChamadoId,
                historico.CriadoEm
            });
    }

    private static void CadastrarDadosIniciais(
        ModelBuilder builder)
    {
        builder.Entity<Unidade>().HasData(
            new Unidade
            {
                Id = 1,
                Nome = "Fiat",
                Ativa = true
            },
            new Unidade
            {
                Id = 2,
                Nome = "Peugeot e Citroën",
                Ativa = true
            },
            new Unidade
            {
                Id = 3,
                Nome = "Citroën (agrupada)",
                Ativa = false
            },
            new Unidade
            {
                Id = 4,
                Nome = "Jeep / RAM",
                Ativa = true
            },
            new Unidade
            {
                Id = 5,
                Nome = "RAM (agrupada)",
                Ativa = false
            },
            new Unidade
            {
                Id = 6,
                Nome = "Caoa Chery",
                Ativa = true
            },
            new Unidade
            {
                Id = 7,
                Nome = "Seminovos",
                Ativa = true
            },
            new Unidade
            {
                Id = 8,
                Nome = "Muve Locadora",
                Ativa = true
            },
            new Unidade
            {
                Id = 9,
                Nome = "Consórcio Guarapiranga",
                Ativa = true
            }
        );

        builder.Entity<Setor>().HasData(
            new Setor
            {
                Id = 1,
                Nome = "Tecnologia da Informação",
                Ativo = true
            },
            new Setor
            {
                Id = 2,
                Nome = "Administrativo",
                Ativo = true
            },
            new Setor
            {
                Id = 3,
                Nome = "Financeiro",
                Ativo = true
            },
            new Setor
            {
                Id = 4,
                Nome = "Comercial / Vendas",
                Ativo = true
            },
            new Setor
            {
                Id = 5,
                Nome = "Oficina",
                Ativo = true
            },
            new Setor
            {
                Id = 6,
                Nome = "Peças",
                Ativo = true
            },
            new Setor
            {
                Id = 7,
                Nome = "Pós-vendas",
                Ativo = true
            },
            new Setor
            {
                Id = 8,
                Nome = "Recursos Humanos",
                Ativo = true
            },
            new Setor
            {
                Id = 9,
                Nome = "Contabilidade",
                Ativo = true
            },
            new Setor
            {
                Id = 10,
                Nome = "Marketing",
                Ativo = true
            },
            new Setor
            {
                Id = 11,
                Nome = "Diretoria",
                Ativo = true
            },
            new Setor
            {
                Id = 12,
                Nome = "Recepção",
                Ativo = true
            },
            new Setor
            {
                Id = 13,
                Nome = "Locadora",
                Ativo = true
            },
            new Setor
            {
                Id = 14,
                Nome = "Consórcio",
                Ativo = true
            }
        );

        builder.Entity<CategoriaChamado>().HasData(
            new CategoriaChamado
            {
                Id = 1,
                Nome = "Acessos e senhas",
                Ativa = true
            },
            new CategoriaChamado
            {
                Id = 2,
                Nome = "Computador ou notebook",
                Ativa = true
            },
            new CategoriaChamado
            {
                Id = 3,
                Nome = "Impressora ou scanner",
                Ativa = true
            },
            new CategoriaChamado
            {
                Id = 4,
                Nome = "Sistemas",
                Ativa = true
            },
            new CategoriaChamado
            {
                Id = 5,
                Nome = "Rede e internet",
                Ativa = true
            },
            new CategoriaChamado
            {
                Id = 6,
                Nome = "E-mail e Microsoft 365",
                Ativa = true
            },
            new CategoriaChamado
            {
                Id = 7,
                Nome = "Telefonia",
                Ativa = true
            },
            new CategoriaChamado
            {
                Id = 8,
                Nome = "Outros",
                Ativa = true
            }
        );
    }
}
