namespace Template.LinuxApp.Views.Main;

using Template.LinuxApp.Services;
using Template.LinuxApp.Settings;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class MenuViewModel : AppViewModelBase
{
    [ObservableProperty]
    public partial string Message { get; set; }

    public ICommand NavigateCommand { get; }

    public ICommand ThemeCommand { get; }

    public MenuViewModel(Setting setting, ThemeService themeService)
    {
        Message = $"Hello from MenuViewModel! setting=[{setting.Value}]";
        NavigateCommand = MakeDelegateCommand(() => Navigator.Forward(ViewId.Sub));
        ThemeCommand = MakeAsyncCommand<string>(x => themeService.ChangeAsync(x).AsTask());
    }
}
