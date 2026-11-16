using SkiaSharp;

namespace Nexora.Fundos;

sealed record Paleta(
    bool Escuro,
    SKColor Fundo,
    SKColor FundoFim,
    SKColor Luz,
    SKColor Sombra,
    SKColor Vinheta,
    SKColor Linha,
    SKColor No,
    SKColor Anel,
    SKColor Destaque)
{
    public static SKColor C(string hex, float alfa = 1) => SKColor.Parse(hex).WithAlpha((byte)Math.Round(alfa * 255));

    public static readonly Paleta Grafo = new(true,
        C("#0E0D0C"), C("#0A090B"), C("#948BAE", 0.22f), C("#5B5470", 0.28f), C("#000000", 0.55f),
        C("#948BAE"), C("#D9D4E6"), C("#F2F0EC"), C("#BDB4DA"));

    public static readonly Paleta GrafoClaro = new(false,
        C("#F6F4F1"), C("#ECE9F1"), C("#948BAE", 0.26f), C("#BDB4DA", 0.40f), C("#3A3548", 0.10f),
        C("#5B5470"), C("#4A4460"), C("#3A3548"), C("#7A6FA0"));

    public static readonly Paleta Constelacao = new(true,
        C("#0E1024"), C("#06060E"), C("#8F96D8", 0.20f), C("#5B5470", 0.30f), C("#000000", 0.50f),
        C("#A9B6F2"), C("#E4E6FF"), C("#F4F2FF"), C("#A9B6F2"));

    public static readonly Paleta CartaCeleste = new(false,
        C("#F6F3EA"), C("#EDE8DC"), C("#A9B6F2", 0.24f), C("#BDB4DA", 0.32f), C("#3A3548", 0.12f),
        C("#4A4E86"), C("#2A2E52"), C("#2A2E52"), C("#4A4E86"));

    public static readonly Paleta ViaLactea = new(true,
        C("#0C0B13"), C("#07070B"), C("#948BAE", 0.18f), C("#5B5470", 0.26f), C("#000000", 0.55f),
        C("#BDB4DA"), C("#EEE8F6"), C("#F2F0EC"), C("#D9C7F0"));

    public static readonly Paleta Nebulosa = new(true,
        C("#0E0B12"), C("#08070B"), C("#E59AC4", 0.12f), C("#5B5470", 0.30f), C("#000000", 0.55f),
        C("#948BAE"), C("#F0E6F2"), C("#F2F0EC"), C("#E59AC4"));

    public static readonly Paleta Pulso = new(true,
        C("#0B0D12"), C("#08090C"), C("#948BAE", 0.20f), C("#86C5E6", 0.08f), C("#000000", 0.55f),
        C("#948BAE"), C("#E0ECF4"), C("#F2F0EC"), C("#86C5E6"));

    public static readonly Paleta Aurora = new(true,
        C("#0A0F14"), C("#07090B"), C("#948BAE", 0.14f), C("#5B5470", 0.24f), C("#000000", 0.50f),
        C("#9FB8C8"), C("#E6F2EE"), C("#F2F0EC"), C("#7BCB9F"));

    public static readonly Paleta Mosaico = new(true,
        C("#13110E"), C("#0B0A09"), C("#E0A048", 0.10f), C("#2C2822", 0.62f), C("#000000", 0.45f),
        C("#948BAE"), C("#F2E6D2"), C("#FFD592"), C("#E0A048"));
}
