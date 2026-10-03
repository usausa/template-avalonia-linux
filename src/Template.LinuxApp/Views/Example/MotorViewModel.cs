namespace Template.LinuxApp.Views.Example;

using Avalonia.Media;
using Avalonia.Threading;

using Template.LinuxApp.Components.Gamepad;
using Template.LinuxApp.Components.Motor;
using Template.LinuxApp.Settings;
using Template.LinuxApp.State;

public sealed partial class MotorPoseItem : ObservableObject
{
    public string Name { get; }

    [ObservableProperty]
    public partial int Servo1 { get; set; }

    [ObservableProperty]
    public partial int Servo2 { get; set; }

    [ObservableProperty]
    public partial Color Color { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public MotorPoseItem(string name, int servo1, int servo2, Color color)
    {
        Name = name;
        Servo1 = servo1;
        Servo2 = servo2;
        Color = color;
    }
}

public sealed record LedPreset(string Name, Color Color);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class MotorViewModel : AppViewModelBase
{
    private const int NeutralAngle = 90;

    private const int MinimumAngle = 0;

    private const int MaximumAngle = 180;

    private const int MaxLogs = 20;

    private const byte AxisServo1 = 0;

    private const byte AxisServo2 = 4;

    private const double AxisScale = 32767;

    private static readonly TimeSpan GamepadInterval = TimeSpan.FromMilliseconds(1000d / 30);

    private static readonly MotorPoseSetting[] DefaultPoses =
    [
        new() { Servo1 = 90, Servo2 = 90, Green = 255 },
        new() { Servo1 = 30, Servo2 = 90, Blue = 255 },
        new() { Servo1 = 150, Servo2 = 90, Red = 255, Green = 128 },
        new() { Servo1 = 90, Servo2 = 30, Red = 255, Blue = 255 }
    ];

    private readonly TimeProvider timeProvider;

    private readonly IDispatcher dispatcher;

    private readonly IMotorController motorController;

    private readonly IGamepadReader gamepadReader;

    private readonly UserSettingStore settingStore;

    private CancellationTokenSource? gamepadCts;

    private CancellationTokenSource? playCts;

    private bool active;

    public string Port { get; }

    public DeviceStatus? Status { get; }

    public IReadOnlyList<MotorPoseItem> Poses { get; }

    public IReadOnlyList<LedPreset> LedPresets { get; } =
    [
        new("Off", Color.FromRgb(0, 0, 0)),
        new("White", Color.FromRgb(255, 255, 255)),
        new("Red", Color.FromRgb(255, 0, 0)),
        new("Orange", Color.FromRgb(255, 128, 0)),
        new("Yellow", Color.FromRgb(255, 255, 0)),
        new("Green", Color.FromRgb(0, 255, 0)),
        new("Cyan", Color.FromRgb(0, 255, 255)),
        new("Blue", Color.FromRgb(0, 0, 255)),
        new("Magenta", Color.FromRgb(255, 0, 255))
    ];

    public ObservableCollection<string> Logs { get; } = [];

    [ObservableProperty]
    public partial int Servo1 { get; set; } = NeutralAngle;

    [ObservableProperty]
    public partial int Servo2 { get; set; } = NeutralAngle;

    [ObservableProperty]
    public partial int Servo1Minimum { get; set; } = MinimumAngle;

    [ObservableProperty]
    public partial int Servo1Maximum { get; set; } = MaximumAngle;

    [ObservableProperty]
    public partial int Servo2Minimum { get; set; } = MinimumAngle;

    [ObservableProperty]
    public partial int Servo2Maximum { get; set; } = MaximumAngle;

    [ObservableProperty]
    public partial int Red { get; set; }

    [ObservableProperty]
    public partial int Green { get; set; }

    [ObservableProperty]
    public partial int Blue { get; set; }

    [ObservableProperty]
    public partial Color LedColor { get; set; } = Colors.Black;

    [ObservableProperty]
    public partial bool IsGamepadControl { get; set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial double Interval { get; set; } = 1;

    public ICommand ServoPresetCommand { get; }

    public ICommand LedPresetCommand { get; }

    public ICommand SelectPoseCommand { get; }

    public ICommand SavePoseCommand { get; }

    public ICommand PlayCommand { get; }

    public ICommand StopCommand { get; }

    public MotorViewModel(TimeProvider timeProvider, IDispatcher dispatcher, MotorControllerOption option, DeviceState deviceState, UserSettingStore settingStore, IMotorController motorController, IGamepadReader gamepadReader)
    {
        this.timeProvider = timeProvider;
        this.dispatcher = dispatcher;
        this.settingStore = settingStore;
        this.motorController = motorController;
        this.gamepadReader = gamepadReader;

        Port = String.IsNullOrEmpty(option.Port) ? "Not configured" : option.Port;
        Status = deviceState.Devices.FirstOrDefault(static x => x.Name == "Motor");

        var saved = settingStore.Value.MotorPoses;
        var poses = saved.Count == DefaultPoses.Length ? saved : (IEnumerable<MotorPoseSetting>)DefaultPoses;
        Poses = [.. poses.Select(static (x, i) => new MotorPoseItem($"Pose {i + 1}", x.Servo1, x.Servo2, Color.FromRgb(x.Red, x.Green, x.Blue)))];
        Poses[0].IsSelected = true;

        ServoPresetCommand = MakeDelegateCommand<string>(ApplyServoPreset);
        LedPresetCommand = MakeDelegateCommand<LedPreset>(x => SetLed(x.Color));
        SelectPoseCommand = MakeDelegateCommand<MotorPoseItem>(x =>
        {
            StopPlayback();
            Select(x);
            ApplyPose(x);
        });
        SavePoseCommand = MakeAsyncCommand(SavePoseAsync);
        // Playback runs until stopped, so it does not hold the busy state
        PlayCommand = MakeDelegateCommand(CommandMode.Simple, () => _ = PlayAsync(), () => !IsPlaying);
        StopCommand = MakeDelegateCommand(StopPlayback, () => IsPlaying);

        SubscribeServo1(x => SendServo(ServoChannel.Servo1, x));
        SubscribeServo2(x => SendServo(ServoChannel.Servo2, x));
        SubscribeServo1Minimum(_ => Servo1 = Math.Clamp(Servo1, Servo1Minimum, Math.Max(Servo1Minimum, Servo1Maximum)));
        SubscribeServo1Maximum(_ => Servo1 = Math.Clamp(Servo1, Servo1Minimum, Math.Max(Servo1Minimum, Servo1Maximum)));
        SubscribeServo2Minimum(_ => Servo2 = Math.Clamp(Servo2, Servo2Minimum, Math.Max(Servo2Minimum, Servo2Maximum)));
        SubscribeServo2Maximum(_ => Servo2 = Math.Clamp(Servo2, Servo2Minimum, Math.Max(Servo2Minimum, Servo2Maximum)));
        SubscribeRed(_ => SendLed());
        SubscribeGreen(_ => SendLed());
        SubscribeBlue(_ => SendLed());
        SubscribeIsGamepadControl(x =>
        {
            if (x)
            {
                StartGamepad();
            }
            else
            {
                StopGamepad();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopGamepad();
            StopPlayback();
        }

        base.Dispose(disposing);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        active = true;
        motorController.Start();
        AddLog($"Start {Port}");
        return Task.CompletedTask;
    }

    public override async Task OnNavigatingFromAsync(INavigationContext context)
    {
        active = false;
        IsGamepadControl = false;
        StopPlayback();
        await motorController.StopAsync();
    }

    private void ApplyServoPreset(string parameter)
    {
        var values = parameter.Split(',');
        var angle = Int32.Parse(values[1], CultureInfo.InvariantCulture);
        if (values[0] == "1")
        {
            Servo1 = Math.Clamp(angle, Servo1Minimum, Servo1Maximum);
        }
        else
        {
            Servo2 = Math.Clamp(angle, Servo2Minimum, Servo2Maximum);
        }
    }

    private void SendServo(ServoChannel channel, int angle)
    {
        if (!active)
        {
            return;
        }

        motorController.SetServo(channel, angle);
        AddLog(String.Create(CultureInfo.InvariantCulture, $"SERVO{(int)channel + 1} {angle}"));
    }

    private void SetLed(Color color)
    {
        Red = color.R;
        Green = color.G;
        Blue = color.B;
    }

    private void SendLed()
    {
        LedColor = Color.FromRgb((byte)Red, (byte)Green, (byte)Blue);
        if (!active)
        {
            return;
        }

        motorController.SetLed((byte)Red, (byte)Green, (byte)Blue);
        AddLog(String.Create(CultureInfo.InvariantCulture, $"LED {Red} {Green} {Blue}"));
    }

    private void Select(MotorPoseItem pose)
    {
        foreach (var item in Poses)
        {
            item.IsSelected = ReferenceEquals(item, pose);
        }
    }

    private void ApplyPose(MotorPoseItem pose)
    {
        Servo1 = Math.Clamp(pose.Servo1, Servo1Minimum, Servo1Maximum);
        Servo2 = Math.Clamp(pose.Servo2, Servo2Minimum, Servo2Maximum);
        SetLed(pose.Color);
    }

    private async Task SavePoseAsync()
    {
        var pose = Poses.FirstOrDefault(static x => x.IsSelected) ?? Poses[0];
        pose.Servo1 = Servo1;
        pose.Servo2 = Servo2;
        pose.Color = LedColor;

        var setting = settingStore.Value.MotorPoses;
        setting.Clear();
        foreach (var item in Poses)
        {
            setting.Add(new MotorPoseSetting { Servo1 = item.Servo1, Servo2 = item.Servo2, Red = item.Color.R, Green = item.Color.G, Blue = item.Color.B });
        }

        await settingStore.SaveAsync();
        AddLog($"Save {pose.Name}");
    }

    private async Task PlayAsync()
    {
        playCts = new CancellationTokenSource();
        var token = playCts.Token;
        IsPlaying = true;
        AddLog("Play");
        try
        {
            var index = Math.Max(0, Poses.ToList().FindIndex(static x => x.IsSelected));
            while (!token.IsCancellationRequested)
            {
                var pose = Poses[index];
                Select(pose);
                ApplyPose(pose);
                await Task.Delay(TimeSpan.FromSeconds(Interval), timeProvider, token);
                index = (index + 1) % Poses.Count;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsPlaying = false;
        }
    }

    private void StopPlayback()
    {
        if (playCts is null)
        {
            return;
        }

        playCts.Cancel();
        playCts.Dispose();
        playCts = null;
        AddLog("Stop");
    }

    private void StartGamepad()
    {
        if (gamepadCts is not null)
        {
            return;
        }

        gamepadCts = new CancellationTokenSource();
        var token = gamepadCts.Token;
        _ = Task.Run(() => GamepadLoopAsync(token), token);
        AddLog("Gamepad control on");
    }

    private void StopGamepad()
    {
        if (gamepadCts is null)
        {
            return;
        }

        gamepadCts.Cancel();
        gamepadCts.Dispose();
        gamepadCts = null;
        AddLog("Gamepad control off");
    }

    private async Task GamepadLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(GamepadInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                var x = gamepadReader.GetAxisValue(AxisServo1) / AxisScale;
                var y = gamepadReader.GetAxisValue(AxisServo2) / AxisScale;
                dispatcher.Post(() =>
                {
                    Servo1 = MapAxis(x, Servo1Minimum, Servo1Maximum);
                    Servo2 = MapAxis(y, Servo2Minimum, Servo2Maximum);
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static int MapAxis(double value, int minimum, int maximum) =>
        (int)Math.Round(minimum + ((Math.Clamp(value, -1d, 1d) + 1) / 2 * (maximum - minimum)));

    private void AddLog(string message)
    {
        Logs.Insert(0, String.Create(CultureInfo.InvariantCulture, $"{timeProvider.GetLocalNow():HH:mm:ss.fff}  {message}"));
        while (Logs.Count > MaxLogs)
        {
            Logs.RemoveAt(Logs.Count - 1);
        }
    }
}
