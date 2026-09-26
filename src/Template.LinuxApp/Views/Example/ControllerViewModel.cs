namespace Template.LinuxApp.Views.Example;

using System.Security.Cryptography;

using Avalonia.Threading;

using Template.LinuxApp.Components.Gamepad;
using Template.LinuxApp.Components.Motor;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class ControllerViewModel : AppViewModelBase
{
    private const int NeutralAngle = 90;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(1000d / 60);

    private static readonly (byte R, byte G, byte B)[] ShiftColors =
    [
        (0, 0, 255),
        (0, 255, 255),
        (0, 255, 0),
        (255, 255, 0),
        (255, 128, 0),
        (255, 0, 0)
    ];

    private readonly TimeProvider timeProvider;

    private readonly IDispatcher dispatcher;

    private readonly IGamepadReader gamepadReader;

    private readonly IMotorController motorController;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    [ObservableProperty]
    public partial int Fps { get; set; }

    [ObservableProperty]
    public partial int Shift { get; set; } = 1;

    [ObservableProperty]
    public partial int Speed { get; set; }

    [ObservableProperty]
    public partial int SteeringAngle { get; set; }

    [ObservableProperty]
    public partial bool Accel { get; set; }

    [ObservableProperty]
    public partial bool Brake { get; set; }

    [ObservableProperty]
    public partial bool ShiftDown { get; set; }

    [ObservableProperty]
    public partial bool ShiftUp { get; set; }

    public ControllerViewModel(TimeProvider timeProvider, IDispatcher dispatcher, IGamepadReader gamepadReader, IMotorController motorController)
    {
        this.timeProvider = timeProvider;
        this.dispatcher = dispatcher;
        this.gamepadReader = gamepadReader;
        this.motorController = motorController;
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
        motorController.Start();

        cts = new CancellationTokenSource();
        var token = cts.Token;
        loopTask = Task.Run(() => LoopAsync(token), token);
        return Task.CompletedTask;
    }

    public override async Task OnNavigatingFromAsync(INavigationContext context)
    {
        if ((cts is not null) && (loopTask is not null))
        {
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

        await motorController.StopAsync();
    }

    private async Task LoopAsync(CancellationToken token)
    {
        var model = new GameModel();
        var (r, g, b) = ShiftColors[model.ShiftValue];
        motorController.SetLed(r, g, b);
        motorController.SetServo(ServoChannel.Servo1, NeutralAngle);
        motorController.SetServo(ServoChannel.Servo2, NeutralAngle);

        using var timer = new PeriodicTimer(FrameInterval, timeProvider);
        var frames = 0;
        var fpsTimestamp = timeProvider.GetTimestamp();
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            model.Update(
                gamepadReader.GetAxisValue(0),
                gamepadReader.GetButtonPressed(1),
                gamepadReader.GetButtonPressed(0),
                gamepadReader.GetButtonPressed(2),
                gamepadReader.GetButtonPressed(3));

            if (model.IsUpdated)
            {
                var shift = model.ShiftValue + 1;
                var speed = model.Speed;
                var steeringAngle = model.SteeringAngle;
                var accel = model.Accel;
                var brake = model.Brake;
                var shiftDown = model.ShiftDown;
                var shiftUp = model.ShiftUp;
                dispatcher.Post(() =>
                {
                    Shift = shift;
                    Speed = speed;
                    SteeringAngle = steeringAngle;
                    Accel = accel;
                    Brake = brake;
                    ShiftDown = shiftDown;
                    ShiftUp = shiftUp;
                });

                if (model.ShiftChanged)
                {
                    (r, g, b) = ShiftColors[model.ShiftValue];
                    motorController.SetLed(r, g, b);
                }

                if (model.ThrottleChanged)
                {
                    motorController.SetServo(ServoChannel.Servo2, model.ThrottleAngle);
                }

                if (model.SteeringChanged)
                {
                    motorController.SetServo(ServoChannel.Servo1, model.SteeringAngle);
                }
            }

            frames++;
            if (timeProvider.GetElapsedTime(fpsTimestamp) >= TimeSpan.FromSeconds(1))
            {
                var fps = frames;
                dispatcher.Post(() => Fps = fps);
                frames = 0;
                fpsTimestamp = timeProvider.GetTimestamp();
            }
        }
    }

    private sealed class GameModel
    {
        private const double BaseAcceleration = 32d / 60;

        private const double BrakeVelocity = 80d / 60;

        private const double DefaultVelocity = 48d / 60;

        private const double OverSpeedDecay = 48d / 60;

        private static readonly int[] ShiftMaxSpeed = [70, 110, 150, 190, 230, 255];

        private static readonly int[] ShiftOptimalStart = [0, 30, 70, 110, 150, 190];

        private static readonly double[] ShiftTorqueMultiplier = [2.5, 1.9, 1.5, 1.25, 1.1, 0.9];

        private static readonly int[] ShiftSpeedFluctuation = [3, 3, 4, 4, 5, 5];

        private double rawSpeed;

        public int ShiftValue { get; private set; }

        public int Speed { get; private set; }

        public int ThrottleAngle { get; private set; } = NeutralAngle;

        public int SteeringAngle { get; private set; } = NeutralAngle;

        public bool Accel { get; private set; }

        public bool Brake { get; private set; }

        public bool ShiftDown { get; private set; }

        public bool ShiftUp { get; private set; }

        public bool IsUpdated { get; private set; }

        public bool ShiftChanged { get; private set; }

        public bool SteeringChanged { get; private set; }

        public bool ThrottleChanged { get; private set; }

        public void Update(short axis, bool accel, bool brake, bool shiftDown, bool shiftUp)
        {
            var previousShiftValue = ShiftValue;
            var previousSpeed = Speed;

            if (shiftDown && !ShiftDown && (ShiftValue > 0))
            {
                ShiftValue--;
            }

            if (shiftUp && !ShiftUp && (ShiftValue < ShiftMaxSpeed.Length - 1))
            {
                ShiftValue++;
            }

            rawSpeed = CalculateSpeed(accel, brake);
            var speed = (int)rawSpeed;
            var throttleAngle = NeutralAngle + (speed * 90 / 255);
            var steeringAngle = (int)((axis + 32768) * 180.0 / 65535.0);

            ShiftChanged = ShiftValue != previousShiftValue;
            SteeringChanged = steeringAngle != SteeringAngle;
            ThrottleChanged = throttleAngle != ThrottleAngle;
            IsUpdated = (speed != previousSpeed) ||
                        (steeringAngle != SteeringAngle) ||
                        (accel != Accel) ||
                        (brake != Brake) ||
                        (shiftDown != ShiftDown) ||
                        (shiftUp != ShiftUp);

            Speed = speed;
            SteeringAngle = steeringAngle;
            ThrottleAngle = throttleAngle;
            Accel = accel;
            Brake = brake;
            ShiftDown = shiftDown;
            ShiftUp = shiftUp;
        }

        private double CalculateSpeed(bool accel, bool brake)
        {
            var effectiveMaxSpeed = ShiftMaxSpeed[ShiftValue] + ShiftSpeedFluctuation[ShiftValue];

            if (brake)
            {
                return Math.Max(0, rawSpeed - BrakeVelocity);
            }

            if (rawSpeed > effectiveMaxSpeed)
            {
                return Math.Max(0, rawSpeed - OverSpeedDecay);
            }

            if (accel)
            {
                return Math.Min(255, ApplyRedlineFluctuation(rawSpeed + CalculateAcceleration()));
            }

            return Math.Max(0, rawSpeed - DefaultVelocity);
        }

        private double CalculateAcceleration()
        {
            var maxSpeed = ShiftMaxSpeed[ShiftValue];
            var optimalStart = ShiftOptimalStart[ShiftValue];
            if (rawSpeed >= maxSpeed + ShiftSpeedFluctuation[ShiftValue])
            {
                return 0;
            }

            var progress = Math.Clamp((rawSpeed - optimalStart) / (maxSpeed - optimalStart), 0, 1.0);
            var torque = progress switch
            {
                < 0.3 => 0.6 + ((progress / 0.3) * 0.4),
                < 0.7 => 1.0,
                _ => 1.0 - (((progress - 0.7) / 0.3) * 0.6)
            };

            var acceleration = BaseAcceleration * ShiftTorqueMultiplier[ShiftValue] * torque;
            if (rawSpeed < optimalStart)
            {
                acceleration *= Math.Max(0.3, rawSpeed / optimalStart);
            }

            return acceleration;
        }

        private double ApplyRedlineFluctuation(double speed)
        {
            var maxSpeed = ShiftMaxSpeed[ShiftValue];
            var fluctuation = ShiftSpeedFluctuation[ShiftValue];
            var effectiveMaxSpeed = maxSpeed + fluctuation;
            if ((speed > effectiveMaxSpeed) || (speed < maxSpeed * 0.95))
            {
                return speed;
            }

            var proximity = Math.Min(1.0, (speed - (maxSpeed * 0.95)) / (maxSpeed * 0.05));
            var oscillation = ((RandomNumberGenerator.GetInt32(0, 10001) / 10000.0) - 0.5) * 2 * proximity * fluctuation;
            return Math.Max(maxSpeed - fluctuation, Math.Min(effectiveMaxSpeed, speed + oscillation));
        }
    }
}
