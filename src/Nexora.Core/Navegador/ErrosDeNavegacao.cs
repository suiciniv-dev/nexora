using Nexora.Core.Texto;

namespace Nexora.Core.Navegador;

public sealed record FalhaDeNavegacao(string Titulo, string Dica, bool ServidorParado);

public static class ErrosDeNavegacao
{
    public static FalhaDeNavegacao Descrever(string status, string url, int codigoHttp = 0)
    {
        var local = Uri.TryCreate(url, UriKind.Absolute, out var uri) && Enderecos.EhLocal(uri);
        return status switch
        {
            "ConnectionAborted" or "ConnectionReset" or "Disconnected" or "CannotConnect" when local => new(
                Idioma.F("Nada respondendo em {0}", Enderecos.Curto(url)),
                Idioma.T("O servidor de desenvolvimento não está no ar nesta porta. Suba ele no painel Executar ou confira a porta."),
                true),
            "ConnectionAborted" or "ConnectionReset" or "Disconnected" or "CannotConnect" => new(
                Idioma.F("Não deu para conectar em {0}", Enderecos.Curto(url)),
                Idioma.T("O site não respondeu. Confira a internet e o endereço."),
                false),
            "HostNameNotResolved" => new(
                Idioma.F("Endereço não encontrado: {0}", uri?.Host ?? url),
                Idioma.T("O nome do site não existe ou a internet está fora. Confira o endereço."),
                false),
            "Timeout" => new(
                Idioma.F("{0} demorou demais para responder", Enderecos.Curto(url)),
                Idioma.T("Tente recarregar. Se for um servidor local, veja se ele travou no painel Executar."),
                local),
            "CertificateCommonNameIsIncorrect" or "CertificateExpired" or "ClientCertificateContainsErrors" or "CertificateRevoked" or "CertificateIsInvalid" => new(
                Idioma.T("Certificado HTTPS inválido"),
                local
                    ? Idioma.T("O certificado de desenvolvimento não é confiável. Rode \"dotnet dev-certs https --trust\" ou abra a versão http da porta.")
                    : Idioma.T("O certificado do site não é confiável, então a página não foi aberta."),
                false),
            "ServerUnreachable" => new(
                Idioma.F("Servidor inacessível: {0}", Enderecos.Curto(url)),
                Idioma.T("Confira a rede, a VPN ou o proxy."),
                local),
            "ErrorHttpInvalidServerResponse" => new(
                Idioma.T("O servidor mandou uma resposta inválida"),
                Idioma.T("Pode ser http numa porta https (ou o contrário). Tente trocar o começo do endereço."),
                false),
            "OperationCanceled" or "ValidAuthenticationCredentialsRequired" or "ValidProxyAuthenticationRequired" => new(
                Idioma.T("A página não abriu"),
                Idioma.T("A navegação foi cancelada ou pede login."),
                false),
            _ when codigoHttp >= 400 => new(
                Idioma.F("A página respondeu com erro {0}", codigoHttp),
                codigoHttp == 404 ? Idioma.T("Esse caminho não existe no servidor.") : Idioma.T("O servidor recebeu o pedido, mas devolveu um erro. Veja a saída no painel Executar."),
                false),
            _ => new(
                Idioma.T("A página não abriu"),
                Idioma.F("Motivo informado pelo WebView: {0}.", status),
                false),
        };
    }
}
