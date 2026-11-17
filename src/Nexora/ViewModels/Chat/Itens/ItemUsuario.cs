using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed class ItemUsuario(string texto, IReadOnlyList<Avalonia.Media.Imaging.Bitmap>? imagens = null, int imagensSalvas = 0) : ItemChat
{
    static readonly (string? Trecho, string Texto) Nada = (null, "");
    readonly (string? Trecho, string Texto) _partes = texto.StartsWith('>') ? Core.Texto.Citacoes.Separar(texto) : Nada;
    public string Enviado { get; } = texto;
    public string Texto => _partes.Trecho is null ? Enviado : _partes.Texto;
    public CitacaoVM? Citacao => _citacao ??= _partes.Trecho is { } trecho ? new CitacaoVM(trecho) : null;
    public bool TemCitacao => _partes.Trecho is not null;
    CitacaoVM? _citacao;
    public IReadOnlyList<CitadoVM> Citados { get; } = [.. Core.Texto.Mencoes.Arquivos(texto).Select(c => new CitadoVM(c))];
    public bool TemCitados => Citados.Count > 0;
    public IReadOnlyList<Avalonia.Media.Imaging.Bitmap> Imagens { get; } = imagens ?? [];
    public bool TemTexto => Texto.Length > 0;
    public bool TemImagens => Imagens.Count > 0;
    public bool TemImagensSalvas => Imagens.Count == 0 && imagensSalvas > 0;
    public string TextoImagens => imagensSalvas == 1 ? Idioma.T("1 imagem") : Idioma.F("{0} imagens", imagensSalvas);
}
