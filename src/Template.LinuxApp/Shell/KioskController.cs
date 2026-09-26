namespace Template.LinuxApp.Shell;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using Template.LinuxApp.Components.Gamepad;
using Template.LinuxApp.Services;
using Template.LinuxApp.Settings;
using Template.LinuxApp.Views.Dialogs;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class KioskController
{
    private const double CornerSize = 64;

    private static readonly TimeSpan CodeTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan CodeMismatchDisplay = TimeSpan.FromSeconds(2);

    private readonly ILogger<KioskController> log;

    private readonly KioskSetting setting;

    private readonly IDialogService dialogService;

    private readonly IGamepadReader gamepadReader;

    private readonly DispatcherTimer holdTimer;

    private readonly DispatcherTimer padHoldTimer;

    private readonly DispatcherTimer codeTimer;

    private Window? window;

    private IPointer? holdPointer;

    private bool prompting;

    private CodeDialog? codeDialog;

    private List<byte>? codeInput;

    private TaskCompletionSource<bool>? codeCompletion;

    public KioskController(ILogger<KioskController> log, KioskSetting setting, IDialogService dialogService, IGamepadReader gamepadReader)
    {
        this.log = log;
        this.setting = setting;
        this.dialogService = dialogService;
        this.gamepadReader = gamepadReader;
        holdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(setting.AdminHoldSeconds) };
        holdTimer.Tick += async (_, _) =>
        {
            CancelHold();
            await PromptAdminAsync();
        };
        padHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(setting.AdminHoldSeconds) };
        padHoldTimer.Tick += async (_, _) =>
        {
            padHoldTimer.Stop();
            await PromptPadCodeAsync();
        };
        codeTimer = new DispatcherTimer { Interval = CodeTimeout };
        codeTimer.Tick += (_, _) => codeCompletion?.TrySetResult(false);
    }

    public void Attach(Window target)
    {
        window = target;

        target.WindowDecorations = WindowDecorations.None;
        target.CanResize = false;
        target.ShowInTaskbar = false;
        if (target.Screens.Primary is { } screen)
        {
            target.Position = screen.Bounds.Position;
            target.Width = screen.Bounds.Width / screen.Scaling;
            target.Height = screen.Bounds.Height / screen.Scaling;
        }
        target.WindowState = WindowState.FullScreen;

        if (setting.HideCursor)
        {
            target.Classes.Add("hide-cursor");
        }

        target.Closing += OnClosing;
        target.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, true);
        target.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, true);
        target.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, true);

        if (setting.AdminPadButtons.Count > 0)
        {
            gamepadReader.ButtonChanged += OnPadButtonChanged;
        }
    }

    //--------------------------------------------------------------------------------
    // Close
    //--------------------------------------------------------------------------------

    private static void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!e.IsProgrammatic && (e.CloseReason != WindowCloseReason.OSShutdown))
        {
            e.Cancel = true;
        }
    }

    //--------------------------------------------------------------------------------
    // Admin
    //--------------------------------------------------------------------------------

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (prompting || (holdPointer is not null) || !IsInCorner(e.GetPosition(window)))
        {
            return;
        }

        holdPointer = e.Pointer;
        holdTimer.Start();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if ((e.Pointer == holdPointer) && !IsInCorner(e.GetPosition(window)))
        {
            CancelHold();
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer == holdPointer)
        {
            CancelHold();
        }
    }

    private bool IsInCorner(Point position) => (window is not null) && (position.X >= window.Bounds.Width - CornerSize) && (position.Y <= CornerSize);

    private void CancelHold()
    {
        holdPointer = null;
        holdTimer.Stop();
    }

    private async Task PromptAdminAsync()
    {
        prompting = true;
        try
        {
            var pin = await dialogService.InputAsync("Admin PIN", password: true);
            if (pin is null)
            {
                return;
            }

            if (String.Equals(pin, setting.AdminPin, StringComparison.Ordinal))
            {
                log.InfoKioskAdminExit();
                window?.Close();
            }
            else
            {
                log.WarnKioskAdminPinMismatch();
                await dialogService.NotifyAsync("PIN is incorrect.");
            }
        }
        finally
        {
            prompting = false;
        }
    }

    //--------------------------------------------------------------------------------
    // Admin (gamepad)
    //--------------------------------------------------------------------------------

    private void OnPadButtonChanged(object? sender, GamepadButtonEventArgs e) =>
        Dispatcher.UIThread.Post(() => HandlePadButton(e.Button, e.Pressed));

    private void HandlePadButton(byte button, bool pressed)
    {
        if (codeInput is not null)
        {
            if (pressed)
            {
                AcceptCode(button);
            }

            return;
        }

        if (prompting || !setting.AdminPadButtons.Contains(button))
        {
            return;
        }

        if (setting.AdminPadButtons.All(gamepadReader.GetButtonPressed))
        {
            padHoldTimer.Start();
        }
        else
        {
            padHoldTimer.Stop();
        }
    }

    private async Task PromptPadCodeAsync()
    {
        if (prompting || ((window as MainWindow)?.DialogLayer is not { } layer) || !setting.AdminPadButtons.All(gamepadReader.GetButtonPressed))
        {
            return;
        }

        prompting = true;
        var dialog = new CodeDialog { Length = setting.AdminPadCode.Count };
        var input = new List<byte>();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        codeDialog = dialog;
        codeInput = input;
        codeCompletion = completion;
        try
        {
            using (layer.Open(dialog))
            {
                codeTimer.Start();
                if (!await completion.Task)
                {
                    return;
                }

                if (input.SequenceEqual(setting.AdminPadCode))
                {
                    log.InfoKioskAdminExit();
                    window?.Close();
                    return;
                }

                log.WarnKioskAdminCodeMismatch();
                dialog.Message = "Code is incorrect.";
                await Task.Delay(CodeMismatchDisplay);
            }
        }
        finally
        {
            codeTimer.Stop();
            codeDialog = null;
            codeInput = null;
            codeCompletion = null;
            prompting = false;
        }
    }

    private void AcceptCode(byte button)
    {
        if ((codeInput is null) || (codeDialog is null) || (codeCompletion is null) || codeCompletion.Task.IsCompleted)
        {
            return;
        }

        codeInput.Add(button);
        codeDialog.Entered = codeInput.Count;
        codeTimer.Stop();
        codeTimer.Start();
        if (codeInput.Count >= setting.AdminPadCode.Count)
        {
            codeCompletion.TrySetResult(true);
        }
    }
}
