namespace Template.LinuxApp.Views.Example;

using Avalonia;
using Avalonia.Threading;

using Template.LinuxApp.Components.Gamepad;

public sealed partial class GamepadButtonItem : ObservableObject
{
    public string Index { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial bool Pressed { get; set; }

    public GamepadButtonItem(int index, string name)
    {
        Index = index.ToString(CultureInfo.InvariantCulture);
        Name = name;
    }
}

public sealed partial class GamepadAxisItem : ObservableObject
{
    public string Index { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial double Value { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; } = "0";

    public GamepadAxisItem(int index, string name)
    {
        Index = index.ToString(CultureInfo.InvariantCulture);
        Name = name;
    }
}

public sealed partial class GamepadStickItem : ObservableObject
{
    public string Name { get; }

    public int AxisX { get; }

    public int AxisY { get; }

    [ObservableProperty]
    public partial Point Position { get; set; }

    [ObservableProperty]
    public partial string XText { get; set; } = "0.0%";

    [ObservableProperty]
    public partial string YText { get; set; } = "0.0%";

    [ObservableProperty]
    public partial string OffsetText { get; set; } = "0.0%";

    [ObservableProperty]
    public partial bool IsOutside { get; set; }

    [ObservableProperty]
    public partial string StateText { get; set; } = "Inside deadzone";

    public GamepadStickItem(string name, int axisX, int axisY)
    {
        Name = name;
        AxisX = axisX;
        AxisY = axisY;
    }

    public void Update(double x, double y)
    {
        var offset = Math.Sqrt((x * x) + (y * y));
        Position = new Point(x, y);
        XText = String.Create(CultureInfo.InvariantCulture, $"{x * 100:+0.0;-0.0;0.0}%");
        YText = String.Create(CultureInfo.InvariantCulture, $"{y * 100:+0.0;-0.0;0.0}%");
        OffsetText = String.Create(CultureInfo.InvariantCulture, $"{offset * 100:0.0}%");
        IsOutside = offset > GamepadViewModel.Deadzone;
        StateText = IsOutside ? "Outside deadzone" : "Inside deadzone";
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class GamepadViewModel : AppViewModelBase
{
    public const double Deadzone = 0.08;

    private const int ButtonCount = 16;

    private const int AxisCount = 8;

    private const int AxisLx = 0;

    private const int AxisLy = 1;

    private const int AxisRx = 3;

    private const int AxisRy = 4;

    private const int AxisDpadX = 6;

    private const int AxisDpadY = 7;

    private const double AxisScale = 32767;

    private const int MaxEvents = 14;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(1000d / 60);

    private static readonly string[] ButtonNames = ["A", "B", "X", "Y", "LB", "RB", "Back", "Start", "Guide", "LS", "RS"];

    private static readonly string[] AxisNames = ["LX", "LY", "LT", "RX", "RY", "RT", "D-pad X", "D-pad Y"];

    private readonly TimeProvider timeProvider;

    private readonly IDispatcher dispatcher;

    private readonly IGamepadReader gamepadReader;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    public IReadOnlyList<GamepadButtonItem> ButtonItems { get; }

    public IReadOnlyList<GamepadAxisItem> AxisItems { get; }

    public IReadOnlyList<GamepadStickItem> StickItems { get; } =
    [
        new("Left stick", AxisLx, AxisLy),
        new("Right stick", AxisRx, AxisRy)
    ];

    public ObservableCollection<string> Events { get; } = [];

    public string Device { get; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = String.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<bool>? Buttons { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double>? Axes { get; set; }

    public GamepadViewModel(TimeProvider timeProvider, IDispatcher dispatcher, IGamepadReader gamepadReader)
    {
        this.timeProvider = timeProvider;
        this.dispatcher = dispatcher;
        this.gamepadReader = gamepadReader;

        Device = String.IsNullOrEmpty(gamepadReader.Device) ? "Not configured" : gamepadReader.Device;
        ButtonItems = [.. Enumerable.Range(0, ButtonCount).Select(static x => new GamepadButtonItem(x, x < ButtonNames.Length ? ButtonNames[x] : String.Empty))];
        AxisItems = [.. Enumerable.Range(0, AxisCount).Select(static x => new GamepadAxisItem(x, AxisNames[x]))];

        Disposables.Add(Observable
            .FromEvent<EventHandler<GamepadConnectionEventArgs>, GamepadConnectionEventArgs>(static h => (_, e) => h(e), h => gamepadReader.ConnectionChanged += h, h => gamepadReader.ConnectionChanged -= h)
            .Subscribe(x => dispatcher.Post(() =>
            {
                UpdateConnection(x.Connected);
                AddEvent(x.Connected ? "Connected" : "Disconnected");
            })));

        UpdateConnection(gamepadReader.IsConnected);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        base.Dispose(disposing);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        cts = new CancellationTokenSource();
        var token = cts.Token;
        loopTask = Task.Run(() => LoopAsync(token), token);
        return Task.CompletedTask;
    }

    public override async Task OnNavigatingFromAsync(INavigationContext context)
    {
        if ((cts is null) || (loopTask is null))
        {
            return;
        }

        await cts.CancelAsync();
        try
        {
            await loopTask;
        }
        catch (OperationCanceledException)
        {
        }

        cts.Dispose();
        cts = null;
        loopTask = null;
    }

    private async Task LoopAsync(CancellationToken token)
    {
        var buttons = new bool[ButtonCount];
        var axes = new short[AxisCount];
        var first = true;
        using var timer = new PeriodicTimer(FrameInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            var nextButtons = new bool[ButtonCount];
            for (var i = 0; i < ButtonCount; i++)
            {
                nextButtons[i] = gamepadReader.GetButtonPressed((byte)i);
            }

            var nextAxes = new short[AxisCount];
            for (var i = 0; i < AxisCount; i++)
            {
                nextAxes[i] = gamepadReader.GetAxisValue((byte)i);
            }

            if (!first && nextButtons.AsSpan().SequenceEqual(buttons) && nextAxes.AsSpan().SequenceEqual(axes))
            {
                continue;
            }

            var previousButtons = buttons;
            var previousAxes = axes;
            var initial = first;
            buttons = nextButtons;
            axes = nextAxes;
            first = false;
            dispatcher.Post(() => Apply(nextButtons, nextAxes, initial ? null : previousButtons, initial ? null : previousAxes));
        }
    }

    private void Apply(bool[] buttons, short[] axes, bool[]? previousButtons, short[]? previousAxes)
    {
        for (var i = 0; i < ButtonCount; i++)
        {
            ButtonItems[i].Pressed = buttons[i];
            if ((previousButtons is not null) && (previousButtons[i] != buttons[i]))
            {
                AddEvent($"{GetButtonName(i)} {(buttons[i] ? "pressed" : "released")}");
            }
        }

        for (var i = 0; i < AxisCount; i++)
        {
            AxisItems[i].Value = axes[i] / AxisScale;
            AxisItems[i].Text = axes[i].ToString(CultureInfo.InvariantCulture);
        }

        foreach (var stick in StickItems)
        {
            stick.Update(axes[stick.AxisX] / AxisScale, axes[stick.AxisY] / AxisScale);
        }

        if (previousAxes is not null)
        {
            var direction = GetDpadDirection(axes);
            if ((direction != GetDpadDirection(previousAxes)) && (direction.Length > 0))
            {
                AddEvent($"D-pad {direction}");
            }
        }

        Buttons = buttons;
        Axes = [.. axes.Select(static x => x / AxisScale)];
    }

    private void UpdateConnection(bool connected)
    {
        IsConnected = connected;
        Status = connected ? $"{gamepadReader.Name}  {Device}" : $"Disconnected  {Device}";
    }

    private void AddEvent(string message)
    {
        Events.Insert(0, String.Create(CultureInfo.InvariantCulture, $"{timeProvider.GetLocalNow():HH:mm:ss.fff}  {message}"));
        while (Events.Count > MaxEvents)
        {
            Events.RemoveAt(Events.Count - 1);
        }
    }

    private static string GetButtonName(int index) =>
        index < ButtonNames.Length ? ButtonNames[index] : index.ToString(CultureInfo.InvariantCulture);

    private static string GetDpadDirection(short[] axes)
    {
        var vertical = axes[AxisDpadY] < 0 ? "Up" : axes[AxisDpadY] > 0 ? "Down" : String.Empty;
        var horizontal = axes[AxisDpadX] < 0 ? "Left" : axes[AxisDpadX] > 0 ? "Right" : String.Empty;
        return vertical.Length == 0 ? horizontal : horizontal.Length == 0 ? vertical : $"{vertical} {horizontal}";
    }
}
