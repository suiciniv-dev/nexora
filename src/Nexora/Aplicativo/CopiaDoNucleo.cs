namespace Nexora.Aplicativo;

public static class CopiaDoNucleo
{
    const string Marca = "Nexora.dll";
    const string Parcial = ".parcial-";
    static readonly TimeSpan IdadeDaParcial = TimeSpan.FromMinutes(10);

    public static string Raiz => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexora", "nucleo");

    public static string Preparar(string origem, string raiz, Guid versao)
    {
        var destino = Path.Combine(raiz, versao.ToString("N"));
        if (File.Exists(Path.Combine(destino, Marca))) return destino;
        Directory.CreateDirectory(raiz);
        var temporaria = destino + Parcial + Guid.NewGuid().ToString("N")[..8];
        try
        {
            Copiar(new DirectoryInfo(origem), temporaria);
            Directory.Move(temporaria, destino);
        }
        catch (IOException) when (File.Exists(Path.Combine(destino, Marca)))
        {
        }
        finally
        {
            Apagar(temporaria);
        }
        return destino;
    }

    public static void Limpar(string raiz, string manter)
    {
        if (!Directory.Exists(raiz)) return;
        foreach (var pasta in new DirectoryInfo(raiz).EnumerateDirectories())
        {
            if (string.Equals(pasta.FullName.TrimEnd('\\', '/'), manter.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;
            var parcial = pasta.Name.Contains(Parcial, StringComparison.Ordinal);
            if (parcial ? DateTime.UtcNow - pasta.LastWriteTimeUtc < IdadeDaParcial : EmUso(pasta.FullName)) continue;
            Apagar(pasta.FullName);
        }
    }

    public static bool EmUso(string pasta)
    {
        var marca = Path.Combine(pasta, Marca);
        if (!File.Exists(marca)) return false;
        try
        {
            using var _ = new FileStream(marca, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    static void Copiar(DirectoryInfo origem, string destino)
    {
        Directory.CreateDirectory(destino);
        foreach (var arquivo in origem.EnumerateFiles()) arquivo.CopyTo(Path.Combine(destino, arquivo.Name), true);
        foreach (var pasta in origem.EnumerateDirectories()) Copiar(pasta, Path.Combine(destino, pasta.Name));
    }

    static void Apagar(string pasta)
    {
        try
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
