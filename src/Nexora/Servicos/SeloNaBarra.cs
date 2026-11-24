using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Nexora.Servicos;

public static class SeloNaBarra
{
    public const int Lado = 32;

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    class TaskbarList { }

    [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList3
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool telaCheia);
        void SetProgressValue(IntPtr hwnd, ulong feito, ulong total);
        void SetProgressState(IntPtr hwnd, int estado);
        void RegisterTab(IntPtr aba, IntPtr mdi);
        void UnregisterTab(IntPtr aba);
        void SetTabOrder(IntPtr aba, IntPtr antes);
        void SetTabActive(IntPtr aba, IntPtr mdi, uint reservado);
        void ThumbBarAddButtons(IntPtr hwnd, uint quantos, IntPtr botoes);
        void ThumbBarUpdateButtons(IntPtr hwnd, uint quantos, IntPtr botoes);
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr lista);
        void SetOverlayIcon(IntPtr hwnd, IntPtr icone, [MarshalAs(UnmanagedType.LPWStr)] string? descricao);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("user32.dll")] static extern IntPtr CreateIconIndirect(ref ICONINFO info);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icone);
    [DllImport("gdi32.dll")] static extern IntPtr CreateBitmap(int largura, int altura, uint planos, uint bits, byte[] dados);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER info, uint uso, out IntPtr bits, IntPtr secao, uint deslocamento);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr objeto);

    static ITaskbarList3? _barra;
    static bool _semBarra;
    static IntPtr _icone;
    static (IntPtr Janela, int Total, TipoDoSelo Tipo)? _atual;

    public static string Texto(int total) => total > 9 ? "9+" : total.ToString(Core.Texto.Idioma.Cultura);

    public static Color Cor(TipoDoSelo tipo) => tipo switch
    {
        TipoDoSelo.Erro => Color.Parse("#E8806C"),
        TipoDoSelo.Precisa => Color.Parse("#F0A860"),
        _ => Color.Parse("#7BCB9F"),
    };

    public static void Mostrar(IntPtr janela, int total, TipoDoSelo tipo, string? descricao)
    {
        if (!OperatingSystem.IsWindows() || janela == IntPtr.Zero || _semBarra || !Sistema.AppDeVerdade) return;
        var chave = (janela, total, total == 0 ? TipoDoSelo.Pronta : tipo);
        if (_atual == chave) return;
        try
        {
            if (_barra is null)
            {
                _barra = (ITaskbarList3)new TaskbarList();
                _barra.HrInit();
            }
            var novo = total == 0 ? IntPtr.Zero : Icone(Desenhar(total, tipo));
            _barra.SetOverlayIcon(janela, novo, descricao);
            if (_icone != IntPtr.Zero) DestroyIcon(_icone);
            _icone = novo;
            _atual = chave;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or EntryPointNotFoundException or DllNotFoundException)
        {
            _semBarra = true;
        }
    }

    public static byte[] Desenhar(int total, TipoDoSelo tipo)
    {
        using var imagem = new RenderTargetBitmap(new PixelSize(Lado, Lado), new Vector(96, 96));
        using (var contexto = imagem.CreateDrawingContext())
        using (contexto.PushRenderOptions(new RenderOptions { TextRenderingMode = TextRenderingMode.Antialias }))
        {
            var centro = new Point(Lado / 2.0, Lado / 2.0);
            contexto.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x1B, 0x1A, 0x22)), null, centro, Lado / 2.0, Lado / 2.0);
            contexto.DrawEllipse(new SolidColorBrush(Cor(tipo)), null, centro, Lado / 2.0 - 2.5, Lado / 2.0 - 2.5);
            var texto = Texto(total);
            var formatado = new FormattedText(texto, Core.Texto.Idioma.Cultura, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), texto.Length > 1 ? 14 : 19, new SolidColorBrush(Color.FromRgb(0x1B, 0x1A, 0x22)));
            contexto.DrawText(formatado, new Point(centro.X - formatado.Width / 2, centro.Y - formatado.Height / 2));
        }
        var passo = Lado * 4;
        var dados = new byte[passo * Lado];
        var alca = GCHandle.Alloc(dados, GCHandleType.Pinned);
        try
        {
            imagem.CopyPixels(new PixelRect(0, 0, Lado, Lado), alca.AddrOfPinnedObject(), dados.Length, passo);
        }
        finally
        {
            alca.Free();
        }
        for (var i = 0; i < dados.Length; i += 4)
        {
            var alfa = dados[i + 3];
            if (alfa is 0 or 255) continue;
            dados[i] = (byte)Math.Min(255, dados[i] * 255 / alfa);
            dados[i + 1] = (byte)Math.Min(255, dados[i + 1] * 255 / alfa);
            dados[i + 2] = (byte)Math.Min(255, dados[i + 2] * 255 / alfa);
        }
        return dados;
    }

    static IntPtr Icone(byte[] bgra)
    {
        var cabecalho = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = Lado,
            biHeight = -Lado,
            biPlanes = 1,
            biBitCount = 32,
        };
        var cor = CreateDIBSection(IntPtr.Zero, ref cabecalho, 0, out var bits, IntPtr.Zero, 0);
        if (cor == IntPtr.Zero) return IntPtr.Zero;
        var mascara = CreateBitmap(Lado, Lado, 1, 1, new byte[Lado * Lado / 8]);
        try
        {
            Marshal.Copy(bgra, 0, bits, bgra.Length);
            var info = new ICONINFO { fIcon = true, hbmMask = mascara, hbmColor = cor };
            return CreateIconIndirect(ref info);
        }
        finally
        {
            DeleteObject(cor);
            DeleteObject(mascara);
        }
    }
}
