namespace Nexora.Core.Orquestra;

public interface IPonteMcp
{
    void Apontar(string servidor, int porta, string segredo);
    RegistroNaPonte? Registrar(string servidor, string dono);
    void Esquecer(string chave);
}
