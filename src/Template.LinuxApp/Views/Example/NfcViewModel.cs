namespace Template.LinuxApp.Views.Example;

using Smart.Reactive;

using Template.LinuxApp.Components.Nfc;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class NfcViewModel : AppViewModelBase
{
    private readonly ISuicaReader suicaReader;

    [ObservableProperty]
    public partial string Id { get; set; } = "-";

    [ObservableProperty]
    public partial int Balance { get; set; }

    public NfcViewModel(ISuicaReader suicaReader)
    {
        this.suicaReader = suicaReader;

        Disposables.Add(Observable
            .FromEventPattern<SuicaReadEventArgs>(h => suicaReader.CardRead += h, h => suicaReader.CardRead -= h)
            .ObserveOnCurrentContext()
            .Subscribe(x =>
            {
                Id = x.EventArgs.Idm;
                Balance = x.EventArgs.Balance;
            }));
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        suicaReader.Start();
        return Task.CompletedTask;
    }

    public override async Task OnNavigatingFromAsync(INavigationContext context)
    {
        await suicaReader.StopAsync();
    }
}
