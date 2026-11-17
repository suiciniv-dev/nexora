using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class InstrucaoVM(string caminho, string? projeto)
{
    public string Caminho { get; } = caminho;
    public string Nome => Path.GetFileName(Caminho);
    public string Detalhe
    {
        get
        {
            long tamanho;
            try
            {
                tamanho = new FileInfo(Caminho).Length;
            }
            catch (IOException)
            {
                tamanho = 0;
            }
            var texto = tamanho >= 1024 ? $"{(tamanho / 1024.0).ToString("0.#", Idioma.Cultura)} kB" : $"{tamanho} B";
            return projeto is null ? texto : $"{projeto} · {texto}";
        }
    }

    [RelayCommand]
    void Abrir()
    {
        try
        {
            Sistema.Abrir(new ProcessStartInfo(Caminho) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}
