using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public interface IAnfitriao
{
    void AbrirSessao(Local local, string? primeiraMensagem = null);
    bool Ocupada(string pasta);
    void BranchMudou(string pasta);
    AmbienteVM Registrar(Ambiente ambiente);
    void Esconder(AmbienteVM ambiente);
    void PedirExclusao(AmbienteVM ambiente);
    bool LiberadaEmTodas(Core.Claude.PermissaoPedida pedido);
    void LiberarEmTodas(string chave);
    void Lembrar(Ambiente ambiente);
    AmbienteVM? AmbienteDe(string pasta);
    IReadOnlyList<Local> Locais();
    Local LocalDe(string pasta);
    void Mostrar(ChatVM chat);
    IReadOnlyList<ConversaVM> ConversasDe(string pasta);
    string? EmOutroLugar(string sessaoId);
    void MostrarExecutar(Local local);
    IReadOnlyList<string> UsosDe(string pasta);
    void Esquecer(string pasta);
    void AbrirLateral(ChatVM origem, string? pergunta) { }
    void AbrirEmSplit(object conteudo) { }
    IReadOnlyList<ServidorNoAr> ServidoresNoAr(Local local) => [];
}
