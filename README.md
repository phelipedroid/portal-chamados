# Portal de Chamados

Sistema interno de atendimento desenvolvido em ASP.NET Core MVC para organizar
solicitacoes de suporte, acompanhar prazos e centralizar a comunicacao entre
funcionarios, gestores e equipe tecnica.

Este repositorio contem somente codigo e configuracoes de exemplo. Bancos de
dados, contas reais, certificados, chaves, backups e configuracoes da empresa
nao fazem parte do versionamento.

## Funcionalidades

- abertura, classificacao e acompanhamento de chamados;
- perfis de Funcionario, Gerente, Tecnico e Administrador;
- visibilidade de gestores limitada as unidades associadas;
- comentarios, historico, anexos e pre-visualizacao de imagens;
- sino de notificacoes baseado em novas interacoes;
- notificacoes de e-mail em eventos importantes;
- recuperacao de senha com token temporario;
- autocadastro sujeito a aprovacao administrativa;
- dashboard com indicadores, filtros e exportacao;
- SLA em horario comercial, pausas e feriados configuraveis;
- auditoria administrativa com filtros e exportacao em PDF;
- backup, monitoramento e execucao como servico do Windows.

## Tecnologias

- .NET 10 e ASP.NET Core MVC;
- Entity Framework Core;
- SQL Server;
- ASP.NET Core Identity;
- Microsoft Graph para envio opcional de e-mails;
- Bootstrap, JavaScript e Razor Views;
- Kestrel hospedado como servico do Windows.

## Organizacao

```text
Controllers/       Fluxos HTTP e regras das telas
Models/            Entidades persistidas
ViewModels/        Formularios e validacoes
Views/             Interface Razor
Services/          Permissoes, SLA, e-mail, auditoria e monitoramento
Data/              DbContext e migracoes do Entity Framework
wwwroot/           CSS, JavaScript e imagens
deploy/            Scripts genericos de publicacao e diagnostico
```

## Executar localmente

Requisitos:

- SDK do .NET 10;
- SQL Server ou SQL Server Express;
- banco vazio acessivel pela connection string configurada.

Prepare as configuracoes locais, que sao ignoradas pelo Git:

```powershell
Copy-Item appsettings.example.json appsettings.json
Copy-Item appsettings.Production.example.json appsettings.Production.json
```

Revise a connection string e execute:

```powershell
dotnet restore
dotnet ef database update
dotnet run
```

As migracoes criam a estrutura do banco automaticamente. Credenciais de
desenvolvimento devem ser armazenadas com User Secrets ou variaveis de
ambiente, nunca commitadas.

## Seguranca e privacidade

- senhas sao armazenadas pelo ASP.NET Core Identity, nunca em texto aberto;
- formularios mutaveis usam protecao antiforgery;
- login e recuperacao de senha possuem limitacao de tentativas;
- autorizacao e aplicada por perfil e pelo escopo de unidades;
- anexos passam por validacao de extensao, tipo e tamanho;
- configuracoes reais e dados operacionais sao excluidos pelo `.gitignore`.

## Contexto do projeto

Projeto autoral criado para modernizar o atendimento interno de suporte. O
codigo deste repositorio serve como demonstracao tecnica e referencia de
portfolio; nao inclui o ambiente nem os dados utilizados em producao.
