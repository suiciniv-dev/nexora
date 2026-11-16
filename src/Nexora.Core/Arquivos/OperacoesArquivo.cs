using Microsoft.VisualBasic.FileIO;

namespace Nexora.Core.Arquivos;

public static class OperacoesArquivo
{
    static readonly HashSet<string> Reservados = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string? NomeInvalido(string nome)
    {
        var limpo = nome.Trim();
        if (limpo.Length == 0) return "O nome não pode ficar vazio.";
        if (limpo.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "O nome tem caracteres que o Windows não aceita (\\ / : * ? \" < > |).";
        if (limpo.EndsWith('.') || Reservados.Contains(Path.GetFileNameWithoutExtension(limpo))) return "O Windows não aceita esse nome.";
        return null;
    }

    public static string Criar(string pasta, string nome, bool ehPasta)
    {
        if (NomeInvalido(nome) is { } erro) throw new IOException(erro);
        var caminho = Path.Combine(pasta, nome.Trim());
        if (File.Exists(caminho) || Directory.Exists(caminho)) throw new IOException($"Já existe {nome.Trim()} nesta pasta.");
        if (ehPasta) Directory.CreateDirectory(caminho);
        else File.WriteAllBytes(caminho, []);
        return caminho;
    }

    public static string Renomear(string caminho, string novoNome)
    {
        if (NomeInvalido(novoNome) is { } erro) throw new IOException(erro);
        var destino = Path.Combine(Path.GetDirectoryName(caminho)!, novoNome.Trim());
        if (string.Equals(destino, caminho, StringComparison.Ordinal)) return caminho;
        var soMaiusculas = string.Equals(destino, caminho, StringComparison.OrdinalIgnoreCase);
        if (!soMaiusculas && (File.Exists(destino) || Directory.Exists(destino))) throw new IOException($"Já existe {novoNome.Trim()} nesta pasta.");
        if (Directory.Exists(caminho))
        {
            if (soMaiusculas)
            {
                var meio = caminho + ".nexora-renomeando";
                Directory.Move(caminho, meio);
                Directory.Move(meio, destino);
            }
            else
                Directory.Move(caminho, destino);
        }
        else
            File.Move(caminho, destino);
        return destino;
    }

    public static void ParaLixeira(string caminho)
    {
        if (!OperatingSystem.IsWindows())
        {
            ParaLixeiraDoMac(caminho);
            return;
        }
        if (Directory.Exists(caminho)) FileSystem.DeleteDirectory(caminho, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        else if (File.Exists(caminho)) FileSystem.DeleteFile(caminho, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    static void ParaLixeiraDoMac(string caminho)
    {
        var lixeira = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
        Directory.CreateDirectory(lixeira);
        var nome = Path.GetFileName(caminho.TrimEnd('/'));
        var destino = Path.Combine(lixeira, nome);
        for (var i = 2; Directory.Exists(destino) || File.Exists(destino); i++)
            destino = Path.Combine(lixeira, $"{Path.GetFileNameWithoutExtension(nome)} {i}{Path.GetExtension(nome)}");
        if (Directory.Exists(caminho)) Directory.Move(caminho, destino);
        else if (File.Exists(caminho)) File.Move(caminho, destino);
    }
}
