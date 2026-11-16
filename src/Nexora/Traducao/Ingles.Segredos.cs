namespace Nexora.Traducao;

public static partial class Ingles
{
    static readonly (string Pt, string En)[] Segredos =
    [
        ("Valor do segredo", "Secret value"),
        ("Mostrar ou esconder o valor", "Show or hide the value"),
        ("Recusar", "Decline"),
        ("Recusado", "Declined"),
        ("Responda pela conversa completa: o campo do segredo fica só lá.", "Answer in the full conversation: the secret field is only there."),
        ("Responder na conversa", "Answer in the conversation"),
        ("Abre a conversa no formulário do segredo", "Opens the conversation at the secret form"),
        ("Apagar o segredo", "Delete the secret"),
        ("Segredos", "Secrets"),
        ("Nenhum segredo guardado ainda. Quando um agente usar pedir_segredo e você salvar, ele aparece aqui.", "No secrets saved yet. When an agent uses pedir_segredo and you save it, it shows up here."),
        ("O Remote Control desligou porque a sessão reiniciou para receber o segredo. Ligue de novo se precisar.", "Remote Control turned off because the session restarted to receive the secret. Turn it on again if you need it."),
        ("Os segredos que os agentes pediram pelo formulário do Nexora. O valor nunca aparece aqui: as variáveis ficam no {0} e os .env ficam no projeto.", "The secrets agents asked for through the Nexora form. The value never shows here: variables stay in the {0} and .env files stay in the project."),
        ("Porta-chaves", "Keychain"),
        ("cofre do Windows (DPAPI)", "Windows vault (DPAPI)"),
        ("Os segredos que os agentes pediram pelo formulário do Nexora. O valor nunca aparece aqui.", "The secrets agents asked for through the Nexora form. The value never shows here."),
        ("Não deu para apagar {0}: {1}", "Couldn't delete {0}: {1}"),
        ("O agente pede um segredo: {0}", "The agent asks for a secret: {0}"),
        ("Vai para {0} como {1}. O arquivo está no .gitignore.", "Goes to {0} as {1}. The file is in .gitignore."),
        ("Fica guardado pelo Nexora no cofre do sistema e entra como a variável {0} nos processos deste projeto.", "Nexora keeps it in the system vault and passes it as the {0} variable to this project's processes."),
        ("O valor não entra na conversa nem no histórico. O agente recebe só a confirmação.", "The value never enters the conversation or the history. The agent only gets a confirmation."),
        ("Guardado em {0} como {1}", "Saved to {0} as {1}"),
        ("Guardado pelo Nexora como {0}", "Saved by Nexora as {0}"),
        ("Arquivo {0}", "File {0}"),
        ("Variável de ambiente (cofre do sistema)", "Environment variable (system vault)"),
        ("Tira a linha {0} do {1}", "Removes the {0} line from {1}"),
        ("Apaga o valor do cofre do sistema", "Deletes the value from the system vault"),
        ("Este pedido já foi respondido.", "This request was already answered."),
        ("Digite o valor ou recuse o pedido.", "Type the value or decline the request."),
        ("Para o resumo automático das notas chamar a API da OpenAI nos testes de integração.", "So the automatic note summary can call the OpenAI API in the integration tests."),
    ];
}
