using System.Diagnostics;
using System.Runtime.InteropServices;
using Nexora.Core.Recursos;

namespace Nexora.Core.Desempenho;

public sealed record MedidaDeProcessos(
    long NexoraRam, long NexoraWs, long NexoraPrivada, int Handles, double CpuNexora,
    Consumo Sessoes, Consumo Mcps, Consumo Builds, Consumo Outros);

public static class Cpu
{
    public static double Percentual(TimeSpan usado, TimeSpan intervalo, int nucleos)
    {
        if (usado <= TimeSpan.Zero || intervalo <= TimeSpan.Zero || nucleos <= 0) return 0;
        return Math.Min(100, usado.Ticks * 100.0 / (intervalo.Ticks * (double)nucleos));
    }
}

public sealed partial class Medidor
{
    enum Tipo : byte { Desconhecido, Visitando, Fora, Nexora, Sessao, Mcp, Build, Outro }

    readonly record struct Processo(int Pid, int Pai, long Criacao, long Cpu, long Ram, long Ws, long Privada, int Alcas, byte Imagem);

    const byte ImagemDotnet = 1;
    const byte ImagemCompilador = 2;
    const int InformacaoDeProcessos = 5;
    const int TamanhoInsuficiente = unchecked((int)0xC0000004);

    readonly int _eu = Environment.ProcessId;
    readonly int _nucleos = Environment.ProcessorCount;
    readonly List<Processo> _processos = [];
    readonly Dictionary<int, int> _indice = [];
    readonly List<int> _caminho = [];
    readonly Dictionary<(int, long), bool> _builds = [];
    readonly HashSet<int> _sessoes = [];
    readonly HashSet<int> _mcps = [];
    Dictionary<(int, long), long> _cpuAntes = [];
    Dictionary<(int, long), long> _cpuAgora = [];
    Tipo[] _tipos = [];
    long _relogioAntes;
    long _relogioAgora;
    long _horaAntes;
    long _horaAgora;
    int _tamanho = 1 << 19;

    public MedidaDeProcessos Medir(IEnumerable<int> sessoes, IEnumerable<int> mcps)
    {
        _sessoes.Clear();
        _sessoes.UnionWith(sessoes);
        _mcps.Clear();
        _mcps.UnionWith(mcps);
        return OperatingSystem.IsWindows() && Fotografar() ? Classificar() : MedirPeloProcess();
    }

    MedidaDeProcessos Classificar()
    {
        var intervalo = _relogioAntes == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(_relogioAntes, _relogioAgora);
        _indice.Clear();
        for (var i = 0; i < _processos.Count; i++) _indice[_processos[i].Pid] = i;
        if (_tipos.Length < _processos.Count) _tipos = new Tipo[_processos.Count * 2];
        Array.Clear(_tipos, 0, _processos.Count);
        _cpuAgora.Clear();
        Consumo sessoes = default, mcps = default, builds = default, outros = default;
        Processo? eu = null;
        double cpuEu = 0;
        for (var i = 0; i < _processos.Count; i++)
        {
            var tipo = TipoDe(i);
            if (tipo is Tipo.Fora) continue;
            var p = _processos[i];
            var cpu = Cpu.Percentual(TimeSpan.FromTicks(Usado(p.Pid, p.Criacao, p.Cpu)), intervalo, _nucleos);
            switch (tipo)
            {
                case Tipo.Nexora:
                    eu = p;
                    cpuEu = cpu;
                    break;
                case Tipo.Sessao:
                    sessoes = sessoes.Mais(p.Ram, cpu);
                    break;
                case Tipo.Mcp:
                    mcps = mcps.Mais(p.Ram, cpu);
                    break;
                case Tipo.Build:
                    builds = builds.Mais(p.Ram, cpu);
                    break;
                default:
                    outros = outros.Mais(p.Ram, cpu);
                    break;
            }
        }
        Virar(_relogioAgora, _horaAgora);
        EsquecerBuilds();
        var proprio = eu ?? default;
        return new MedidaDeProcessos(proprio.Ram, proprio.Ws, proprio.Privada, proprio.Alcas, cpuEu, sessoes, mcps, builds, outros);
    }

    long Usado(int pid, long criacao, long cpu)
    {
        var chave = (pid, criacao);
        _cpuAgora[chave] = cpu;
        if (_cpuAntes.TryGetValue(chave, out var antes)) return Math.Max(0, cpu - antes);
        return _horaAntes != 0 && criacao > _horaAntes ? cpu : 0;
    }

    void Virar(long relogio, long hora)
    {
        (_cpuAntes, _cpuAgora) = (_cpuAgora, _cpuAntes);
        _relogioAntes = relogio;
        _horaAntes = hora;
    }

    Tipo TipoDe(int i)
    {
        if (_tipos[i] > Tipo.Visitando) return _tipos[i];
        _caminho.Clear();
        var atual = i;
        Tipo achado;
        while (true)
        {
            var conhecido = _tipos[atual];
            if (conhecido is not Tipo.Desconhecido)
            {
                achado = conhecido is Tipo.Visitando ? Tipo.Fora : conhecido;
                break;
            }
            if (Proprio(_processos[atual]) is { } proprio)
            {
                _tipos[atual] = proprio;
                achado = proprio;
                break;
            }
            _tipos[atual] = Tipo.Visitando;
            _caminho.Add(atual);
            var p = _processos[atual];
            if (p.Pai == p.Pid || !_indice.TryGetValue(p.Pai, out var pai) || _processos[pai].Criacao > p.Criacao)
            {
                achado = Tipo.Fora;
                break;
            }
            atual = pai;
        }
        var herdado = achado switch
        {
            Tipo.Nexora or Tipo.Outro => Tipo.Outro,
            Tipo.Sessao or Tipo.Mcp or Tipo.Build => achado,
            _ => Tipo.Fora,
        };
        foreach (var n in _caminho) _tipos[n] = herdado;
        return _tipos[i];
    }

    Tipo? Proprio(in Processo p)
    {
        if (p.Pid == _eu) return Tipo.Nexora;
        if (_mcps.Contains(p.Pid)) return Tipo.Mcp;
        if (EhBuild(p)) return Tipo.Build;
        if (_sessoes.Contains(p.Pid)) return Tipo.Sessao;
        return null;
    }

    bool EhBuild(in Processo p)
    {
        if (p.Imagem == ImagemCompilador) return true;
        if (p.Imagem != ImagemDotnet || p.Pid == _eu) return false;
        var chave = (p.Pid, p.Criacao);
        if (!_builds.TryGetValue(chave, out var build))
            _builds[chave] = build = ServidoresDeBuild.Classificar("dotnet", ServidoresDeBuild.LinhaDeComando(p.Pid)) is not null;
        return build;
    }

    void EsquecerBuilds()
    {
        List<(int, long)>? velhos = null;
        foreach (var chave in _builds.Keys)
            if (!_indice.TryGetValue(chave.Item1, out var i) || _processos[i].Criacao != chave.Item2) (velhos ??= []).Add(chave);
        if (velhos is not null) foreach (var chave in velhos) _builds.Remove(chave);
    }

    MedidaDeProcessos MedirPeloProcess()
    {
        var relogio = Stopwatch.GetTimestamp();
        var intervalo = _relogioAntes == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(_relogioAntes, relogio);
        _cpuAgora.Clear();
        double CpuDe(int pid, TimeSpan total)
        {
            _cpuAgora[(pid, 0)] = total.Ticks;
            var usado = _cpuAntes.TryGetValue((pid, 0), out var antes) ? total.Ticks - antes : 0;
            return Cpu.Percentual(TimeSpan.FromTicks(usado), intervalo, _nucleos);
        }
        Consumo Somar(IEnumerable<int> pids)
        {
            Consumo soma = default;
            foreach (var pid in pids)
                try
                {
                    using var p = Process.GetProcessById(pid);
                    soma = soma.Mais(p.WorkingSet64, CpuDe(pid, p.TotalProcessorTime));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                }
            return soma;
        }
        using var eu = Process.GetCurrentProcess();
        var cpuEu = CpuDe(eu.Id, eu.TotalProcessorTime);
        var sessoes = Somar(_sessoes.Where(p => !_mcps.Contains(p)));
        var mcps = Somar(_mcps);
        Virar(relogio, DateTime.UtcNow.ToFileTimeUtc());
        return new MedidaDeProcessos(eu.WorkingSet64, eu.WorkingSet64, eu.PrivateMemorySize64, eu.HandleCount, cpuEu, sessoes, mcps, default, default);
    }

    unsafe bool Fotografar()
    {
        _processos.Clear();
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            var memoria = (byte*)NativeMemory.Alloc((nuint)_tamanho);
            try
            {
                var status = NtQuerySystemInformation(InformacaoDeProcessos, (IntPtr)memoria, _tamanho, out var precisa);
                if (status == TamanhoInsuficiente)
                {
                    _tamanho = Math.Max(_tamanho * 2, precisa + (1 << 16));
                    continue;
                }
                if (status != 0) return false;
                _relogioAgora = Stopwatch.GetTimestamp();
                _horaAgora = DateTime.UtcNow.ToFileTimeUtc();
                Ler(memoria);
                return true;
            }
            finally
            {
                NativeMemory.Free(memoria);
            }
        }
        return false;
    }

    unsafe void Ler(byte* inicio)
    {
        long deslocamento = 0;
        while (true)
        {
            var info = (InfoDeProcesso*)(inicio + deslocamento);
            var nome = info->Nome == IntPtr.Zero ? default : new ReadOnlySpan<char>((void*)info->Nome, info->TamanhoDoNome / 2);
            var imagem = nome.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) ? ImagemDotnet
                : nome.Equals("VBCSCompiler.exe", StringComparison.OrdinalIgnoreCase) ? ImagemCompilador : (byte)0;
            _processos.Add(new Processo((int)info->Pid, (int)info->Pai, info->Criacao, info->TempoDeUsuario + info->TempoDeNucleo,
                info->PrivadoNoWs, (long)(nuint)info->Ws, (long)(nuint)info->Privado, (int)info->Alcas, imagem));
            if (info->Proximo == 0) return;
            deslocamento += info->Proximo;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct InfoDeProcesso
    {
        public uint Proximo;
        public uint Threads;
        public long PrivadoNoWs;
        public uint FaltasDuras;
        public uint PicoDeThreads;
        public ulong Ciclos;
        public long Criacao;
        public long TempoDeUsuario;
        public long TempoDeNucleo;
        public ushort TamanhoDoNome;
        public ushort CapacidadeDoNome;
        public IntPtr Nome;
        public int Prioridade;
        public IntPtr Pid;
        public IntPtr Pai;
        public uint Alcas;
        public uint Sessao;
        public UIntPtr Chave;
        public UIntPtr PicoVirtual;
        public UIntPtr Virtual;
        public uint FaltasDePagina;
        public UIntPtr PicoWs;
        public UIntPtr Ws;
        public UIntPtr PicoPaginado;
        public UIntPtr Paginado;
        public UIntPtr PicoNaoPaginado;
        public UIntPtr NaoPaginado;
        public UIntPtr Pagefile;
        public UIntPtr PicoPagefile;
        public UIntPtr Privado;
    }

    [LibraryImport("ntdll.dll")]
    private static partial int NtQuerySystemInformation(int classe, IntPtr dados, int tamanho, out int devolvido);
}
