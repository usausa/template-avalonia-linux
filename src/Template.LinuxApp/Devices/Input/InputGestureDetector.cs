namespace Template.LinuxApp.Devices.Input;

public sealed class InputGestureDetector : IDisposable
{
    private readonly Lock sync = new();

    private readonly TimeProvider timeProvider;

    private readonly InputButtonOption option;

    private readonly Action<InputAction> handler;

    private readonly ITimer timer;

    private bool pressed;

    private bool suppressed;

    private bool longPressed;

    private long? lastPressTimestamp;

    public InputGestureDetector(TimeProvider timeProvider, InputButtonOption option, Action<InputAction> handler)
    {
        this.timeProvider = timeProvider;
        this.option = option;
        this.handler = handler;
        timer = timeProvider.CreateTimer(static x => ((InputGestureDetector)x!).OnTimer(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        timer.Dispose();
    }

    public void Down()
    {
        var press = false;
        lock (sync)
        {
            if (pressed || suppressed)
            {
                return;
            }

            var timestamp = timeProvider.GetTimestamp();
            if ((option.MinimumIntervalMilliseconds > 0) &&
                (lastPressTimestamp is { } last) &&
                (timeProvider.GetElapsedTime(last, timestamp).TotalMilliseconds < option.MinimumIntervalMilliseconds))
            {
                suppressed = true;
                return;
            }

            pressed = true;
            longPressed = false;
            lastPressTimestamp = timestamp;

            if (option.LongPressMilliseconds > 0)
            {
                timer.Change(TimeSpan.FromMilliseconds(option.LongPressMilliseconds), Timeout.InfiniteTimeSpan);
            }
            else
            {
                press = true;
                if (option.RepeatDelayMilliseconds > 0)
                {
                    timer.Change(TimeSpan.FromMilliseconds(option.RepeatDelayMilliseconds), TimeSpan.FromMilliseconds(option.RepeatIntervalMilliseconds));
                }
            }
        }

        if (press)
        {
            handler(InputAction.Press);
        }
    }

    public void Up()
    {
        bool press;
        lock (sync)
        {
            if (suppressed)
            {
                suppressed = false;
                return;
            }

            if (!pressed)
            {
                return;
            }

            pressed = false;
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            press = (option.LongPressMilliseconds > 0) && !longPressed;
        }

        if (press)
        {
            handler(InputAction.Press);
        }
    }

    public void Reset()
    {
        lock (sync)
        {
            pressed = false;
            suppressed = false;
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTimer()
    {
        InputAction? action = null;
        lock (sync)
        {
            if (!pressed)
            {
                return;
            }

            if (option.LongPressMilliseconds <= 0)
            {
                action = InputAction.Repeat;
            }
            else if (!longPressed)
            {
                longPressed = true;
                action = InputAction.LongPress;
            }
        }

        if (action.HasValue)
        {
            handler(action.Value);
        }
    }
}
