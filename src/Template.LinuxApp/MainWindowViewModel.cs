namespace Template.LinuxApp;

using System.Reactive.Concurrency;

using Template.LinuxApp.Devices.Input;
using Template.LinuxApp.Services;
using Template.LinuxApp.Shell;
using Template.LinuxApp.Views;

// ReSharper disable once ClassNeverInstantiated.Global
[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public sealed class MainWindowViewModel : ExtendViewModelBase
{
    private static readonly ViewId[] Views =
    [
        ViewId.Dashboard,
        ViewId.Typography,
        ViewId.Barcode,
        ViewId.Camera,
        ViewId.Printer,
        ViewId.Controller,
        ViewId.Nfc
    ];

    private readonly IDialogService dialogService;

    public INavigator Navigator { get; }

    public ICommand ForwardCommand { get; }

    public MainWindowViewModel(INavigator navigator, IDialogService dialogService, IInputDevice input)
    {
        Navigator = navigator;
        this.dialogService = dialogService;

        ForwardCommand = MakeAsyncCommand<ViewId>(x => Navigator.ForwardAsync(x));

        var scheduler = new SynchronizationContextScheduler(SynchronizationContext.Current!);
        Disposables.Add(Observable
            .FromEvent<EventHandler<EventArgs<InputSignal>>, EventArgs<InputSignal>>(static h => (_, e) => h(e), h => input.Handle += h, h => input.Handle -= h)
            .ObserveOn(scheduler)
            .Select(x => Observable.FromAsync(() => HandleInputAsync(x.Data), scheduler))
            .Concat()
            .Subscribe());
    }

    private Task HandleInputAsync(InputSignal signal) => dialogService.IsOpen ? Task.CompletedTask : signal switch
    {
        { Key: InputKey.Previous, Action: InputAction.Press } => SwitchViewAsync(-1),
        { Key: InputKey.Next, Action: InputAction.Press } => SwitchViewAsync(1),
        { Key: InputKey.Start, Action: InputAction.Press } => Navigator.NotifyAsync(ShellEvent.Start),
        _ => Task.CompletedTask
    };

    private Task<bool> SwitchViewAsync(int offset)
    {
        var index = Navigator.CurrentViewId is ViewId current ? Array.IndexOf(Views, current) : -1;
        index = index < 0 ? 0 : (index + offset + Views.Length) % Views.Length;
        return Navigator.ForwardAsync(Views[index]);
    }
}
