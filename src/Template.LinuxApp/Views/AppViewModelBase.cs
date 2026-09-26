namespace Template.LinuxApp.Views;

using Template.LinuxApp.Shell;

[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public abstract class AppViewModelBase : ExtendViewModelBase, INavigatorAware, INavigationEventSupportAsync, INotifySupportAsync<ShellEvent>
{
    public INavigator Navigator { get; set; } = default!;

    public virtual Task OnNavigatingFromAsync(INavigationContext context) => Task.CompletedTask;

    public virtual Task OnNavigatingToAsync(INavigationContext context) => Task.CompletedTask;

    public virtual Task OnNavigatedToAsync(INavigationContext context) => Task.CompletedTask;

    public Task NavigatorNotifyAsync(ShellEvent parameter) => parameter switch
    {
        ShellEvent.Start => OnShellStartAsync(),
        _ => Task.CompletedTask
    };

    protected virtual Task OnShellStartAsync() => Task.CompletedTask;
}
