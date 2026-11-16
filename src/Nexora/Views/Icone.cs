using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Nexora.Views;

public sealed class Icone : Control
{
    public static readonly StyledProperty<string?> NomeProperty =
        AvaloniaProperty.Register<Icone, string?>(nameof(Nome));

    public static readonly StyledProperty<double> TamanhoProperty =
        AvaloniaProperty.Register<Icone, double>(nameof(Tamanho), 16);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Icone>();

    static readonly Dictionary<string, string> Caminhos = new()
    {
        ["mais"] = "M12 5 L12 19 M5 12 L19 12",
        ["inicio"] = "M3 10.5 L12 3 L21 10.5 M5 9 L5 19 A2 2 0 0 0 7 21 L17 21 A2 2 0 0 0 19 19 L19 9 M10 21 L10 15 L14 15 L14 21",
        ["ajustes"] = "M12.22 2 L11.78 2 A2 2 0 0 0 9.78 4 L9.78 4.18 A2 2 0 0 1 8.78 5.91 L8.35 6.16 A2 2 0 0 1 6.35 6.16 L6.2 6.08 A2 2 0 0 0 3.47 6.81 L3.25 7.19 A2 2 0 0 0 3.98 9.92 L4.13 10.02 A2 2 0 0 1 5.13 11.74 L5.13 12.25 A2 2 0 0 1 4.13 13.99 L3.98 14.08 A2 2 0 0 0 3.25 16.81 L3.47 17.19 A2 2 0 0 0 6.2 17.92 L6.35 17.84 A2 2 0 0 1 8.35 17.84 L8.78 18.09 A2 2 0 0 1 9.78 19.82 L9.78 20 A2 2 0 0 0 11.78 22 L12.22 22 A2 2 0 0 0 14.22 20 L14.22 19.82 A2 2 0 0 1 15.22 18.09 L15.65 17.84 A2 2 0 0 1 17.65 17.84 L17.8 17.92 A2 2 0 0 0 20.53 17.19 L20.75 16.8 A2 2 0 0 0 20.02 14.07 L19.87 13.99 A2 2 0 0 1 18.87 12.25 L18.87 11.75 A2 2 0 0 1 19.87 10.01 L20.02 9.92 A2 2 0 0 0 20.75 7.19 L20.53 6.81 A2 2 0 0 0 17.8 6.08 L17.65 6.16 A2 2 0 0 1 15.65 6.16 L15.22 5.91 A2 2 0 0 1 14.22 4.18 L14.22 4 A2 2 0 0 0 12.22 2 Z M12 9 A3 3 0 1 0 12 15 A3 3 0 1 0 12 9",
        ["dividir"] = "M5 3 L19 3 A2 2 0 0 1 21 5 L21 19 A2 2 0 0 1 19 21 L5 21 A2 2 0 0 1 3 19 L3 5 A2 2 0 0 1 5 3 Z M12 3 L12 21",
        ["fechar"] = "M18 6 L6 18 M6 6 L18 18",
        ["foco"] = "M3 8 L3 5 A2 2 0 0 1 5 3 L8 3 M16 3 L19 3 A2 2 0 0 1 21 5 L21 8 M21 16 L21 19 A2 2 0 0 1 19 21 L16 21 M8 21 L5 21 A2 2 0 0 1 3 19 L3 16 M9 12 A3 3 0 1 0 15 12 A3 3 0 1 0 9 12",
        ["modelo"] = "M6 3 L18 3 A3 3 0 0 1 21 6 L21 18 A3 3 0 0 1 18 21 L6 21 A3 3 0 0 1 3 18 L3 6 A3 3 0 0 1 6 3 Z M8 9 L16 9 M8 13 L13 13",
        ["branch"] = "M6 3 L6 15 M15 6 A3 3 0 1 0 21 6 A3 3 0 1 0 15 6 M3 18 A3 3 0 1 0 9 18 A3 3 0 1 0 3 18 M18 9 A9 9 0 0 1 9 18",
        ["worktree"] = "M9 18 A3 3 0 1 0 15 18 A3 3 0 1 0 9 18 M3 6 A3 3 0 1 0 9 6 A3 3 0 1 0 3 6 M15 6 A3 3 0 1 0 21 6 A3 3 0 1 0 15 6 M18 9 L18 11 Q18 12 17 12 L7 12 Q6 12 6 11 L6 9 M12 12 L12 15",
        ["pasta"] = "M20 20 A2 2 0 0 0 22 18 L22 8 A2 2 0 0 0 20 6 L12.1 6 A2 2 0 0 1 10.41 5.1 L9.6 3.9 A2 2 0 0 0 7.93 3 L4 3 A2 2 0 0 0 2 5 L2 18 A2 2 0 0 0 4 20 Z",
        ["camadas"] = "M12.83 2.18 A2 2 0 0 0 11.17 2.18 L2.6 6.08 A1 1 0 0 0 2.6 7.91 L11.18 11.82 A2 2 0 0 0 12.84 11.82 L21.42 7.92 A1 1 0 0 0 21.42 6.09 Z M2 12 A1 1 0 0 0 2.58 12.91 L11.18 16.82 A2 2 0 0 0 12.83 16.82 L21.41 12.92 A1 1 0 0 0 22 12 M2 17 A1 1 0 0 0 2.58 17.91 L11.18 21.82 A2 2 0 0 0 12.83 21.82 L21.41 17.92 A1 1 0 0 0 22 17",
        ["conversa"] = "M21 15 A2 2 0 0 1 19 17 L7 17 L3 21 L3 5 A2 2 0 0 1 5 3 L19 3 A2 2 0 0 1 21 5 Z",
        ["enviar"] = "M12 19 L12 5 M5 12 L12 5 L19 12",
        ["parar"] = "M8 6 L16 6 A2 2 0 0 1 18 8 L18 16 A2 2 0 0 1 16 18 L8 18 A2 2 0 0 1 6 16 L6 8 A2 2 0 0 1 8 6 Z",
        ["lua"] = "M12 3 A6 6 0 0 0 21 12 A9 9 0 1 1 12 3 Z",
        ["sol"] = "M8 12 A4 4 0 1 0 16 12 A4 4 0 1 0 8 12 M12 2 L12 4 M12 20 L12 22 M4.93 4.93 L6.34 6.34 M17.66 17.66 L19.07 19.07 M2 12 L4 12 M20 12 L22 12 M6.34 17.66 L4.93 19.07 M19.07 4.93 L17.66 6.34",
        ["monitor"] = "M4 3 L20 3 A2 2 0 0 1 22 5 L22 15 A2 2 0 0 1 20 17 L4 17 A2 2 0 0 1 2 15 L2 5 A2 2 0 0 1 4 3 Z M8 21 L16 21 M12 17 L12 21",
        ["nuvem"] = "M17.5 19 L9 19 A7 7 0 1 1 15.71 10 L17.5 10 A4.5 4.5 0 1 1 17.5 19 Z",
        ["desfazer"] = "M9 14 L4 9 L9 4 M4 9 L14.5 9 A5.5 5.5 0 0 1 14.5 20 L11 20",
        ["atualizar"] = "M3 12 A9 9 0 0 1 12 3 A9.75 9.75 0 0 1 18.74 5.74 L21 8 M21 3 L21 8 L16 8 M21 12 A9 9 0 0 1 12 21 A9.75 9.75 0 0 1 5.26 18.26 L3 16 M8 16 L3 16 L3 21",
        ["busca"] = "M3 11 A8 8 0 1 0 19 11 A8 8 0 1 0 3 11 M21 21 L16.7 16.7",
        ["vinculo"] = "M10 13 A5 5 0 0 0 17.54 13.54 L20.54 10.54 A5 5 0 0 0 13.47 3.47 L11.75 5.18 M14 11 A5 5 0 0 0 6.46 10.46 L3.46 13.46 A5 5 0 0 0 10.53 20.53 L12.24 18.82",
        ["esquerda"] = "M15 18 L9 12 L15 6",
        ["issue"] = "M12 2 A10 10 0 1 0 12 22 A10 10 0 1 0 12 2 M12 11 A1 1 0 1 0 12 13 A1 1 0 1 0 12 11",
        ["grafico"] ="M3 3 L3 19 A2 2 0 0 0 5 21 L21 21 M18 17 L18 9 M13 17 L13 5 M8 17 L8 14",
        ["maximizar"] ="M15 3 L21 3 L21 9 M9 21 L3 21 L3 15 M21 3 L14 10 M3 21 L10 14",
        ["externo"] ="M15 3 L21 3 L21 9 M10 14 L21 3 M18 13 L18 19 A2 2 0 0 1 16 21 L5 21 A2 2 0 0 1 3 19 L3 8 A2 2 0 0 1 5 6 L11 6",
        ["abaixo"] = "M6 9 L12 15 L18 9",
        ["acima"] = "M6 15 L12 9 L18 15",
        ["direita"] = "M9 18 L15 12 L9 6",
        ["ok"] = "M20 6 L9 17 L4 12",
        ["faisca"] = "M12 3 L12 21 M3 12 L21 12 M6.6 6.6 L17.4 17.4 M17.4 6.6 L6.6 17.4",
        ["lapis"] = "M17 3 L21 7 L8 20 L3 21 L4 16 Z M15 5 L19 9",
        ["microfone"] = "M12 2 A3 3 0 0 0 9 5 L9 12 A3 3 0 0 0 15 12 L15 5 A3 3 0 0 0 12 2 Z M19 10 L19 12 A7 7 0 0 1 5 12 L5 10 M12 19 L12 22",
        ["passagem"] = "M3 12 A9 9 0 1 0 6 5.3 L3 8 M3 3 L3 8 L8 8 M12 7 L12 12 L15.5 14",
        ["raio"] = "M13 2 L4 14 L12 14 L11 22 L20 10 L12 10 Z",
        ["terminal"] = "M4 17 L10 11 L4 5 M12 19 L20 19",
        ["memoria"] = "M6 4 L18 4 A2 2 0 0 1 20 6 L20 18 A2 2 0 0 1 18 20 L6 20 A2 2 0 0 1 4 18 L4 6 A2 2 0 0 1 6 4 Z M9 9 L15 9 L15 15 L9 15 Z M9 1.5 L9 4 M15 1.5 L15 4 M9 20 L9 22.5 M15 20 L15 22.5 M20 9 L22.5 9 M20 15 L22.5 15 M1.5 9 L4 9 M1.5 15 L4 15",
        ["fixar"] = "M12 17 L12 22 M9 10.76 A2 2 0 0 1 7.89 12.55 L6.11 13.45 A2 2 0 0 0 5 15.24 L5 16 A1 1 0 0 0 6 17 L18 17 A1 1 0 0 0 19 16 L19 15.24 A2 2 0 0 0 17.89 13.45 L16.11 12.55 A2 2 0 0 1 15 10.76 L15 7 A1 1 0 0 1 16 6 A2 2 0 0 0 18 4 A2 2 0 0 0 16 2 L8 2 A2 2 0 0 0 6 4 A2 2 0 0 0 8 6 A1 1 0 0 1 9 7 Z",
        ["arquivos"] = "M15 2 L9 2 A2 2 0 0 0 7 4 L7 16 A2 2 0 0 0 9 18 L18 18 A2 2 0 0 0 20 16 L20 7 Z M15 2 L15 7 L20 7 M3 8 L3 20 A2 2 0 0 0 5 22 L15 22",
        ["painel-direito"] = "M5 3 L19 3 A2 2 0 0 1 21 5 L21 19 A2 2 0 0 1 19 21 L5 21 A2 2 0 0 1 3 19 L3 5 A2 2 0 0 1 5 3 Z M15 3 L15 21",
        ["painel-baixo"] = "M5 3 L19 3 A2 2 0 0 1 21 5 L21 19 A2 2 0 0 1 19 21 L5 21 A2 2 0 0 1 3 19 L3 5 A2 2 0 0 1 5 3 Z M3 15 L21 15",
        ["globo"] = "M2 12 A10 10 0 1 0 22 12 A10 10 0 1 0 2 12 M2 12 L22 12 M12 2 A15.3 15.3 0 0 1 16 12 A15.3 15.3 0 0 1 12 22 A15.3 15.3 0 0 1 8 12 A15.3 15.3 0 0 1 12 2 Z",
        ["pr"] = "M3 6 A3 3 0 1 0 9 6 A3 3 0 1 0 3 6 M15 18 A3 3 0 1 0 21 18 A3 3 0 1 0 15 18 M13 6 L16 6 A2 2 0 0 1 18 8 L18 15 M6 9 L6 21 M15.5 3.5 L13 6 L15.5 8.5",
        ["pulado"] = "M3 12 A9 9 0 1 0 21 12 A9 9 0 1 0 3 12 M5.6 18.4 L18.4 5.6",
        ["commit"] = "M9 12 A3 3 0 1 0 15 12 A3 3 0 1 0 9 12 M3 12 L9 12 M15 12 L21 12",
        ["quadro"] = "M5 4 L8 4 A1 1 0 0 1 9 5 L9 19 A1 1 0 0 1 8 20 L5 20 A1 1 0 0 1 4 19 L4 5 A1 1 0 0 1 5 4 Z M11 4 L13 4 A1 1 0 0 1 14 5 L14 13 A1 1 0 0 1 13 14 L11 14 A1 1 0 0 1 10 13 L10 5 A1 1 0 0 1 11 4 Z M17 4 L19 4 A1 1 0 0 1 20 5 L20 16 A1 1 0 0 1 19 17 L17 17 A1 1 0 0 1 16 16 L16 5 A1 1 0 0 1 17 4 Z",
        ["importar"] = "M12 3 L12 15 M7 8 L12 3 L17 8 M5 21 L19 21",
        ["lista"] = "M8 6 L21 6 M8 12 L21 12 M8 18 L21 18 M3 6 L3.01 6 M3 12 L3.01 12 M3 18 L3.01 18",
        ["ajuda"] = "M2 12 A10 10 0 1 0 22 12 A10 10 0 1 0 2 12 M9.1 9 A3 3 0 0 1 14.9 10 C14.9 12 11.9 13 11.9 13 M12 17 L12.01 17",
        ["codigo"] = "M16 18 L22 12 L16 6 M8 6 L2 12 L8 18",
        ["teclado"] = "M4 5 L20 5 A2 2 0 0 1 22 7 L22 17 A2 2 0 0 1 20 19 L4 19 A2 2 0 0 1 2 17 L2 7 A2 2 0 0 1 4 5 Z M6 9 L6.01 9 M10 9 L10.01 9 M14 9 L14.01 9 M18 9 L18.01 9 M8 12 L8.01 12 M12 12 L12.01 12 M16 12 L16.01 12 M7 15.5 L17 15.5",
        ["brilho"] = "M12 3 L13.9 8.1 L19 10 L13.9 11.9 L12 17 L10.1 11.9 L5 10 L10.1 8.1 Z M19 17 L19 21 M17 19 L21 19",
        ["relogio"] = "M2 12 A10 10 0 1 0 22 12 A10 10 0 1 0 2 12 M12 6 L12 12 L16 14",
        ["arquivo"] = "M15 2 L6 2 A2 2 0 0 0 4 4 L4 20 A2 2 0 0 0 6 22 L18 22 A2 2 0 0 0 20 20 L20 7 Z M14 2 L14 6 A2 2 0 0 0 16 8 L20 8",
        ["salvar"] = "M15.2 3 A2 2 0 0 1 16.6 3.6 L20.4 7.4 A2 2 0 0 1 21 8.8 L21 19 A2 2 0 0 1 19 21 L5 21 A2 2 0 0 1 3 19 L3 5 A2 2 0 0 1 5 3 Z M17 21 L17 14 A1 1 0 0 0 16 13 L8 13 A1 1 0 0 0 7 14 L7 21 M7 3 L7 7 A1 1 0 0 0 8 8 L15 8",
        ["painel"] = "M5 3 L19 3 A2 2 0 0 1 21 5 L21 19 A2 2 0 0 1 19 21 L5 21 A2 2 0 0 1 3 19 L3 5 A2 2 0 0 1 5 3 Z M15 3 L15 21",
        ["diff"] = "M6 3 L6 15 M3 6 A3 3 0 1 0 9 6 A3 3 0 1 0 3 6 M15 18 A3 3 0 1 0 21 18 A3 3 0 1 0 15 18 M6 15 Q6 18 9 18 L12 18 M18 15 L18 9 Q18 6 15 6 L12 6",
        ["citar"] = "M21 15 A2 2 0 0 1 19 17 L7 17 L3 21 L3 5 A2 2 0 0 1 5 3 L19 3 A2 2 0 0 1 21 5 Z M8 8 L8 11 M12 8 L12 11",
        ["responder"] = "M9 17 L4 12 L9 7 M20 18 L20 16 A4 4 0 0 0 16 12 L4 12",
        ["painel-geral"] ="M3 3 L10 3 L10 10 L3 10 Z M14 3 L21 3 L21 10 L14 10 Z M14 14 L21 14 L21 21 L14 21 Z M3 14 L10 14 L10 21 L3 21 Z",
        ["sino"] = "M10.27 21 A2 2 0 0 0 13.73 21 M3.26 15.33 A1 1 0 0 0 4 17 L20 17 A1 1 0 0 0 20.74 15.33 C19.41 13.96 18 12.5 18 8 A6 6 0 0 0 6 8 C6 12.5 4.59 13.96 3.26 15.33",
        ["olho"] = "M2.06 12.35 A1 1 0 0 1 2.06 11.65 A10.75 10.75 0 0 1 21.94 11.65 A1 1 0 0 1 21.94 12.35 A10.75 10.75 0 0 1 2.06 12.35 M15 12 A3 3 0 1 1 9 12 A3 3 0 1 1 15 12",
        ["cadeado"] = "M5 11 L19 11 A2 2 0 0 1 21 13 L21 20 A2 2 0 0 1 19 22 L5 22 A2 2 0 0 1 3 20 L3 13 A2 2 0 0 1 5 11 M7 11 L7 7 A5 5 0 0 1 17 7 L17 11",
        ["chave"] = "M15.5 7.5 L18.5 10.5 M19.5 2.5 L22 5 M21 2 L11.4 11.6 M12.5 16.5 A5.5 5.5 0 1 1 1.5 16.5 A5.5 5.5 0 1 1 12.5 16.5",
        ["olho-fechado"] = "M10.73 5.08 A10.43 10.43 0 0 1 12 5 C16.36 5 20.1 7.7 21.9 11.7 A1 1 0 0 1 21.9 12.3 A10.75 10.75 0 0 1 20.5 14.65 M14.08 14.16 A3 3 0 0 1 9.84 9.92 M17.48 17.5 A10.75 10.75 0 0 1 2.1 12.3 A1 1 0 0 1 2.1 11.7 A10.75 10.75 0 0 1 6.52 6.52 M2 2 L22 22",
        ["paleta"] = "M12 22 A10 10 0 1 1 22 12 C22 14.5 20 15 18.5 15 L16.5 15 A1.5 1.5 0 0 0 15.5 17.6 C16.3 18.5 15.6 22 12 22 Z M6.5 12.5 A1 1 0 1 0 8.5 12.5 A1 1 0 1 0 6.5 12.5 M8.5 7.5 A1 1 0 1 0 10.5 7.5 A1 1 0 1 0 8.5 7.5 M13.5 6.5 A1 1 0 1 0 15.5 6.5 A1 1 0 1 0 13.5 6.5 M16.5 10.5 A1 1 0 1 0 18.5 10.5 A1 1 0 1 0 16.5 10.5",
        ["alerta"] = "M2 12 A10 10 0 1 0 22 12 A10 10 0 1 0 2 12 M12 8 L12 12 M12 16 L12.01 16",
        ["lateral"] = "M5 3 L19 3 A2 2 0 0 1 21 5 L21 19 A2 2 0 0 1 19 21 L5 21 A2 2 0 0 1 3 19 L3 5 A2 2 0 0 1 5 3 Z M9 3 L9 21",
        ["executar"] = "M6 3 L20 12 L6 21 Z",
        ["celular"] = "M7 2 L17 2 A2 2 0 0 1 19 4 L19 20 A2 2 0 0 1 17 22 L7 22 A2 2 0 0 1 5 20 L5 4 A2 2 0 0 1 7 2 Z M12 18 L12.01 18",
        ["lixeira"] = "M3 6 L21 6 M8 6 L8 4 A2 2 0 0 1 10 2 L14 2 A2 2 0 0 1 16 4 L16 6 M19 6 L18 20 A2 2 0 0 1 16 22 L8 22 A2 2 0 0 1 6 20 L5 6 M10 11 L10 17 M14 11 L14 17",
        ["clipe"] = "M13.234 20.252 L21 12.3 M16 6 L7.586 14.586 A2 2 0 0 0 10.414 17.414 L18.828 9 A4 4 0 0 0 13.172 3.343 L4.757 11.757 A6 6 0 0 0 13.243 20.243",
        ["imagem"] = "M5 3 L19 3 A2 2 0 0 1 21 5 L21 19 A2 2 0 0 1 19 21 L5 21 A2 2 0 0 1 3 19 L3 5 A2 2 0 0 1 5 3 Z M7 9 A2 2 0 1 0 11 9 A2 2 0 1 0 7 9 M21 15 L17.914 11.914 A2 2 0 0 0 15.086 11.914 L6 21",
        ["orquestra"] = "M5 3 L9 3 A2 2 0 0 1 11 5 L11 9 A2 2 0 0 1 9 11 L5 11 A2 2 0 0 1 3 9 L3 5 A2 2 0 0 1 5 3 Z M7 11 L7 15 A2 2 0 0 0 9 17 L13 17 M15 13 L19 13 A2 2 0 0 1 21 15 L21 19 A2 2 0 0 1 19 21 L15 21 A2 2 0 0 1 13 19 L13 15 A2 2 0 0 1 15 13 Z",
        ["subagente"] = "M12 8 L12 4 L8 4 M6 8 L18 8 A2 2 0 0 1 20 10 L20 18 A2 2 0 0 1 18 20 L6 20 A2 2 0 0 1 4 18 L4 10 A2 2 0 0 1 6 8 Z M2 14 L4 14 M20 14 L22 14 M15 13 L15 15 M9 13 L9 15",
        ["copiar"] = "M10 8 L20 8 A2 2 0 0 1 22 10 L22 20 A2 2 0 0 1 20 22 L10 22 A2 2 0 0 1 8 20 L8 10 A2 2 0 0 1 10 8 Z M4 16 A2 2 0 0 1 2 14 L2 4 A2 2 0 0 1 4 2 L14 2 A2 2 0 0 1 16 4",
        ["mais-acoes"] = "M4 12 A1 1 0 1 0 6 12 A1 1 0 1 0 4 12 M11 12 A1 1 0 1 0 13 12 A1 1 0 1 0 11 12 M18 12 A1 1 0 1 0 20 12 A1 1 0 1 0 18 12",
    };

    static readonly Dictionary<string, Geometry> Cache = [];

    static Icone()
    {
        AffectsRender<Icone>(NomeProperty, ForegroundProperty);
        AffectsMeasure<Icone>(TamanhoProperty);
    }

    public string? Nome
    {
        get => GetValue(NomeProperty);
        set => SetValue(NomeProperty, value);
    }

    public double Tamanho
    {
        get => GetValue(TamanhoProperty);
        set => SetValue(TamanhoProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size disponivel) => new(Tamanho, Tamanho);

    public override void Render(DrawingContext contexto)
    {
        if (Nome is null || Foreground is null || Geometria(Nome) is not { } geometria) return;
        var escala = Tamanho / 24;
        var deslocamento = Matrix.CreateTranslation((Bounds.Width - Tamanho) / 2, (Bounds.Height - Tamanho) / 2);
        var pena = new Pen(Foreground, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (contexto.PushTransform(Matrix.CreateScale(escala, escala) * deslocamento))
            contexto.DrawGeometry(null, pena, geometria);
    }

    static Geometry? Geometria(string nome)
    {
        if (Cache.TryGetValue(nome, out var pronta)) return pronta;
        if (!Caminhos.TryGetValue(nome, out var caminho)) return null;
        var geometria = StreamGeometry.Parse(caminho);
        Cache[nome] = geometria;
        return geometria;
    }
}
