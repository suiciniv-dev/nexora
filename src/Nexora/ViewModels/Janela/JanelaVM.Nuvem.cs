using Nexora.Core.Nuvem;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    public static TimeSpan IdadeDaNuvem { get; set; } = TimeSpan.FromDays(7);

    public void ReabrirDaNuvem()
    {
        if (Demo) return;
        foreach (var execucao in App.Nuvem.Execucoes.Where(e => e.SessaoId is not null && e.Agente == Core.Claude.Agentes.Claude && DateTime.Now - e.Criada < IdadeDaNuvem))
        {
            if (App.Nuvem.Maquina(execucao.Maquina) is null || Abertas.Any(c => c.SessaoId == execucao.SessaoId)) continue;
            var pasta = Directory.Exists(execucao.PastaLocal) ? execucao.PastaLocal : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Abertas.Insert(0, ChatVM.DaNuvem(execucao, LocalDe(pasta), this, AoUsar, Fechar));
        }
    }

    public void NuvemDemo()
    {
        SugestaoDaNuvem.Medir = () => 0.91;
        ChatVM.MedirMemoria();
        var maquina = App.Nuvem.Salvar(new MaquinaNaNuvem("demo", "VPS Oracle", TiposDeMaquina.Ssh, "vps.exemplo.com", "ubuntu", 22, "/home/ubuntu",
            DateTime.Now.AddMinutes(-20), "Linux · git · node · claude"));
        App.Config.ProjetosNaNuvem.Add(Config.ChaveDePasta(@"D:\Projetos\Loja-pedidos"));
        App.Config.ProjetosNaNuvem.Add(Config.ChaveDePasta(@"D:\Projetos\App.Notas"));
        if (Abertas.FirstOrDefault(c => c.Pasta.EndsWith("Loja-pedidos", StringComparison.OrdinalIgnoreCase)) is { } chat)
        {
            chat.MaquinaId = maquina.Id;
            chat.EstadoNuvem = EstadoDaNuvem.Rodando;
        }
    }
}
