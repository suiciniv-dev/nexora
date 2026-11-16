using Avalonia.Media;
using Nexora.Fundos;
using Nexora.Temas;

namespace Nexora.Testes;

public sealed class TemasTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-temas-").FullName;

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    [Fact]
    public void TemasProntosTemTodasAsCoresValidas()
    {
        Assert.Equal(4 + Papeis.Todos.Count, Catalogo.Embutidos.Count);
        Assert.Equal("padrao", Catalogo.Embutidos[0].Id);
        Assert.Equal("Padrão", Catalogo.Embutidos[0].Nome);
        Assert.Equal(Papeis.Nenhum, Catalogo.Embutidos[0].FundoAnimado);
        Assert.Equal(Catalogo.Embutidos.Count, Catalogo.Embutidos.Select(t => t.Id).Distinct().Count());
        foreach (var tema in Catalogo.Embutidos)
        {
            Assert.True(tema.Embutido);
            foreach (var chave in Catalogo.Chaves)
                Assert.True(Color.TryParse(tema.Cores[chave], out _), $"{tema.Id}.{chave}");
            Assert.True(tema.FundoAnimado == Papeis.Nenhum || Papeis.Encontrar(tema.FundoAnimado) is not null, tema.Id);
        }
    }

    [Fact]
    public void TodosOsFundosAnimadosSaoDoNexoraEViramTema()
    {
        Assert.Equal(["Grafo", "Grafo claro", "Constelação", "Carta celeste", "Via Láctea", "Nebulosa", "Pulso", "Aurora", "Mosaico", "Mosaico estático"], Papeis.Todos.Select(p => p.Nome));
        Assert.All(Papeis.Todos, p => Assert.StartsWith("nexora:", p.Id));
        Assert.Contains(Papeis.Todos, p => !p.Escuro);
        foreach (var papel in Papeis.Todos)
        {
            var tema = Assert.Single(Catalogo.Embutidos, t => t.FundoAnimado == papel.Id);
            Assert.Equal(papel.Nome, tema.Nome);
            Assert.Equal(papel.Id["nexora:".Length..], tema.Id);
            Assert.Equal(papel.Escuro, tema.Escuro);
            using var motor = Motores.Criar(papel.Id);
            Assert.InRange(motor.Fps, 1, 30);
        }
    }

    [Fact]
    public void TemaDeVidroMantemATransparenciaDasCores()
    {
        var ceu = Catalogo.Encontrar("constelacao");
        Assert.True(ceu.Cor("Lateral").A < 255);
        Assert.Equal(255, ceu.Cor("Fundo").A);
    }

    [Fact]
    public void FundosAntigosCaemNumDosNovos()
    {
        Assert.Equal("nexora:aurora", Papeis.Migrar("claudinho:aurora"));
        Assert.Equal("nexora:grafo-claro", Papeis.Migrar("claudinho:clouds"));
        Assert.Equal("nexora:pulso", Papeis.Migrar("magia:game-of-life"));
        Assert.Equal("nexora:via-lactea", Papeis.Migrar("magia:spiral-galaxy"));
        Assert.Equal("nexora:grafo", Papeis.Migrar("claudinho:um-que-nunca-existiu"));
        Assert.Equal("nexora:nebulosa", Papeis.Migrar("nexora:nebulosa"));
        Assert.Equal(Papeis.Nenhum, Papeis.Migrar(Papeis.Nenhum));
        Assert.Equal(Papeis.Nenhum, Papeis.Migrar(null));
        Assert.Equal("outro:fundo", Papeis.Migrar("outro:fundo"));
        foreach (var antigo in new[] { "claudinho:claude", "claudinho:koi", "claudinho:sakura", "magia:starfield-warp", "magia:neon-city-rain" })
            Assert.NotNull(Papeis.Encontrar(Papeis.Migrar(antigo)));
    }

    [Fact]
    public void TemaDoUsuarioComFundoAntigoAbreComUmFundoNovo()
    {
        var arquivo = Path.Combine(_pasta, "antigo.json");
        File.WriteAllText(arquivo, """{"id":"antigo","nome":"Antigo","escuro":true,"fundoAnimado":"magia:northern-lights","cores":{"acento":"#123456"}}""");
        var tema = Catalogo.Ler(arquivo)!;
        Assert.Equal("nexora:aurora", tema.FundoAnimado);
        Assert.False(Catalogo.EmVidro(tema, "animado"));

        File.WriteAllText(arquivo, """{"id":"antigo","nome":"Antigo","fundoAnimado":null}""");
        Assert.Equal(Papeis.Nenhum, Catalogo.Ler(arquivo)!.FundoAnimado);
    }

    [Fact]
    public void EncontrarAceitaOsNomesAntigosDaConfig()
    {
        Assert.Equal("dracula", Catalogo.Encontrar("escuro").Id);
        Assert.Equal("alucard", Catalogo.Encontrar("claro").Id);
        Assert.Equal("padrao", Catalogo.Encontrar("suiciniv").Id);
        Assert.Equal("padrao-claro", Catalogo.Encontrar("suiciniv-papel").Id);
        Assert.Equal("padrao", Catalogo.Encontrar("oceano").Id);
        Assert.Equal("pulso", Catalogo.Encontrar("arcade").Id);
        Assert.Equal("aurora", Catalogo.Encontrar("koi").Id);
        Assert.Equal("aurora", Catalogo.Encontrar("aurora").Id);
        Assert.Equal("grafo-claro", Catalogo.Encontrar("clouds").Id);
        Assert.Equal("constelacao", Catalogo.Encontrar("starfield-warp").Id);
        Assert.Equal("via-lactea", Catalogo.Encontrar("galaxia").Id);
    }

    [Fact]
    public void SeguirOSistemaTrocaEntreOPadraoEscuroEOClaro()
    {
        try
        {
            Catalogo.SistemaEscuroDeTeste = () => true;
            Assert.Equal("padrao", Catalogo.Encontrar("sistema").Id);
            Assert.Equal("padrao", Catalogo.Encontrar(null).Id);
            Assert.Equal("padrao", Catalogo.Encontrar("").Id);
            Catalogo.SistemaEscuroDeTeste = () => false;
            Assert.Equal("padrao-claro", Catalogo.Encontrar("sistema").Id);
            Assert.Equal("padrao-claro", Catalogo.Encontrar(null).Id);
            Assert.Equal("padrao", Catalogo.Encontrar("suiciniv").Id);
            Assert.Equal("padrao", Catalogo.Encontrar("padrao").Id);
            Assert.True(Catalogo.SegueOSistema(null));
            Assert.True(Catalogo.SegueOSistema("sistema"));
            Assert.False(Catalogo.SegueOSistema("padrao"));
        }
        finally
        {
            Catalogo.SistemaEscuroDeTeste = null;
        }
    }

    static Color Sobre(Color cor, Color fundo)
    {
        var a = cor.A / 255.0;
        return Color.FromRgb((byte)Math.Round(cor.R * a + fundo.R * (1 - a)), (byte)Math.Round(cor.G * a + fundo.G * (1 - a)), (byte)Math.Round(cor.B * a + fundo.B * (1 - a)));
    }

    [Fact]
    public void TextosFracosECoresDeEstadoSaoLegiveisNosTemasClaros()
    {
        var falhas = new List<string>();
        foreach (var tema in Catalogo.Embutidos.Where(t => !t.Escuro))
        {
            var fundo = Sobre(tema.Cor("Fundo"), Colors.White);
            foreach (var superficie in new[] { "Fundo", "Superficie", "SuperficieAlta" })
            {
                var atras = Sobre(tema.Cor(superficie), fundo);
                foreach (var chave in new[] { "TextoFraco", "Laranja", "Verde", "Vermelho", "Ciano", "Rosa" })
                {
                    var contraste = Catalogo.Contraste(tema.Cor(chave), atras);
                    if (contraste < 4.5) falhas.Add($"{tema.Id}: {chave} sobre {superficie} = {contraste:0.00}");
                }
            }
        }
        Assert.True(falhas.Count == 0, string.Join(Environment.NewLine, falhas));
    }

    [Fact]
    public void TextoSobreVermelhoESobreAMarcaTemContrasteEmTodosOsTemas()
    {
        var falhas = new List<string>();
        foreach (var tema in Catalogo.Embutidos)
        {
            foreach (var chave in new[] { "Vermelho", "Laranja" })
            {
                var fundo = tema.Cor(chave);
                var contraste = Catalogo.Contraste(Catalogo.MelhorTexto(fundo), fundo);
                if (contraste < 4.5) falhas.Add($"{tema.Id}: texto sobre {chave} = {contraste:0.00}");
            }
            var marca = tema.Cor("Marca");
            var fim = tema.Cor("MarcaFim");
            var meio = Color.FromRgb((byte)((marca.R + fim.R) / 2), (byte)((marca.G + fim.G) / 2), (byte)((marca.B + fim.B) / 2));
            var sobreMarca = Catalogo.Contraste(Catalogo.MelhorTexto(meio), meio);
            if (sobreMarca < 4.5) falhas.Add($"{tema.Id}: texto sobre a marca = {sobreMarca:0.00}");
        }
        Assert.True(falhas.Count == 0, string.Join(Environment.NewLine, falhas));
    }

    [Fact]
    public void ZoomFicaEntreNoventaECinquentaPorCento()
    {
        Assert.Equal(1, Aparencia.Limitar(double.NaN));
        Assert.Equal(1, Aparencia.Limitar(0));
        Assert.Equal(0.9, Aparencia.Limitar(0.5));
        Assert.Equal(1.5, Aparencia.Limitar(3));
        Assert.Equal(1.2, Aparencia.Limitar(1.2));
    }

    [Fact]
    public void GravarELerUmTemaMantemTudo()
    {
        var tema = Catalogo.Encontrar("padrao").Copiar("meu-tema", "Meu tema");
        tema.Cores["Acento"] = "#123456";
        tema.Cores["Lateral"] = "#E0123456";
        tema.FundoAnimado = "nexora:nebulosa";
        tema.Escurecer = 0.4;
        tema.Imagem = @"D:\fundo.png";
        var arquivo = Catalogo.Gravar(tema, Path.Combine(_pasta, "meu-tema.json"));

        var lido = Catalogo.Ler(arquivo)!;
        Assert.False(lido.Embutido);
        Assert.Equal("Meu tema", lido.Nome);
        Assert.Equal(Color.Parse("#123456"), lido.Cor("Acento"));
        Assert.Equal(Color.Parse("#E0123456"), lido.Cor("Lateral"));
        Assert.Equal("nexora:nebulosa", lido.FundoAnimado);
        Assert.Equal(0.4, lido.Escurecer);
        Assert.Equal(@"D:\fundo.png", lido.Imagem);
        Assert.Equal("DarkPlus", lido.Editor);
        Assert.True(Catalogo.EmVidro(lido, "animado") == false);
        Assert.True(Catalogo.EmVidro(Catalogo.Encontrar("padrao"), "animado"));
        Assert.True(Catalogo.EmVidro(Catalogo.Encontrar("dracula"), "vidro"));
        Assert.False(Catalogo.EmVidro(Catalogo.Encontrar("padrao"), "solido"));
        Assert.Contains("\"cores\"", File.ReadAllText(arquivo));
    }

    [Fact]
    public void CorQueFaltaVoltaParaABaseEArquivoRuimNaoQuebra()
    {
        var tema = new Tema { Id = "x", Nome = "X", Escuro = false, Cores = new() { ["Acento"] = "não é cor" } };
        Assert.Equal(Color.Parse(Catalogo.Alucard.Cores["Acento"]), tema.Cor("Acento"));
        Assert.Equal(Color.Parse(Catalogo.Alucard.Cores["Fundo"]), tema.Cor("Fundo"));

        var ruim = Path.Combine(_pasta, "ruim.json");
        File.WriteAllText(ruim, "{ quebrado");
        Assert.Null(Catalogo.Ler(ruim));
        File.WriteAllText(ruim, """{"nome":""}""");
        Assert.Null(Catalogo.Ler(ruim));
    }
}
