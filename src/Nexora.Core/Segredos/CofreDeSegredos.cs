using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Nexora.Core.Segredos;

public sealed record SegredoGuardado(string Id, string Nome, string Projeto, DestinoDoSegredo Destino, string? Arquivo, DateTime Criado)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Caminho => Arquivo is null ? null : Path.GetFullPath(Path.Combine(Projeto, Arquivo));
}

public interface IArmazemDeValores
{
    void Gravar(string id, string valor);
    string? Ler(string id);
    void Apagar(string id);
}

public sealed class ArmazemNaMemoria : IArmazemDeValores
{
    readonly Dictionary<string, string> _valores = [];

    public void Gravar(string id, string valor)
    {
        lock (_valores) _valores[id] = valor;
    }

    public string? Ler(string id)
    {
        lock (_valores) return _valores.GetValueOrDefault(id);
    }

    public void Apagar(string id)
    {
        lock (_valores) _valores.Remove(id);
    }
}

public sealed class ArmazemDpapi(string pasta) : IArmazemDeValores
{
    static readonly byte[] Entropia = Encoding.UTF8.GetBytes("Nexora.Segredos.v1");
    const int SemInterface = 0x1;

    string Arquivo(string id) => Path.Combine(pasta, id + ".bin");

    public void Gravar(string id, string valor)
    {
        Directory.CreateDirectory(pasta);
        Arquivos.ArquivoSeguro.Gravar(Arquivo(id), Proteger(Encoding.UTF8.GetBytes(valor)));
    }

    public string? Ler(string id)
    {
        var arquivo = Arquivo(id);
        if (!File.Exists(arquivo)) return null;
        return Encoding.UTF8.GetString(Abrir(File.ReadAllBytes(arquivo)));
    }

    public void Apagar(string id)
    {
        var arquivo = Arquivo(id);
        if (File.Exists(arquivo)) File.Delete(arquivo);
    }

    static byte[] Proteger(byte[] dados) => Chamar(dados, proteger: true);

    static byte[] Abrir(byte[] dados) => Chamar(dados, proteger: false);

    static byte[] Chamar(byte[] dados, bool proteger)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var entrada = Alocar(dados);
        var entropia = Alocar(Entropia);
        var saida = default(Blob);
        try
        {
            var ok = proteger
                ? CryptProtectData(ref entrada, null, ref entropia, IntPtr.Zero, IntPtr.Zero, SemInterface, ref saida)
                : CryptUnprotectData(ref entrada, IntPtr.Zero, ref entropia, IntPtr.Zero, IntPtr.Zero, SemInterface, ref saida);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            var resultado = new byte[saida.Tamanho];
            Marshal.Copy(saida.Dados, resultado, 0, saida.Tamanho);
            return resultado;
        }
        finally
        {
            Liberar(entrada);
            Liberar(entropia);
            if (saida.Dados != IntPtr.Zero)
            {
                ZerarMemoria(saida.Dados, saida.Tamanho);
                LocalFree(saida.Dados);
            }
        }
    }

    static Blob Alocar(byte[] dados)
    {
        var p = Marshal.AllocHGlobal(Math.Max(1, dados.Length));
        Marshal.Copy(dados, 0, p, dados.Length);
        return new Blob { Tamanho = dados.Length, Dados = p };
    }

    static void Liberar(Blob b)
    {
        if (b.Dados == IntPtr.Zero) return;
        ZerarMemoria(b.Dados, b.Tamanho);
        Marshal.FreeHGlobal(b.Dados);
    }

    static void ZerarMemoria(IntPtr p, int tamanho)
    {
        for (var i = 0; i < tamanho; i++) Marshal.WriteByte(p, i, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Blob
    {
        public int Tamanho;
        public IntPtr Dados;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref Blob entrada, string? descricao, ref Blob entropia, IntPtr reservado, IntPtr aviso, int opcoes, ref Blob saida);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref Blob entrada, IntPtr descricao, ref Blob entropia, IntPtr reservado, IntPtr aviso, int opcoes, ref Blob saida);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr memoria);
}

public sealed class ArmazemKeychain : IArmazemDeValores
{
    const string Servico = "Nexora";

    public void Gravar(string id, string valor) =>
        Rodar($"add-generic-password -U -s {Servico} -a {id} -l \"Nexora {id}\" -w {Convert.ToHexString(Encoding.UTF8.GetBytes(valor))}\n");

    public string? Ler(string id)
    {
        var (codigo, saida) = Rodar($"find-generic-password -s {Servico} -a {id} -w\n");
        if (codigo != 0) return null;
        var hex = saida.Replace("security> ", "\n").Split('\n',StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault(l => l.Length > 0 && l.All(Uri.IsHexDigit));
        return hex is null ? null : Encoding.UTF8.GetString(Convert.FromHexString(hex));
    }

    public void Apagar(string id) => Rodar($"delete-generic-password -s {Servico} -a {id}\n");

    static (int Codigo, string Saida) Rodar(string comando)
    {
        var info = new ProcessStartInfo("/usr/bin/security")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("-i");
        using var processo = Process.Start(info) ?? throw new InvalidOperationException("O security não abriu.");
        processo.StandardInput.Write(comando);
        processo.StandardInput.Close();
        var saida = processo.StandardOutput.ReadToEnd();
        processo.StandardError.ReadToEnd();
        processo.WaitForExit(15000);
        return (processo.HasExited ? processo.ExitCode : -1, saida);
    }
}

public sealed class CofreDeSegredos
{
    readonly string? _indice;
    readonly IArmazemDeValores? _armazem;
    readonly object _trava = new();
    List<SegredoGuardado> _lista;

    public CofreDeSegredos(string? pasta, IArmazemDeValores? armazem)
    {
        _indice = pasta is null ? null : Path.Combine(pasta, "segredos.json");
        _armazem = armazem;
        _lista = Carregar();
    }

    public static IArmazemDeValores? ArmazemDoSistema(string pasta) =>
        OperatingSystem.IsWindows() ? new ArmazemDpapi(Path.Combine(pasta, "segredos"))
        : OperatingSystem.IsMacOS() ? new ArmazemKeychain()
        : null;

    public bool GuardaVariaveis => _armazem is not null;

    public event Action? Mudou;

    public IReadOnlyList<SegredoGuardado> Lista()
    {
        lock (_trava) return [.. _lista.OrderBy(s => s.Projeto, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Nome, StringComparer.Ordinal)];
    }

    public SegredoGuardado GuardarVariavel(string nome, string projeto, string valor)
    {
        if (_armazem is null) throw new PlatformNotSupportedException("Este sistema não tem onde guardar o segredo de forma protegida.");
        projeto = Normalizar(projeto);
        SegredoGuardado novo;
        lock (_trava)
        {
            var antigo = _lista.FirstOrDefault(s => s.Destino == DestinoDoSegredo.Variavel && s.Nome == nome && Igual(s.Projeto, projeto));
            novo = new SegredoGuardado(antigo?.Id ?? NovoId(), nome, projeto, DestinoDoSegredo.Variavel, null, DateTime.Now);
            _armazem.Gravar(novo.Id, valor);
            if (antigo is not null) _lista.Remove(antigo);
            _lista.Add(novo);
            Salvar();
        }
        Mudou?.Invoke();
        return novo;
    }

    public SegredoGuardado RegistrarArquivo(string nome, string projeto, string arquivo)
    {
        projeto = Normalizar(projeto);
        SegredoGuardado novo;
        lock (_trava)
        {
            var caminho = Path.GetFullPath(Path.Combine(projeto, arquivo));
            var antigo = _lista.FirstOrDefault(s => s.Destino == DestinoDoSegredo.ArquivoEnv && s.Nome == nome && string.Equals(s.Caminho, caminho, StringComparison.OrdinalIgnoreCase));
            novo = new SegredoGuardado(antigo?.Id ?? NovoId(), nome, projeto, DestinoDoSegredo.ArquivoEnv, arquivo, DateTime.Now);
            if (antigo is not null) _lista.Remove(antigo);
            _lista.Add(novo);
            Salvar();
        }
        Mudou?.Invoke();
        return novo;
    }

    public void Apagar(string id)
    {
        SegredoGuardado? achado;
        lock (_trava)
        {
            achado = _lista.FirstOrDefault(s => s.Id == id);
            if (achado is null) return;
            if (achado.Destino == DestinoDoSegredo.Variavel) _armazem?.Apagar(achado.Id);
            else if (achado.Caminho is { } caminho) ArquivoEnv.Remover(caminho, achado.Nome);
            _lista.Remove(achado);
            Salvar();
        }
        Mudou?.Invoke();
    }

    public IReadOnlyDictionary<string, string> VariaveisDe(string? pasta)
    {
        var variaveis = new Dictionary<string, string>(StringComparer.Ordinal);
        if (_armazem is null || string.IsNullOrWhiteSpace(pasta)) return variaveis;
        List<SegredoGuardado> doProjeto;
        var alvo = Normalizar(pasta);
        lock (_trava) doProjeto = [.. _lista.Where(s => s.Destino == DestinoDoSegredo.Variavel && Dentro(alvo, s.Projeto)).OrderBy(s => s.Projeto.Length)];
        foreach (var s in doProjeto)
        {
            try
            {
                if (_armazem.Ler(s.Id) is { } valor) variaveis[s.Nome] = valor;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or FormatException or InvalidOperationException)
            {
            }
        }
        return variaveis;
    }

    static bool Dentro(string pasta, string projeto) =>
        Igual(pasta, projeto) || pasta.StartsWith(projeto.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, Comparacao);

    static StringComparison Comparacao => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    static bool Igual(string a, string b) => string.Equals(a, b, Comparacao);

    static string Normalizar(string pasta) => Path.GetFullPath(pasta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    static string NovoId() => Guid.NewGuid().ToString("N")[..16];

    public Arquivos.OrigemDaLeitura Origem { get; private set; } = Arquivos.OrigemDaLeitura.Novo;

    List<SegredoGuardado> Carregar()
    {
        if (_indice is null) return [];
        var lida = Arquivos.ArquivoSeguro.LerJson(_indice, () => new List<SegredoGuardado>());
        Origem = lida.Origem;
        return lida.Valor;
    }

    void Salvar()
    {
        if (_indice is null) return;
        if (Origem == Arquivos.OrigemDaLeitura.Ilegivel) throw new Arquivos.ArquivoIlegivelException(_indice, Texto.Idioma.T("o arquivo estava em uso quando o Nexora abriu"));
        Arquivos.ArquivoSeguro.Gravar(_indice, JsonSerializer.Serialize(_lista, new JsonSerializerOptions { WriteIndented = true }), manterCopia: true);
    }
}
