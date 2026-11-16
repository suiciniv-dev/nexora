using System.Text.Json;
using Nexora.Core.Avatares;

namespace Nexora.Testes.Avatares;

public sealed class ParidadeComJsTestes
{
    const double Tolerancia = 1e-6;

    static JsonElement Fixtures()
    {
        var arquivo = Path.Combine(AppContext.BaseDirectory, "Avatares", "fixtures.json");
        return JsonDocument.Parse(File.ReadAllText(arquivo)).RootElement;
    }

    public static TheoryData<string> Avatares()
    {
        var dados = new TheoryData<string>();
        foreach (var a in Fixtures().GetProperty("avatares").EnumerateArray()) dados.Add(a.GetProperty("definicao").GetProperty("id").GetString()!);
        return dados;
    }

    static HumorAvatar HumorDe(string? nome) => nome switch
    {
        "thinking" => HumorAvatar.Pensando,
        "happy" => HumorAvatar.Feliz,
        _ => HumorAvatar.Parado,
    };

    static MisturaDeHumor? MisturaDe(JsonElement e)
    {
        var t = e.GetProperty("t").GetDouble();
        var de = e.GetProperty("from");
        if (de.ValueKind == JsonValueKind.String) return new MisturaDeHumor(HumorDe(de.GetString()), null, t);
        return new MisturaDeHumor(HumorDe(de.GetProperty("motion").GetString()), de.TryGetProperty("blend", out var b) ? MisturaDe(b) : null, t);
    }

    static OpcoesDoQuadro OpcoesDe(JsonElement o)
    {
        double? Num(string k) => o.TryGetProperty(k, out var v) ? v.GetDouble() : null;
        var vivo = o.TryGetProperty("vivo", out var vv) && vv.GetBoolean();
        Olhar? olhar = o.TryGetProperty("gaze", out var g) ? new Olhar(g.GetProperty("x").GetDouble(), g.GetProperty("y").GetDouble(), g.TryGetProperty("weight", out var w) ? w.GetDouble() : 1) : null;
        Vista? vista = o.TryGetProperty("view", out var vi) ? new Vista(vi.GetProperty("yaw").GetDouble(), vi.GetProperty("pitch").GetDouble()) : null;
        return new OpcoesDoQuadro
        {
            Tempo = Num("time") ?? 0,
            Humor = HumorDe(o.TryGetProperty("motion", out var m) ? m.GetString() : null),
            Mistura = o.TryGetProperty("motionBlend", out var mb) ? MisturaDe(mb) : null,
            Olhar = olhar,
            Vista = vista,
            Profundidade = Num("depth"),
            Tamanho = vivo ? Num("size") : null,
            Reduzido = o.TryGetProperty("reducedMotion", out var r) && r.GetBoolean(),
            Cor = o.TryGetProperty("color", out var c) ? c.GetString() : null,
            Tinta = o.TryGetProperty("eyeColor", out var t) ? t.GetString() : null,
        };
    }

    static void Perto(List<string> erros, string onde, double esperado, double real)
    {
        if (Math.Abs(esperado - real) > Tolerancia * Math.Max(1, Math.Abs(esperado))) erros.Add($"{onde}: esperado {esperado}, veio {real}");
    }

    static void Matriz(List<string> erros, string onde, JsonElement esperado, Matriz real)
    {
        var e = esperado.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        double[] r = [real.A, real.B, real.C, real.D, real.E, real.F];
        for (var i = 0; i < 6; i++) Perto(erros, $"{onde}[{i}]", e[i], r[i]);
    }

    [Theory]
    [MemberData(nameof(Avatares))]
    public void QuadroIgualAoDoMotorJs(string id)
    {
        var avatar = Fixtures().GetProperty("avatares").EnumerateArray().First(a => a.GetProperty("definicao").GetProperty("id").GetString() == id);
        var (def, errosDeValidacao) = Corpo.Validar(avatar.GetProperty("definicao"));
        Assert.True(def is not null, string.Join("; ", errosDeValidacao));
        Assert.Equal(avatar.GetProperty("normalizado").GetString(), def!.Caminho);

        var erros = new List<string>();
        foreach (var caso in avatar.GetProperty("casos").EnumerateArray())
        {
            var opcoes = caso.GetProperty("opcoes");
            var nome = opcoes.GetRawText();
            var quadro = Quadro.Montar(def, OpcoesDe(opcoes));
            if (quadro.Cor != caso.GetProperty("cor").GetString()) erros.Add($"{nome} cor {quadro.Cor}");
            if (quadro.Tinta != caso.GetProperty("tinta").GetString()) erros.Add($"{nome} tinta {quadro.Tinta}");
            Matriz(erros, $"{nome} raiz", caso.GetProperty("raiz"), quadro.Raiz);

            var caminhos = caso.GetProperty("caminhos").EnumerateArray().ToList();
            if (caminhos.Count != quadro.Caminhos.Count) erros.Add($"{nome} caminhos: esperado {caminhos.Count}, veio {quadro.Caminhos.Count}");
            for (var i = 0; i < Math.Min(caminhos.Count, quadro.Caminhos.Count); i++)
            {
                var e = caminhos[i];
                var r = quadro.Caminhos[i];
                if (e.GetProperty("id").GetString() != r.Id) erros.Add($"{nome} caminho {i}: id {r.Id} != {e.GetProperty("id").GetString()}");
                if (e.GetProperty("hash").GetUInt32() != Pose.Hash(r.D) || e.GetProperty("tamanho").GetInt32() != r.D.Length)
                    erros.Add($"{nome} caminho {r.Id}: d diferente ({r.D.Length} vs {e.GetProperty("tamanho").GetInt32()} caracteres)");
                if (e.GetProperty("parOuImpar").GetBoolean() != r.ParOuImpar) erros.Add($"{nome} caminho {r.Id}: regra de preenchimento");
                var caixa = e.GetProperty("caixa").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                Perto(erros, $"{nome} caixa {r.Id} x", caixa[0], r.Caixa.X);
                Perto(erros, $"{nome} caixa {r.Id} y", caixa[1], r.Caixa.Y);
                Perto(erros, $"{nome} caixa {r.Id} w", caixa[2], r.Caixa.W);
                Perto(erros, $"{nome} caixa {r.Id} h", caixa[3], r.Caixa.H);
            }

            var formas = caso.GetProperty("formas").EnumerateArray().ToList();
            if (formas.Count != quadro.Formas.Count) erros.Add($"{nome} formas: esperado {formas.Count}, veio {quadro.Formas.Count}");
            for (var i = 0; i < Math.Min(formas.Count, quadro.Formas.Count); i++)
            {
                var e = formas[i];
                var r = quadro.Formas[i];
                var chave = e.GetProperty("chave").GetString();
                if (chave != r.Chave) { erros.Add($"{nome} forma {i}: chave {r.Chave} != {chave}"); continue; }
                Matriz(erros, $"{nome} forma {chave}", e.GetProperty("matriz"), r.Matriz);
                if (!e.GetProperty("olho").GetBoolean())
                {
                    if (e.GetProperty("caminho").GetString() != r.Caminho) erros.Add($"{nome} forma {chave}: caminho {r.Caminho}");
                    continue;
                }
                var marca = e.GetProperty("marca");
                if (marca.GetProperty("shape").GetString() == "arc")
                {
                    if (r.Marca is not { Arco: true } arco) { erros.Add($"{nome} {chave}: devia ser arco"); continue; }
                    if (marca.GetProperty("d").GetString() != arco.D) erros.Add($"{nome} {chave}: arco {arco.D} != {marca.GetProperty("d").GetString()}");
                    Perto(erros, $"{nome} {chave} traco", marca.GetProperty("strokeWidth").GetDouble(), arco.Traco);
                }
                else
                {
                    if (r.Marca is not { Arco: false } pilula) { erros.Add($"{nome} {chave}: devia ser pílula"); continue; }
                    Perto(erros, $"{nome} {chave} x", marca.GetProperty("x").GetDouble(), pilula.X);
                    Perto(erros, $"{nome} {chave} y", marca.GetProperty("y").GetDouble(), pilula.Y);
                    Perto(erros, $"{nome} {chave} w", marca.GetProperty("width").GetDouble(), pilula.Largura);
                    Perto(erros, $"{nome} {chave} h", marca.GetProperty("height").GetDouble(), pilula.Altura);
                    Perto(erros, $"{nome} {chave} r", marca.GetProperty("radius").GetDouble(), pilula.Raio);
                }
            }
        }
        Assert.True(erros.Count == 0, $"{erros.Count} diferenças:\n" + string.Join("\n", erros.Take(25)));
    }

    [Fact]
    public void DefinicaoInvalidaVoltaErroSemQuebrar()
    {
        var (def, erros) = Corpo.Ler("""{"version":1,"id":"x","name":"X","body":{"path":"M0 0 Q"},"eyes":{"x":50,"y":50,"spacing":15,"scale":1}}""");
        Assert.Null(def);
        Assert.NotEmpty(erros);
        Assert.Null(Corpo.Ler("não é json").Definicao);
    }
}
