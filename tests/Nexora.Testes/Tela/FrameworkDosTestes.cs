using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Nexora.Testes.Tela.FrameworkDosTestes", "Nexora.Testes")]

namespace Nexora.Testes.Tela;

public sealed class FrameworkDosTestes : XunitTestFramework
{
    public FrameworkDosTestes(IMessageSink mensagens) : base(mensagens) => Sessao.Rodar(() => { }).GetAwaiter().GetResult();
}
