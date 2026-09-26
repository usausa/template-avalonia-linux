namespace Template.LinuxApp.Views.Example;

using Smart.Reactive;

using Template.LinuxApp.Components.Barcode;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class BarcodeViewModel : AppViewModelBase
{
    private readonly IBarcodeReader barcodeReader;

    private readonly IQrReader qrReader;

    [ObservableProperty]
    public partial string Barcode { get; set; } = string.Empty;

    public ICommand ResumeCommand { get; }

    public ICommand PauseCommand { get; }

    public BarcodeViewModel(IBarcodeReader barcodeReader, IQrReader qrReader)
    {
        this.barcodeReader = barcodeReader;
        this.qrReader = qrReader;

        Disposables.Add(Observable
            .FromEventPattern<BarcodeEventArgs>(h => barcodeReader.Scanned += h, h => barcodeReader.Scanned -= h)
            .Merge(Observable.FromEventPattern<BarcodeEventArgs>(h => qrReader.Scanned += h, h => qrReader.Scanned -= h))
            .ObserveOnCurrentContext()
            .Subscribe(x => Barcode = x.EventArgs.Code));

        ResumeCommand = MakeDelegateCommand(qrReader.ResumeReading);
        PauseCommand = MakeDelegateCommand(qrReader.PauseReading);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        barcodeReader.Start();
        qrReader.Start();
        return Task.CompletedTask;
    }

    public override async Task OnNavigatingFromAsync(INavigationContext context)
    {
        await barcodeReader.StopAsync();
        await qrReader.StopAsync();
    }
}
