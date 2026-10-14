using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using PortalSanMarco.Data;
using PortalSanMarco.Models;
using PortalSanMarco.Services;
using System.Threading.RateLimiting;

var webApplicationOptions =
    new WebApplicationOptions
    {
        Args = args,
        ContentRootPath =
            WindowsServiceHelpers.IsWindowsService() ||
            args.Contains("--monitor-once",
                StringComparer.OrdinalIgnoreCase)
                ? AppContext.BaseDirectory
                : default
    };

var builder =
    WebApplication.CreateBuilder(
        webApplicationOptions);

builder.WebHost.ConfigureKestrel(
    options => options.AddServerHeader = false);

builder.Host.UseWindowsService(
    options =>
        options.ServiceName =
            "Portal San Marco");

var diretorioChaves =
    Path.Combine(
        builder.Environment.ContentRootPath,
        "DataProtectionKeys");

Directory.CreateDirectory(diretorioChaves);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(diretorioChaves))
    .SetApplicationName("PortalSanMarco");

// Conexão com o SQL Server
var connectionString =
    builder.Configuration
        .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
        options.UseSqlServer(connectionString));

builder.Services
    .AddDatabaseDeveloperPageExceptionFilter();

// Login e perfis do Portal San Marco
builder.Services
    .AddDefaultIdentity<IdentityUser>(
        options =>
        {
            options.SignIn
                .RequireConfirmedAccount = false;

            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredUniqueChars = 4;

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(15);
        })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<
    IUserClaimsPrincipalFactory<IdentityUser>,
    PortalUserClaimsPrincipalFactory>();

builder.Services.Configure<DataProtectionTokenProviderOptions>(
    options =>
        options.TokenLifespan = TimeSpan.FromMinutes(30));

builder.Services.Configure<SecurityStampValidatorOptions>(
    options =>
        options.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.AddMemoryCache();

builder.Services.AddRateLimiter(
    options =>
    {
        options.RejectionStatusCode =
            StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.ContentType =
                "text/plain; charset=utf-8";

            await context.HttpContext.Response.WriteAsync(
                "Muitas tentativas em pouco tempo. " +
                "Aguarde um minuto e tente novamente.",
                token);
        };

        options.AddPolicy(
            "Autenticacao",
            context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ??
                    "endereco-desconhecido",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

        options.AddPolicy(
            "RecuperacaoSenha",
            context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ??
                    "endereco-desconhecido",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

        options.AddPolicy(
            "Autocadastro",
            context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ??
                    "endereco-desconhecido",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
    });

builder.Services
    .AddOptions<SelfRegistrationOptions>()
    .BindConfiguration("SelfRegistration")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<GraphMailOptions>()
    .BindConfiguration("GraphMail")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<
    IPortalEmailSender,
    GraphEmailSender>();

builder.Services.AddHttpClient();

builder.Services
    .AddOptions<SlaOptions>()
    .BindConfiguration("Sla")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<PortalMonitorOptions>()
    .BindConfiguration("Monitoring")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ISlaService, SlaService>();
builder.Services.AddScoped<
    IChamadoEmailNotificationService,
    ChamadoEmailNotificationService>();
builder.Services.AddScoped<
    IUsuarioEmailNotificationService,
    UsuarioEmailNotificationService>();
builder.Services.AddHostedService<EmailNotificationWorker>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IAuditoriaPdfService, AuditoriaPdfService>();
builder.Services.AddScoped<PortalMonitorRunner>();

builder.Services.AddScoped<
    IChamadoAccessService,
    ChamadoAccessService>();

// Define nosso login como o login oficial
builder.Services.ConfigureApplicationCookie(
    options =>
    {
        options.LoginPath =
            "/Conta/Entrar";

        options.AccessDeniedPath =
            "/Conta/AcessoNegado";

        options.ExpireTimeSpan =
            TimeSpan.FromHours(8);

        options.SlidingExpiration = true;
        options.Cookie.Name = "PortalSanMarco.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddControllersWithViews(
    options =>
        options.Filters.Add(
            new AutoValidateAntiforgeryTokenAttribute()));

builder.Services.AddAuthorization(
    options =>
    {
        options.AddPolicy(
            "PodeVisualizarTodosChamados",
            policy =>
                policy.RequireRole(
                    "Tecnico",
                    "Gerente",
                    "Administrador"));

        options.AddPolicy(
            "PodeAtenderChamados",
            policy =>
                policy.RequireRole(
                    "Tecnico",
                    "Administrador"));
    });

var app = builder.Build();

if (args.Contains(
        "--monitor-once",
        StringComparer.OrdinalIgnoreCase))
{
    using var monitorScope = app.Services.CreateScope();
    var monitor = monitorScope.ServiceProvider
        .GetRequiredService<PortalMonitorRunner>();
    Environment.ExitCode = await monitor.ExecutarAsync();
    return;
}

// Criação automática dos perfis do portal
using (var scope =
       app.Services.CreateScope())
{
    var banco =
        scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

    // O serviço depende do SQL Server, mas durante a inicialização do
    // Windows a instância ainda pode levar alguns segundos para aceitar
    // conexões. O retry fica restrito à migração inicial para não interferir
    // nas transações manuais usadas pelo cadastro e edição de usuários.
    const int maximoTentativasBanco = 5;

    for (var tentativa = 1;
         tentativa <= maximoTentativasBanco;
         tentativa++)
    {
        try
        {
            await banco.Database.MigrateAsync();
            break;
        }
        catch (SqlException)
            when (tentativa < maximoTentativasBanco)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    var roleManager =
        scope.ServiceProvider
            .GetRequiredService<
                RoleManager<IdentityRole>>();

    var userManager =
        scope.ServiceProvider
            .GetRequiredService<
                UserManager<IdentityUser>>();

    string[] perfis =
    {
        "Funcionario",
        "Tecnico",
        "Gerente",
        "Administrador"
    };

    foreach (var perfil in perfis)
    {
        if (!await roleManager
                .RoleExistsAsync(perfil))
        {
            await roleManager.CreateAsync(
                new IdentityRole(perfil));
        }
    }

    // Cadastros de gerente criados antes do suporte a múltiplas unidades
    // passam a ter explicitamente a unidade principal como área gerenciada.
    var perfilGerente =
        await roleManager.FindByNameAsync("Gerente");

    if (perfilGerente is not null)
    {
        var gerentesSemUnidade =
            await (
                from vinculo in banco.UserRoles
                join funcionario in banco.FuncionariosPerfis
                    on vinculo.UserId equals funcionario.UsuarioId
                where vinculo.RoleId == perfilGerente.Id &&
                      !banco.GerentesUnidades.Any(item =>
                          item.UsuarioId == vinculo.UserId)
                select new GerenteUnidade
                {
                    UsuarioId = vinculo.UserId,
                    UnidadeId = funcionario.UnidadeId
                })
                .ToListAsync();

        if (gerentesSemUnidade.Count > 0)
        {
            banco.GerentesUnidades.AddRange(
                gerentesSemUnidade);

            await banco.SaveChangesAsync();
        }
    }

    // Conta inicial exclusiva para desenvolvimento local. As credenciais
    // ficam em User Secrets, nunca no código ou em appsettings.json.
    if (app.Environment.IsDevelopment())
    {
        var emailAdministradorLocal =
            builder.Configuration[
                "BootstrapAdmin:Email"];

        var senhaAdministradorLocal =
            builder.Configuration[
                "BootstrapAdmin:Password"];

        if (!string.IsNullOrWhiteSpace(
                emailAdministradorLocal) &&
            !string.IsNullOrWhiteSpace(
                senhaAdministradorLocal))
        {
            var administradorLocal =
                await userManager.FindByEmailAsync(
                    emailAdministradorLocal);

            if (administradorLocal is null)
            {
                administradorLocal = new IdentityUser
                {
                    UserName = emailAdministradorLocal,
                    Email = emailAdministradorLocal,
                    EmailConfirmed = true,
                    LockoutEnabled = true
                };

                var resultadoCriacao =
                    await userManager.CreateAsync(
                        administradorLocal,
                        senhaAdministradorLocal);

                if (!resultadoCriacao.Succeeded)
                {
                    var erros = string.Join(
                        "; ",
                        resultadoCriacao.Errors.Select(
                            erro => erro.Description));

                    throw new InvalidOperationException(
                        "Não foi possível criar o administrador " +
                        $"local: {erros}");
                }
            }

            if (!await userManager.IsInRoleAsync(
                    administradorLocal,
                    "Administrador"))
            {
                await userManager.AddToRoleAsync(
                    administradorLocal,
                    "Administrador");
            }
        }
    }

    // Conta administrativa oficial opcional, definida por configuracao.
    var emailAdministrador = builder.Configuration[
        "BootstrapAdmin:OfficialEmail"];

    if (!string.IsNullOrWhiteSpace(emailAdministrador))
    {
        var administrador =
            await userManager.FindByEmailAsync(
                emailAdministrador);

        if (administrador is not null &&
            !await userManager.IsInRoleAsync(
                administrador,
                "Administrador"))
        {
            await userManager.AddToRoleAsync(
                administrador,
                "Administrador");
        }
    }
}

// Configuração das requisições
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler(
        "/Home/Error");

    app.UseHsts();
}

if (builder.Configuration.GetValue<bool>(
        "Security:UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseRateLimiter();

// Carrega a identidade do usuário
app.UseAuthentication();

// Cabeçalhos de segurança para reduzir exposição a incorporação em outros
// sites, interpretação incorreta de anexos e carregamento de conteúdo externo.
app.Use(
    async (context, next) =>
    {
        context.Response.OnStarting(
            () =>
            {
                var headers = context.Response.Headers;

                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] =
                    "camera=(), microphone=(), geolocation=()";
                headers.ContentSecurityPolicy =
                    "default-src 'self'; " +
                    "base-uri 'self'; " +
                    "object-src 'none'; " +
                    "frame-ancestors 'none'; " +
                    "form-action 'self'; " +
                    "img-src 'self' data:; " +
                    "font-src 'self'; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "script-src 'self'";

                if (context.User.Identity?.IsAuthenticated == true)
                {
                    headers.CacheControl = "no-store, no-cache";
                    headers.Pragma = "no-cache";
                }

                return Task.CompletedTask;
            });

        await next();
    });

// O portal possui telas próprias de autenticação e administração. Mantém
// somente a página de segundo fator do Identity disponível para eventual uso.
app.Use(
    async (context, next) =>
    {
        var caminho =
            context.Request.Path;

        var ehRotaIdentity =
            caminho.StartsWithSegments(
                "/Identity/Account");

        var ehSegundoFator =
            caminho.StartsWithSegments(
                "/Identity/Account/LoginWith2fa");

        if (ehRotaIdentity && !ehSegundoFator)
        {
            context.Response.Redirect(
                context.User.Identity?.IsAuthenticated == true
                    ? "/Conta/AcessoNegado"
                    : "/Conta/Entrar");

            return;
        }

        await next();
    });

// Obriga a troca da senha provisória
app.Use(
    async (context, next) =>
    {
        if (context.User.Identity
                ?.IsAuthenticated == true)
        {
            var caminho =
                context.Request.Path;

            var caminhoLiberado =
                caminho.StartsWithSegments(
                    "/Conta/Entrar") ||

                caminho.StartsWithSegments(
                    "/Conta/Sair") ||

                caminho.StartsWithSegments(
                    "/Conta/TrocarSenha") ||

                caminho.StartsWithSegments(
                    "/Conta/AcessoNegado") ||

                caminho.StartsWithSegments(
                    "/Identity/Account/LoginWith2fa") ||

                caminho.StartsWithSegments(
                    "/Identity/Account/Logout") ||

                caminho.StartsWithSegments("/css") ||

                caminho.StartsWithSegments("/js") ||

                caminho.StartsWithSegments("/lib") ||

                caminho.StartsWithSegments(
                    "/favicon.ico") ||

                System.IO.Path.HasExtension(
                    caminho.Value);

            if (!caminhoLiberado)
            {
                var userManager =
                    context.RequestServices
                        .GetRequiredService<
                            UserManager<IdentityUser>>();

                var banco =
                    context.RequestServices
                        .GetRequiredService<
                            ApplicationDbContext>();

                var usuario =
                    await userManager.GetUserAsync(
                        context.User);

                if (usuario is not null)
                {
                    var deveTrocar =
                        await banco
                            .FuncionariosPerfis
                            .AsNoTracking()
                            .AnyAsync(
                                perfil =>
                                    perfil.UsuarioId ==
                                        usuario.Id &&

                                    perfil
                                        .DeveTrocarSenha);

                    if (deveTrocar)
                    {
                        context.Response.Redirect(
                            "/Conta/TrocarSenha");

                        return;
                    }
                }
            }
        }

        await next();
    });

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "inicio",
        pattern: "",
        defaults: new
        {
            controller = "Conta",
            action = "Entrar"
        })
    .WithStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern:
            "{controller=Conta}/" +
            "{action=Index}/" +
            "{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.MapGet(
        "/health",
        async (ApplicationDbContext banco) =>
        {
            try
            {
                return await banco.Database
                    .CanConnectAsync()
                    ? Results.Ok(
                        new
                        {
                            status = "ok"
                        })
                    : Results.Problem(
                        "Banco de dados indisponível.",
                        statusCode: 503);
            }
            catch
            {
                return Results.Problem(
                    "Banco de dados indisponível.",
                    statusCode: 503);
            }
        })
    .AllowAnonymous();

app.Run();
