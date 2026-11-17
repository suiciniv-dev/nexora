using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Nexora.Servicos;

public sealed record VersaoNova(Version Versao, string Tag, string Notas, IReadOnlyDictionary<string, string> Arquivos)
{
    public string Rotulo => Versao.ToString(3);
}
