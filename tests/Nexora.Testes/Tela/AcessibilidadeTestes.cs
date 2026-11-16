using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed partial class AcessibilidadeTestes
{
    sealed partial class Estado : ObservableObject
    {
        [ObservableProperty]
        public partial bool Ligado { get; set; }

        [RelayCommand]
        void Alternar() => Ligado = !Ligado;
    }

    [Fact]
    public Task BotaoLigadoSegueOEstadoEAvisaOLeitorDeTela() => Sessao.Rodar(() =>
    {
        var estado = new Estado();
        var botao = new BotaoLigado { Content = "Terminal", DataContext = estado };
        botao.Classes.Add("icone");
        botao.Bind(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, new Avalonia.Data.Binding(nameof(Estado.Ligado)) { Mode = Avalonia.Data.BindingMode.OneWay });
        botao.Bind(Button.CommandProperty, new Avalonia.Data.Binding(nameof(Estado.AlternarCommand)));
        var janela = new Window { Width = 300, Height = 200, Content = botao };
        janela.Show();

        var alternavel = Assert.IsAssignableFrom<IToggleProvider>(ControlAutomationPeer.CreatePeerForElement(botao));
        Assert.Equal(Avalonia.Automation.Provider.ToggleState.Off, alternavel.ToggleState);

        janela.MouseDown(new Avalonia.Point(botao.Bounds.Center.X, botao.Bounds.Center.Y), MouseButton.Left);
        janela.MouseUp(new Avalonia.Point(botao.Bounds.Center.X, botao.Bounds.Center.Y), MouseButton.Left);
        Assert.True(estado.Ligado);
        Assert.Equal(Avalonia.Automation.Provider.ToggleState.On, alternavel.ToggleState);

        estado.Ligado = false;
        Assert.Equal(false, botao.IsChecked);
        Assert.Equal(8, botao.Padding.Left);
    });

    [Fact]
    public Task CamposChavesEBotoesCompostosGanhamNomeParaOLeitorDeTela() => Sessao.Rodar(() =>
    {
        var chave = new ToggleSwitch { OnContent = "", OffContent = "" };
        var campo = new TextBox { PlaceholderText = "Procurar branch" };
        var botao = new Button { Content = new StackPanel { Children = { new Icone { Nome = "mais" }, new TextBlock { Text = "Novo projeto" } } } };
        var linha = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var rotulo = new StackPanel { Children = { new TextBlock { Text = "Hibernar sessões paradas" }, new TextBlock { Text = "Explicação longa" } } };
        Grid.SetColumn(chave, 1);
        linha.Children.Add(rotulo);
        linha.Children.Add(chave);
        var janela = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { linha, campo, botao } } };
        janela.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("Hibernar sessões paradas", Avalonia.Automation.AutomationProperties.GetName(chave));
        Assert.Equal("Procurar branch", Avalonia.Automation.AutomationProperties.GetName(campo));
        Assert.Equal("Novo projeto", Avalonia.Automation.AutomationProperties.GetName(botao));
    });
}
