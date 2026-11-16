using Nexora.Core.Recursos;
using Nexora.ViewModels;

namespace Nexora.Testes;

public sealed class ServidoresDeBuildTestes
{
    [Fact]
    public void ReconheceOsProcessosDeBuildPelaLinhaDeComando()
    {
        Assert.Equal("Nó do MSBuild", ServidoresDeBuild.Classificar("dotnet",
            "\"C:\\Program Files\\dotnet\\dotnet.exe\" \"C:\\Program Files\\dotnet\\sdk\\10.0.302\\MSBuild.dll\" /noautoresponse /nologo /nodemode:1 /nodeReuse:true /low:false"));
        Assert.Equal("Compilador do C#", ServidoresDeBuild.Classificar("VBCSCompiler", null));
        Assert.Equal("Compilador do C#", ServidoresDeBuild.Classificar("dotnet", "dotnet exec \"C:\\sdk\\Roslyn\\bincore\\VBCSCompiler.dll\" -pipename:abc"));
        Assert.Equal("Servidor do Razor", ServidoresDeBuild.Classificar("dotnet", "dotnet exec \"C:\\sdk\\Sdks\\Microsoft.NET.Sdk.Razor\\tools\\rzc.dll\" server -p abc"));
        Assert.Null(ServidoresDeBuild.Classificar("dotnet", "\"C:\\Program Files\\dotnet\\dotnet.exe\" run --project src/Acme.Shop.Blazor/Acme.Shop.Blazor.csproj"));
        Assert.Null(ServidoresDeBuild.Classificar("dotnet", "\"C:\\Program Files\\dotnet\\dotnet.exe\" D:\\NexoraApp\\Nexora.dll"));
        Assert.Null(ServidoresDeBuild.Classificar("dotnet", null));
    }

    [Fact]
    public void DescreveOsProcessosPorTipo()
    {
        var texto = RecursosVM.DescreverBuild(
        [
            new(1, "Nó do MSBuild", 300L << 20),
            new(2, "Nó do MSBuild", 200L << 20),
            new(3, "Compilador do C#", 524L << 20),
        ]);
        Assert.StartsWith("3 processos ocupando 1", texto);
        Assert.EndsWith(" GB: 2 × Nó do MSBuild, Compilador do C#.", texto);
        Assert.Equal("Nenhum processo de build aberto.", RecursosVM.DescreverBuild([]));
    }
}
