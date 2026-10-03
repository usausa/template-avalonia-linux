namespace Template.LinuxApp;

using System.Reactive.Concurrency;

using Smart.Mvvm.ViewModels;

using Template.LinuxApp.Devices.Input;
using Template.LinuxApp.Services;
using Template.LinuxApp.Shell;
using Template.LinuxApp.Views;

// ReSharper disable once ClassNeverInstantiated.Global
[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public sealed class MainWindowViewModel : ExtendViewModelBase
{
    private readonly IDialogService dialogService;

    private IDisposable? navigatingBusy;

    public INavigator Navigator { get; }

    public IReadOnlyList<NavigationItem> Items { get; } =
    [
        new(ViewId.Dashboard, "Dashboard"),
        new(ViewId.Performance, "Performance"),
        new(ViewId.System, "System"),
        new(ViewId.Camera, "Camera"),
        new(ViewId.Barcode, "Barcode"),
        new(ViewId.Nfc, "NFC"),
        new(ViewId.Printer, "Printer"),
        new(ViewId.Controller, "Controller"),
        new(ViewId.Gamepad, "Gamepad"),
        new(ViewId.Motor, "Motor"),
        new(ViewId.Typography, "Typography"),
        new(ViewId.Graphics, "Graphics")
    ];

    public ICommand ForwardCommand { get; }

    public MainWindowViewModel(INavigator navigator, IDialogService dialogService, IInputDevice input)
    {
        Navigator = navigator;
        this.dialogService = dialogService;

        ForwardCommand = MakeAsyncCommand<ViewId>(x => Navigator.ForwardAsync(x));

        // Busy while navigating
        Disposables.Add(Observable.FromEventPattern<EventArgs>(h => Navigator.ExecutingChanged += h, h => Navigator.ExecutingChanged -= h)
            .Subscribe(_ => UpdateNavigatingBusy()));

        Disposables.Add(Observable
            .FromEventPattern<NavigationEventArgs>(h => navigator.Navigated += h, h => navigator.Navigated -= h)
            .Subscribe(x => UpdateSelection(x.EventArgs.Context.ToId as ViewId?)));

        var scheduler = new SynchronizationContextScheduler(SynchronizationContext.Current!);
        Disposables.Add(Observable
            .FromEvent<EventHandler<EventArgs<InputSignal>>, EventArgs<InputSignal>>(static h => (_, e) => h(e), h => input.Handle += h, h => input.Handle -= h)
            .ObserveOn(scheduler)
            .Select(x => Observable.FromAsync(() => HandleInputAsync(x.Data), scheduler))
            .Concat()
            .Subscribe());
    }

    private void UpdateNavigatingBusy()
    {
        if (Navigator.Executing)
        {
            navigatingBusy ??= BusyState.Begin();
        }
        else
        {
            navigatingBusy?.Dispose();
            navigatingBusy = null;
        }
    }

    private void UpdateSelection(ViewId? id)
    {
        foreach (var item in Items)
        {
            item.IsSelected = item.Id == id;
        }
    }

    private Task HandleInputAsync(InputSignal signal) => dialogService.IsOpen ? Task.CompletedTask : signal switch
    {
        { Key: InputKey.Select, Action: InputAction.Press } => SwitchViewAsync(),
        { Key: InputKey.Start, Action: InputAction.Press } => Navigator.NotifyAsync(ShellEvent.Start),
        _ => Task.CompletedTask
    };

    private Task<bool> SwitchViewAsync()
    {
        var index = -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (Equals(Navigator.CurrentViewId, Items[i].Id))
            {
                index = i;
                break;
            }
        }

        return Navigator.ForwardAsync(Items[(index + 1) % Items.Count].Id);
    }
}
