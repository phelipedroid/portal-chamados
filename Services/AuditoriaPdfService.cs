using System.Globalization;
using System.Text;
using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public sealed class AuditoriaPdfService : IAuditoriaPdfService
{
    private const double LarguraPagina = 842;
    private const double AlturaPagina = 595;
    private const double Margem = 36;
    private const double TopoTabela = 456;
    private const double RodapeTabela = 48;

    private static readonly Coluna[] Colunas =
    [
        new("Data e hora", 78),
        new("Administrador", 132),
        new("Ação", 78),
        new("Área", 72),
        new("Detalhes", 326),
        new("IP", 84)
    ];

    public byte[] Gerar(
        IReadOnlyCollection<AuditoriaAdministrativa> registros,
        DateTime dataInicial,
        DateTime dataFinal,
        string? busca,
        string? entidade,
        string geradoPor)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var linhas = registros
            .Select(CriarLinha)
            .ToList();
        var paginas = Paginar(linhas);

        if (paginas.Count == 0)
        {
            paginas.Add([]);
        }

        var objetos = new List<byte[]>
        {
            Bytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Array.Empty<byte>(),
            Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>")
        };

        var referenciasPaginas = new List<int>();

        for (var indice = 0; indice < paginas.Count; indice++)
        {
            var numeroPagina = objetos.Count + 1;
            var numeroConteudo = numeroPagina + 1;
            referenciasPaginas.Add(numeroPagina);

            var conteudo = MontarPagina(
                paginas[indice],
                indice + 1,
                paginas.Count,
                registros.Count,
                dataInicial,
                dataFinal,
                busca,
                entidade,
                geradoPor);

            objetos.Add(Bytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {LarguraPagina} {AlturaPagina}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {numeroConteudo} 0 R >>"));
            objetos.Add(CriarStream(conteudo));
        }

        objetos[1] = Bytes(
            $"<< /Type /Pages /Count {referenciasPaginas.Count} /Kids [{string.Join(" ", referenciasPaginas.Select(numero => $"{numero} 0 R"))}] >>");

        return MontarDocumento(objetos);
    }

    private static List<List<Linha>> Paginar(IReadOnlyCollection<Linha> linhas)
    {
        var paginas = new List<List<Linha>>();
        var paginaAtual = new List<Linha>();
        var espacoDisponivel = TopoTabela - RodapeTabela - 26;
        var usado = 0d;

        foreach (var linha in linhas)
        {
            if (paginaAtual.Count > 0 && usado + linha.Altura > espacoDisponivel)
            {
                paginas.Add(paginaAtual);
                paginaAtual = [];
                usado = 0;
            }

            paginaAtual.Add(linha);
            usado += linha.Altura;
        }

        if (paginaAtual.Count > 0)
        {
            paginas.Add(paginaAtual);
        }

        return paginas;
    }

    private static Linha CriarLinha(AuditoriaAdministrativa item)
    {
        var valores = new[]
        {
            Quebrar(item.CriadoEm.ToString("dd/MM/yyyy HH:mm:ss"), Colunas[0].Largura, 7.2, 2),
            Quebrar(item.UsuarioEmail, Colunas[1].Largura, 7.2, 3),
            Quebrar(item.Acao, Colunas[2].Largura, 7.2, 2),
            Quebrar(item.Entidade, Colunas[3].Largura, 7.2, 2),
            Quebrar(item.Descricao, Colunas[4].Largura, 7.2, 4),
            Quebrar(item.EnderecoIp ?? "-", Colunas[5].Largura, 7.2, 2)
        };

        var maiorQuantidade = valores.Max(valor => valor.Count);
        return new Linha(valores, Math.Max(24, 8 + maiorQuantidade * 9));
    }

    private static List<string> Quebrar(
        string? valor,
        double largura,
        double tamanhoFonte,
        int maximoLinhas)
    {
        var texto = string.IsNullOrWhiteSpace(valor) ? "-" : valor.Trim();
        var maximoCaracteres = Math.Max(
            5,
            (int)Math.Floor((largura - 8) / (tamanhoFonte * 0.52)));
        var palavras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var linhas = new List<string>();
        var atual = new StringBuilder();

        foreach (var palavraOriginal in palavras)
        {
            var palavra = palavraOriginal;

            while (palavra.Length > maximoCaracteres)
            {
                if (atual.Length > 0)
                {
                    linhas.Add(atual.ToString());
                    atual.Clear();
                }

                linhas.Add(palavra[..maximoCaracteres]);
                palavra = palavra[maximoCaracteres..];
            }

            if (atual.Length == 0)
            {
                atual.Append(palavra);
            }
            else if (atual.Length + 1 + palavra.Length <= maximoCaracteres)
            {
                atual.Append(' ').Append(palavra);
            }
            else
            {
                linhas.Add(atual.ToString());
                atual.Clear();
                atual.Append(palavra);
            }
        }

        if (atual.Length > 0)
        {
            linhas.Add(atual.ToString());
        }

        if (linhas.Count <= maximoLinhas)
        {
            return linhas.Count == 0 ? ["-"] : linhas;
        }

        var resultado = linhas.Take(maximoLinhas).ToList();
        var ultima = resultado[^1];
        resultado[^1] = ultima.Length >= maximoCaracteres
            ? ultima[..Math.Max(1, maximoCaracteres - 3)] + "..."
            : ultima + "...";
        return resultado;
    }

    private static byte[] MontarPagina(
        IReadOnlyCollection<Linha> linhas,
        int pagina,
        int totalPaginas,
        int totalRegistros,
        DateTime dataInicial,
        DateTime dataFinal,
        string? busca,
        string? entidade,
        string geradoPor)
    {
        var pdf = new StringBuilder();

        Retangulo(pdf, 0, AlturaPagina - 84, LarguraPagina, 84, "0.035 0.455 0.263");
        Texto(pdf, "Relatório de Auditoria Administrativa", Margem, AlturaPagina - 39, 19, true, "1 1 1");
        Texto(pdf, "Portal de Chamados San Marco", Margem, AlturaPagina - 60, 9, false, "0.86 0.96 0.90");

        Texto(pdf, $"Período: {dataInicial:dd/MM/yyyy} a {dataFinal:dd/MM/yyyy}", Margem, 486, 9, true, "0.12 0.18 0.15");
        Texto(pdf, $"Área: {(string.IsNullOrWhiteSpace(entidade) ? "Todas" : entidade)}", 255, 486, 9, false, "0.25 0.31 0.28");
        Texto(pdf, $"Pesquisa: {(string.IsNullOrWhiteSpace(busca) ? "Sem filtro" : Limitar(busca, 48))}", 438, 486, 9, false, "0.25 0.31 0.28");
        Texto(pdf, $"Gerado por: {Limitar(geradoPor, 42)} em {DateTime.Now:dd/MM/yyyy HH:mm}", Margem, 469, 8, false, "0.35 0.40 0.37");
        Texto(pdf, $"Total no período: {totalRegistros} registro(s)", 550, 469, 8, true, "0.12 0.18 0.15");

        var x = Margem;
        Retangulo(pdf, Margem, TopoTabela - 24, Colunas.Sum(coluna => coluna.Largura), 24, "0.90 0.94 0.92");

        foreach (var coluna in Colunas)
        {
            Texto(pdf, coluna.Titulo, x + 4, TopoTabela - 16, 7.5, true, "0.12 0.18 0.15");
            x += coluna.Largura;
        }

        var y = TopoTabela - 24;
        var alternada = false;

        foreach (var linha in linhas)
        {
            y -= linha.Altura;

            if (alternada)
            {
                Retangulo(pdf, Margem, y, Colunas.Sum(coluna => coluna.Largura), linha.Altura, "0.975 0.985 0.98");
            }

            LinhaHorizontal(pdf, Margem, y, LarguraPagina - Margem, "0.82 0.86 0.84");
            x = Margem;

            for (var indice = 0; indice < Colunas.Length; indice++)
            {
                var textoY = y + linha.Altura - 11;

                foreach (var trecho in linha.Valores[indice])
                {
                    Texto(pdf, trecho, x + 4, textoY, 7.2, false, "0.16 0.20 0.18");
                    textoY -= 9;
                }

                x += Colunas[indice].Largura;
            }

            alternada = !alternada;
        }

        if (linhas.Count == 0)
        {
            Texto(pdf, "Nenhum registro encontrado para os filtros selecionados.", Margem + 12, TopoTabela - 55, 10, false, "0.35 0.40 0.37");
        }

        Texto(pdf, "Documento gerado pelo Portal San Marco", Margem, 25, 7.5, false, "0.45 0.49 0.47");
        Texto(pdf, $"Página {pagina} de {totalPaginas}", LarguraPagina - 112, 25, 7.5, false, "0.45 0.49 0.47");

        return Bytes(pdf.ToString());
    }

    private static byte[] MontarDocumento(IReadOnlyList<byte[]> objetos)
    {
        using var arquivo = new MemoryStream();
        Escrever(arquivo, Bytes("%PDF-1.4\n%PortalSanMarco\n"));
        var deslocamentos = new List<long> { 0 };

        for (var indice = 0; indice < objetos.Count; indice++)
        {
            deslocamentos.Add(arquivo.Position);
            Escrever(arquivo, Bytes($"{indice + 1} 0 obj\n"));
            Escrever(arquivo, objetos[indice]);
            Escrever(arquivo, Bytes("\nendobj\n"));
        }

        var inicioXref = arquivo.Position;
        Escrever(arquivo, Bytes($"xref\n0 {objetos.Count + 1}\n"));
        Escrever(arquivo, Bytes("0000000000 65535 f \n"));

        foreach (var deslocamento in deslocamentos.Skip(1))
        {
            Escrever(arquivo, Bytes($"{deslocamento:0000000000} 00000 n \n"));
        }

        Escrever(arquivo, Bytes(
            $"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{inicioXref}\n%%EOF"));
        return arquivo.ToArray();
    }

    private static byte[] CriarStream(byte[] conteudo)
    {
        using var stream = new MemoryStream();
        Escrever(stream, Bytes($"<< /Length {conteudo.Length} >>\nstream\n"));
        Escrever(stream, conteudo);
        Escrever(stream, Bytes("\nendstream"));
        return stream.ToArray();
    }

    private static void Texto(
        StringBuilder pdf,
        string texto,
        double x,
        double y,
        double tamanho,
        bool negrito,
        string cor)
    {
        var bytes = Encoding.GetEncoding(1252).GetBytes(texto);
        var hexadecimal = Convert.ToHexString(bytes);
        pdf.Append("BT /")
            .Append(negrito ? "F2" : "F1")
            .Append(' ')
            .Append(Numero(tamanho))
            .Append(" Tf ")
            .Append(cor)
            .Append(" rg ")
            .Append(Numero(x))
            .Append(' ')
            .Append(Numero(y))
            .Append(" Td <")
            .Append(hexadecimal)
            .Append("> Tj ET\n");
    }

    private static void Retangulo(
        StringBuilder pdf,
        double x,
        double y,
        double largura,
        double altura,
        string cor)
    {
        pdf.Append(cor).Append(" rg ")
            .Append(Numero(x)).Append(' ')
            .Append(Numero(y)).Append(' ')
            .Append(Numero(largura)).Append(' ')
            .Append(Numero(altura)).Append(" re f\n");
    }

    private static void LinhaHorizontal(
        StringBuilder pdf,
        double x1,
        double y,
        double x2,
        string cor)
    {
        pdf.Append(cor).Append(" RG 0.5 w ")
            .Append(Numero(x1)).Append(' ')
            .Append(Numero(y)).Append(" m ")
            .Append(Numero(x2)).Append(' ')
            .Append(Numero(y)).Append(" l S\n");
    }

    private static string Numero(double valor) =>
        valor.ToString("0.##", CultureInfo.InvariantCulture);

    private static byte[] Bytes(string valor) =>
        Encoding.Latin1.GetBytes(valor);

    private static void Escrever(Stream destino, byte[] dados) =>
        destino.Write(dados, 0, dados.Length);

    private static string Limitar(string valor, int tamanho) =>
        valor.Length <= tamanho ? valor : valor[..(tamanho - 3)] + "...";

    private sealed record Coluna(string Titulo, double Largura);
    private sealed record Linha(List<string>[] Valores, double Altura);
}
