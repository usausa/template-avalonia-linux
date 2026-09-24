namespace Template.LinuxApp;

// ReSharper disable once ClassNeverInstantiated.Global
[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public sealed class MainWindowViewModel : ExtendViewModelBase
{
    public INavigator Navigator { get; }

    public MainWindowViewModel(INavigator navigator)
    {
        Navigator = navigator;
    }
}
