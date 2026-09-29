namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

public sealed class GamepadDiagram : Control
{
    private const double DesignWidth = 180;

    private const double ContentLeft = 13.5;

    private const double ContentTop = -6.7;

    private const double ContentWidth = 153;

    private const double ContentHeight = 119.4;

    private const double DisconnectedOpacity = 0.4;

    private const double PressThreshold = 0.5;

    private const double Deadzone = 0.08;

    private const double EdgeOffset = 0.6;

    private const double PadRadius = 17.5;

    private const double MountRadius = 13;

    private const double WellRadius = 10.2;

    private const double CapRadius = 8.6;

    private const double CapTopRadius = 6;

    private const double StickTravel = 4.5;

    private const double TiltHalfAngle = 40;

    private const double TiltBaseOpacity = 0.35;

    private const double ArmLength = 9.5;

    private const double ArmHalfWidth = 3.4;

    private const double ArmTipRadius = 1.6;

    private const double ArrowTip = 0.73;

    private const double ArrowBase = 0.45;

    private const double ArrowHalf = 0.155;

    private const double FaceRadius = 5.1;

    private const double PillWidth = 11.2;

    private const double PillHeight = 3.4;

    private const double GuideRadius = 3.4;

    private const double TriggerRadius = 2.6;

    private const double TriggerVisible = 6.4;

    private const double LabelSize = 3.4;

    private const double SmallLabelSize = 2.4;

    private const double LetterSize = 5.5;

    private const double CapCenter = 0.36;

    private const int ShadowLayers = 3;

    private const double ShadowStep = 0.7;

    private const int ButtonA = 0;
    private const int ButtonB = 1;
    private const int ButtonX = 2;
    private const int ButtonY = 3;
    private const int ButtonLb = 4;
    private const int ButtonRb = 5;
    private const int ButtonBack = 6;
    private const int ButtonStart = 7;
    private const int ButtonGuide = 8;
    private const int ButtonLs = 9;
    private const int ButtonRs = 10;

    private const int AxisLx = 0;
    private const int AxisLy = 1;
    private const int AxisLt = 2;
    private const int AxisRx = 3;
    private const int AxisRy = 4;
    private const int AxisRt = 5;
    private const int AxisDpadX = 6;
    private const int AxisDpadY = 7;

    private const int LabelLt = 0;
    private const int LabelRt = 1;
    private const int LabelLb = 2;
    private const int LabelRb = 3;
    private const int LabelBack = 4;
    private const int LabelStart = 5;

    private static readonly Color DefaultBody = Color.FromRgb(0x3D, 0x3D, 0x3D);

    private static readonly Color DefaultStroke = Color.FromRgb(0x9E, 0x9E, 0x9E);

    private static readonly Color DefaultPressed = Color.FromRgb(0x57, 0x94, 0xF2);

    private static readonly Color DefaultForeground = Color.FromRgb(0xE0, 0xE0, 0xE0);

    private static readonly ImmutableSolidColorBrush WhiteBrush = new(Colors.White);

    private static readonly ImmutableSolidColorBrush InkBrush = new(Color.FromRgb(0x1C, 0x1C, 0x1C));

    private static readonly string[] LabelTexts = ["LT", "RT", "LB", "RB", "BACK", "START"];

    private static readonly double[] LabelSizes = [LabelSize, LabelSize, LabelSize, LabelSize, SmallLabelSize, SmallLabelSize];

    private static readonly string[] FaceTexts = ["A", "B", "X", "Y"];

    private static readonly int[] FaceButtons = [ButtonA, ButtonB, ButtonX, ButtonY];

    private static readonly Point LeftStickCenter = new(44.5, 35.5);

    private static readonly Point RightStickCenter = new(108.5, 56);

    private static readonly Point DpadCenter = new(71.5, 56);

    private static readonly Point FacePadCenter = new(135.5, 35.5);

    private static readonly Point[] FaceCenters = [new(135.5, 46.5), new(146.5, 35.5), new(124.5, 35.5), new(135.5, 24.5)];

    private static readonly Point BackCenter = new(77.9, 38.1);

    private static readonly Point StartCenter = new(102.1, 38.1);

    private static readonly Point BackLabel = new(77.9, 34.2);

    private static readonly Point StartLabel = new(102.1, 34.2);

    private static readonly Point GuideCenter = new(90, 64.5);

    private static readonly Rect LeftTrigger = new(37, -6.4, 17, 8.4);

    private static readonly Rect RightTrigger = new(126, -6.4, 17, 8.4);

    private static readonly Point LeftTriggerLabel = new(45.5, -3.2);

    private static readonly Point RightTriggerLabel = new(134.5, -3.2);

    private static readonly Point LeftBumperLabel = new(44, 4.4);

    private static readonly Point RightBumperLabel = new(136, 4.4);

    private static readonly Curve Envelope = new(
        new Point(90, 9.7),
        new(98, 9.7), new(106, 10.5), new(111.5, 11),
        new(114.5, 10.4), new(120, 0.3), new(123.5, 0),
        new(128, 0.3), new(137, 1.2), new(143, 1.8),
        new(144.5, 2.3), new(145.3, 4.6), new(147.3, 6.2),
        new(147.8, 7), new(148.3, 7.9), new(148.7, 8.7),
        new(154.5, 18.5), new(166.5, 53.7), new(166.5, 83.7),
        new(166.5, 97), new(162, 110.2), new(155.4, 110.2),
        new(151.4, 110.2), new(149.5, 108.2), new(148, 106.2),
        new(140, 95.7), new(132, 85), new(124.5, 74.6),
        new(119, 71.5), new(108, 71.9), new(90, 71.9));

    private static readonly Curve BlockSeam = new(new Point(123.5, 7.6), new(131, 7.7), new(142, 8), new(148.7, 8.7));

    private static readonly Curve BlockEdge = new(new Point(123.5, 7.6), new(123.5, 5.1), new(123.5, 2.5), new(123.5, 0));

    private static readonly StreamGeometry BodyGeometry = BuildMirrored(Envelope);

    private static readonly StreamGeometry LeftBumper = BuildSide(true, (Envelope.Slice(2, 4), false), (BlockSeam, true), (BlockEdge, false));

    private static readonly StreamGeometry RightBumper = BuildSide(false, (Envelope.Slice(2, 4), false), (BlockSeam, true), (BlockEdge, false));

    private static readonly StreamGeometry LeftTriggerGeometry = BuildRoundedRect(LeftTrigger, TriggerRadius);

    private static readonly StreamGeometry RightTriggerGeometry = BuildRoundedRect(RightTrigger, TriggerRadius);

    private static readonly StreamGeometry DpadCross = BuildCross(ArmHalfWidth, ArmLength, ArmTipRadius);

    private static readonly Rect[] ArmRects =
    [
        new(ArmHalfWidth, -ArmHalfWidth, ArmLength - ArmHalfWidth + 1, ArmHalfWidth * 2),
        new(-ArmHalfWidth, ArmHalfWidth, ArmHalfWidth * 2, ArmLength - ArmHalfWidth + 1),
        new(-ArmLength - 1, -ArmHalfWidth, ArmLength - ArmHalfWidth + 1, ArmHalfWidth * 2),
        new(-ArmHalfWidth, -ArmLength - 1, ArmHalfWidth * 2, ArmLength - ArmHalfWidth + 1)
    ];

    private static readonly StreamGeometry[] DpadArrows = [BuildArrow(0), BuildArrow(90), BuildArrow(180), BuildArrow(270)];

    private static readonly StreamGeometry TiltArc = BuildArc(WellRadius + 0.4, TiltHalfAngle);

    private static readonly StreamGeometry HouseGlyph = BuildHouse();

    private static readonly RenderOptions FullOpacity = new() { RequiresFullOpacityHandling = true };

    public static readonly StyledProperty<IReadOnlyList<bool>?> ButtonsProperty = AvaloniaProperty.Register<GamepadDiagram, IReadOnlyList<bool>?>(nameof(Buttons));

    public static readonly StyledProperty<IReadOnlyList<double>?> AxesProperty = AvaloniaProperty.Register<GamepadDiagram, IReadOnlyList<double>?>(nameof(Axes));

    public static readonly StyledProperty<bool> IsConnectedProperty = AvaloniaProperty.Register<GamepadDiagram, bool>(nameof(IsConnected));

    public static readonly StyledProperty<IReadOnlyList<Color>?> FaceColorsProperty = AvaloniaProperty.Register<GamepadDiagram, IReadOnlyList<Color>?>(nameof(FaceColors));

    public static readonly StyledProperty<IBrush?> BodyFillProperty = AvaloniaProperty.Register<GamepadDiagram, IBrush?>(nameof(BodyFill));

    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<GamepadDiagram, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> PressedFillProperty = AvaloniaProperty.Register<GamepadDiagram, IBrush?>(nameof(PressedFill));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<GamepadDiagram, IBrush?>(nameof(Foreground));

    private Palette? palette;

    public IReadOnlyList<bool>? Buttons
    {
        get => GetValue(ButtonsProperty);
        set => SetValue(ButtonsProperty, value);
    }

    public IReadOnlyList<double>? Axes
    {
        get => GetValue(AxesProperty);
        set => SetValue(AxesProperty, value);
    }

    public bool IsConnected
    {
        get => GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    public IReadOnlyList<Color>? FaceColors
    {
        get => GetValue(FaceColorsProperty);
        set => SetValue(FaceColorsProperty, value);
    }

    public IBrush? BodyFill
    {
        get => GetValue(BodyFillProperty);
        set => SetValue(BodyFillProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? PressedFill
    {
        get => GetValue(PressedFillProperty);
        set => SetValue(PressedFillProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    static GamepadDiagram()
    {
        AffectsRender<GamepadDiagram>(ButtonsProperty, AxesProperty, IsConnectedProperty, FaceColorsProperty, BodyFillProperty, StrokeProperty, PressedFillProperty, ForegroundProperty);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if ((width <= 0) || (height <= 0))
        {
            return;
        }

        var colors = ResolvePalette();
        var scale = Math.Min(width / ContentWidth, height / ContentHeight);
        var offsetX = ((width - (ContentWidth * scale)) / 2) - (ContentLeft * scale);
        var offsetY = ((height - (ContentHeight * scale)) / 2) - (ContentTop * scale);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            if (IsConnected)
            {
                DrawController(context, colors, true);
            }
            else
            {
                using (context.PushRenderOptions(FullOpacity))
                using (context.PushOpacity(DisconnectedOpacity))
                {
                    DrawController(context, colors, false);
                }
            }
        }
    }

    private void DrawController(DrawingContext context, Palette colors, bool connected)
    {
        DrawShadow(context, colors);
        DrawTrigger(context, colors, LeftTriggerGeometry, LeftTrigger, LeftTriggerLabel, LabelLt, connected ? Axis(AxisLt) : -1d);
        DrawTrigger(context, colors, RightTriggerGeometry, RightTrigger, RightTriggerLabel, LabelRt, connected ? Axis(AxisRt) : -1d);
        DrawPlate(context, colors);
        DrawBumper(context, colors, LeftBumper, LeftBumperLabel, LabelLb, Button(ButtonLb));
        DrawBumper(context, colors, RightBumper, RightBumperLabel, LabelRb, Button(ButtonRb));
        context.DrawGeometry(null, colors.Outline, BodyGeometry);
        context.DrawEllipse(colors.Pad, colors.Outline, LeftStickCenter, PadRadius, PadRadius);
        context.DrawEllipse(colors.Pad, colors.Outline, FacePadCenter, PadRadius, PadRadius);
        context.DrawEllipse(colors.Pad, colors.Outline, DpadCenter, MountRadius, MountRadius);
        context.DrawEllipse(colors.Pad, colors.Outline, RightStickCenter, MountRadius, MountRadius);
        DrawStick(context, colors, LeftStickCenter, Axis(AxisLx), Axis(AxisLy), Button(ButtonLs));
        DrawStick(context, colors, RightStickCenter, Axis(AxisRx), Axis(AxisRy), Button(ButtonRs));
        DrawDpad(context, colors, Axis(AxisDpadX), Axis(AxisDpadY));
        DrawFaces(context, colors);
        DrawSystemButtons(context, colors);
    }

    private bool Button(int index) => (Buttons is { } buttons) && (index < buttons.Count) && buttons[index];

    private double Axis(int index) => (Axes is { } axes) && (index < axes.Count) && Double.IsFinite(axes[index]) ? axes[index] : 0d;

    private Palette ResolvePalette()
    {
        var body = ColorOf(BodyFill, DefaultBody);
        var stroke = ColorOf(Stroke, DefaultStroke);
        if ((palette is null) || !palette.Matches(body, stroke, PressedFill, Foreground, FaceColors))
        {
            palette = new Palette(body, stroke, PressedFill, Foreground, FaceColors);
        }

        return palette;
    }

    private static void DrawShadow(DrawingContext context, Palette colors)
    {
        for (var i = 1; i <= ShadowLayers; i++)
        {
            using (context.PushTransform(Matrix.CreateTranslation(0, i * ShadowStep)))
            {
                context.DrawGeometry(colors.Drop, null, BodyGeometry);
            }
        }
    }

    private static void DrawPlate(DrawingContext context, Palette colors)
    {
        context.DrawGeometry(colors.Body, null, BodyGeometry);
        using (context.PushGeometryClip(BodyGeometry))
        {
            using (context.PushTransform(Matrix.CreateTranslation(0, EdgeOffset)))
            {
                context.DrawGeometry(null, colors.Highlight, BodyGeometry);
            }

            using (context.PushTransform(Matrix.CreateTranslation(0, -EdgeOffset)))
            {
                context.DrawGeometry(null, colors.Shadow, BodyGeometry);
            }
        }
    }

    private static void DrawTrigger(DrawingContext context, Palette colors, StreamGeometry geometry, Rect rect, Point labelCenter, int label, double value)
    {
        var ratio = Math.Clamp((value + 1) / 2, 0d, 1d);
        context.DrawGeometry(colors.Trigger, null, geometry);
        DrawText(context, colors.Labels[label], labelCenter, LabelSizes[label]);
        if (ratio > 0.01)
        {
            var area = new Rect(rect.X, rect.Y + (TriggerVisible * (1 - ratio)), rect.Width, rect.Height);
            using (context.PushGeometryClip(geometry))
            using (context.PushClip(area))
            {
                context.DrawRectangle(colors.Pressed, null, area);
                DrawText(context, colors.PressedLabels[label], labelCenter, LabelSizes[label]);
            }
        }

        context.DrawGeometry(null, colors.Outline, geometry);
    }

    private static void DrawBumper(DrawingContext context, Palette colors, StreamGeometry geometry, Point labelCenter, int label, bool pressed)
    {
        context.DrawGeometry(pressed ? colors.Pressed : colors.Bumper, colors.Seam, geometry);
        DrawText(context, pressed ? colors.PressedLabels[label] : colors.Labels[label], labelCenter, LabelSizes[label]);
    }

    private static void DrawStick(DrawingContext context, Palette colors, Point center, double x, double y, bool pressed)
    {
        context.DrawEllipse(colors.Well, colors.WellRim, center, WellRadius, WellRadius);
        var dx = Math.Clamp(x, -1d, 1d);
        var dy = Math.Clamp(y, -1d, 1d);
        var magnitude = Math.Sqrt((dx * dx) + (dy * dy));
        if (magnitude > 1)
        {
            dx /= magnitude;
            dy /= magnitude;
            magnitude = 1;
        }

        var moved = magnitude > Deadzone;
        if (moved)
        {
            using (context.PushTransform(Matrix.CreateRotation(Math.Atan2(dy, dx)) * Matrix.CreateTranslation(center.X, center.Y)))
            using (context.PushOpacity(TiltBaseOpacity + ((1 - TiltBaseOpacity) * magnitude)))
            {
                context.DrawGeometry(null, colors.Tilt, TiltArc);
            }
        }

        var highlighted = pressed || moved;
        var cap = new Point(center.X + (dx * StickTravel), center.Y + (dy * StickTravel));
        context.DrawEllipse(pressed ? colors.Pressed : colors.Cap, highlighted ? colors.PressedRim : colors.CapRim, cap, CapRadius, CapRadius);
        context.DrawEllipse(pressed ? colors.PressedTop : colors.CapTop, null, cap, CapTopRadius, CapTopRadius);
    }

    private static void DrawDpad(DrawingContext context, Palette colors, double x, double y)
    {
        using (context.PushTransform(Matrix.CreateTranslation(DpadCenter.X, DpadCenter.Y)))
        {
            context.DrawGeometry(colors.KeyFill, null, DpadCross);
            DrawArm(context, colors, 0, x > PressThreshold);
            DrawArm(context, colors, 1, y > PressThreshold);
            DrawArm(context, colors, 2, x < -PressThreshold);
            DrawArm(context, colors, 3, y < -PressThreshold);
            context.DrawGeometry(null, colors.Outline, DpadCross);
        }
    }

    private static void DrawArm(DrawingContext context, Palette colors, int index, bool pressed)
    {
        if (pressed)
        {
            using (context.PushGeometryClip(DpadCross))
            {
                context.DrawRectangle(colors.Pressed, null, ArmRects[index]);
            }
        }

        context.DrawGeometry(pressed ? WhiteBrush : colors.Mark, null, DpadArrows[index]);
    }

    private void DrawFaces(DrawingContext context, Palette colors)
    {
        for (var i = 0; i < FaceCenters.Length; i++)
        {
            var face = colors.Faces[i];
            var pressed = Button(FaceButtons[i]);
            context.DrawEllipse(pressed ? face.Fill : colors.KeyFill, pressed ? face.Ring : colors.KeyRim, FaceCenters[i], FaceRadius, FaceRadius);
            DrawText(context, pressed ? face.PressedLetter : face.Letter, FaceCenters[i], LetterSize);
        }
    }

    private void DrawSystemButtons(DrawingContext context, Palette colors)
    {
        DrawPill(context, colors, BackCenter, Button(ButtonBack));
        DrawPill(context, colors, StartCenter, Button(ButtonStart));
        DrawText(context, colors.Labels[LabelBack], BackLabel, SmallLabelSize);
        DrawText(context, colors.Labels[LabelStart], StartLabel, SmallLabelSize);

        var guide = Button(ButtonGuide);
        context.DrawEllipse(guide ? colors.Pressed : colors.KeyFill, guide ? colors.PressedRim : colors.KeyRim, GuideCenter, GuideRadius, GuideRadius);
        using (context.PushTransform(Matrix.CreateTranslation(GuideCenter.X, GuideCenter.Y)))
        {
            context.DrawGeometry(guide ? WhiteBrush : colors.Mark, null, HouseGlyph);
        }
    }

    private static void DrawPill(DrawingContext context, Palette colors, Point center, bool pressed)
    {
        var rect = new Rect(center.X - (PillWidth / 2), center.Y - (PillHeight / 2), PillWidth, PillHeight);
        context.DrawRectangle(pressed ? colors.Pressed : colors.KeyFill, pressed ? colors.PressedRim : colors.KeyRim, rect, PillHeight / 2, PillHeight / 2);
    }

    private static void DrawText(DrawingContext context, FormattedText text, Point center, double size) =>
        context.DrawText(text, new Point(center.X - (text.Width / 2), center.Y - text.Baseline + (size * CapCenter)));

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    private static double Luminance(Color color) => ((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255;

    private static Color Shade(Color color, double amount) => amount >= 0 ? Mix(color, Colors.White, amount) : Mix(color, Colors.Black, -amount);

    private static Color Mix(Color color, Color target, double amount) =>
        Color.FromArgb(color.A, Lerp(color.R, target.R, amount), Lerp(color.G, target.G, amount), Lerp(color.B, target.B, amount));

    private static byte Lerp(byte from, byte to, double amount) => (byte)Math.Round(from + ((to - from) * amount));

    private static Color WithAlpha(Color color, double alpha) => Color.FromArgb((byte)Math.Round(alpha * 255), color.R, color.G, color.B);

    private static Point Map(Point point, bool mirror) => mirror ? new Point(DesignWidth - point.X, point.Y) : point;

    private static StreamGeometry BuildMirrored(Curve half) => BuildPath((half, false, false), (half, true, true));

    private static StreamGeometry BuildSide(bool mirror, params (Curve Curve, bool Reverse)[] pieces)
    {
        var mapped = new (Curve Curve, bool Reverse, bool Mirror)[pieces.Length];
        for (var i = 0; i < pieces.Length; i++)
        {
            mapped[i] = (pieces[i].Curve, pieces[i].Reverse, mirror);
        }

        return BuildPath(mapped);
    }

    private static StreamGeometry BuildPath(params (Curve Curve, bool Reverse, bool Mirror)[] pieces)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(pieces[0].Curve.Origin(pieces[0].Reverse, pieces[0].Mirror));
        foreach (var (curve, reverse, mirror) in pieces)
        {
            curve.Append(context, reverse, mirror);
        }

        context.EndFigure(true);

        return geometry;
    }

    private static StreamGeometry BuildRoundedRect(Rect rect, double radius)
    {
        var corner = new Size(radius, radius);
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(rect.Left + radius, rect.Top));
        context.LineTo(new Point(rect.Right - radius, rect.Top));
        context.ArcTo(new Point(rect.Right, rect.Top + radius), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(rect.Right, rect.Bottom - radius));
        context.ArcTo(new Point(rect.Right - radius, rect.Bottom), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(rect.Left + radius, rect.Bottom));
        context.ArcTo(new Point(rect.Left, rect.Bottom - radius), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(rect.Left, rect.Top + radius));
        context.ArcTo(new Point(rect.Left + radius, rect.Top), corner, 0, false, SweepDirection.Clockwise);
        context.EndFigure(true);

        return geometry;
    }

    private static StreamGeometry BuildCross(double half, double length, double radius)
    {
        var corner = new Size(radius, radius);
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(-half, -half));
        context.LineTo(new Point(-half, -length + radius));
        context.ArcTo(new Point(-half + radius, -length), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(half - radius, -length));
        context.ArcTo(new Point(half, -length + radius), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(half, -half));
        context.LineTo(new Point(length - radius, -half));
        context.ArcTo(new Point(length, -half + radius), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(length, half - radius));
        context.ArcTo(new Point(length - radius, half), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(half, half));
        context.LineTo(new Point(half, length - radius));
        context.ArcTo(new Point(half - radius, length), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(-half + radius, length));
        context.ArcTo(new Point(-half, length - radius), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(-half, half));
        context.LineTo(new Point(-length + radius, half));
        context.ArcTo(new Point(-length, half - radius), corner, 0, false, SweepDirection.Clockwise);
        context.LineTo(new Point(-length, -half + radius));
        context.ArcTo(new Point(-length + radius, -half), corner, 0, false, SweepDirection.Clockwise);
        context.EndFigure(true);

        return geometry;
    }

    private static StreamGeometry BuildArrow(double degrees)
    {
        var rotation = Matrix.CreateRotation(Matrix.ToRadians(degrees));
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(ArmLength * ArrowTip, 0).Transform(rotation));
        context.LineTo(new Point(ArmLength * ArrowBase, -ArmLength * ArrowHalf).Transform(rotation));
        context.LineTo(new Point(ArmLength * ArrowBase, ArmLength * ArrowHalf).Transform(rotation));
        context.EndFigure(true);

        return geometry;
    }

    private static StreamGeometry BuildArc(double radius, double halfAngle)
    {
        var angle = Matrix.ToRadians(halfAngle);
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(radius * Math.Cos(-angle), radius * Math.Sin(-angle)), false);
        context.ArcTo(new Point(radius * Math.Cos(angle), radius * Math.Sin(angle)), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        context.EndFigure(false);

        return geometry;
    }

    private static StreamGeometry BuildHouse()
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(0, -1.7));
        context.LineTo(new Point(1.8, 0));
        context.LineTo(new Point(1.25, 0));
        context.LineTo(new Point(1.25, 1.5));
        context.LineTo(new Point(-1.25, 1.5));
        context.LineTo(new Point(-1.25, 0));
        context.LineTo(new Point(-1.8, 0));
        context.EndFigure(true);

        return geometry;
    }

    private sealed class Curve
    {
        public Point Start { get; }

        public Point[] Points { get; }

        public Point End => Points[^1];

        public Curve(Point start, params Point[] points)
        {
            Start = start;
            Points = points;
        }

        public Curve Slice(int first, int last) => new(first == 0 ? Start : Points[(first * 3) - 1], Points[(first * 3)..((last + 1) * 3)]);

        public Point Origin(bool reverse, bool mirror) => Map(reverse ? End : Start, mirror);

        public void Append(StreamGeometryContext context, bool reverse, bool mirror)
        {
            if (reverse)
            {
                for (var i = Points.Length - 3; i >= 0; i -= 3)
                {
                    var previous = i >= 3 ? Points[i - 1] : Start;
                    context.CubicBezierTo(Map(Points[i + 1], mirror), Map(Points[i], mirror), Map(previous, mirror));
                }
            }
            else
            {
                for (var i = 0; i < Points.Length; i += 3)
                {
                    context.CubicBezierTo(Map(Points[i], mirror), Map(Points[i + 1], mirror), Map(Points[i + 2], mirror));
                }
            }
        }
    }

    private sealed class Palette
    {
        private static readonly RelativePoint TopCenter = new(0.5, 0, RelativeUnit.Relative);

        private static readonly RelativePoint BottomCenter = new(0.5, 1, RelativeUnit.Relative);

        private static readonly RelativePoint Center = new(0.5, 0.5, RelativeUnit.Relative);

        private static readonly RelativePoint LightSource = new(0.36, 0.3, RelativeUnit.Relative);

        private static readonly RelativeScalar Half = new(0.5, RelativeUnit.Relative);

        private readonly Color bodyColor;

        private readonly Color strokeColor;

        private readonly IBrush? pressedSource;

        private readonly IBrush? foregroundSource;

        private readonly IReadOnlyList<Color>? faceSource;

        public ImmutableLinearGradientBrush Body { get; }

        public ImmutableLinearGradientBrush Bumper { get; }

        public ImmutableLinearGradientBrush Trigger { get; }

        public ImmutableRadialGradientBrush Pad { get; }

        public ImmutableSolidColorBrush Drop { get; }

        public ImmutablePen Outline { get; }

        public ImmutablePen Seam { get; }

        public ImmutablePen Highlight { get; }

        public ImmutablePen Shadow { get; }

        public ImmutableRadialGradientBrush Well { get; }

        public ImmutablePen WellRim { get; }

        public ImmutableRadialGradientBrush Cap { get; }

        public ImmutableRadialGradientBrush CapTop { get; }

        public ImmutablePen CapRim { get; }

        public ImmutableSolidColorBrush KeyFill { get; }

        public ImmutablePen KeyRim { get; }

        public ImmutableSolidColorBrush Mark { get; }

        public IBrush Pressed { get; }

        public ImmutableSolidColorBrush PressedTop { get; }

        public ImmutablePen PressedRim { get; }

        public ImmutablePen Tilt { get; }

        public FormattedText[] Labels { get; }

        public FormattedText[] PressedLabels { get; }

        public Face[] Faces { get; }

        public Palette(Color body, Color stroke, IBrush? pressed, IBrush? foreground, IReadOnlyList<Color>? faces)
        {
            bodyColor = body;
            strokeColor = stroke;
            pressedSource = pressed;
            foregroundSource = foreground;
            faceSource = faces;

            var dark = Luminance(body) < 0.5;
            Body = new ImmutableLinearGradientBrush([new(0, Shade(body, dark ? 0.09 : 0.45)), new(0.55, body), new(1, Shade(body, -0.1))], startPoint: TopCenter, endPoint: BottomCenter);
            Bumper = new ImmutableLinearGradientBrush([new(0, Shade(body, dark ? 0.12 : 0.2)), new(1, Shade(body, dark ? 0.02 : -0.08))], startPoint: TopCenter, endPoint: BottomCenter);
            Trigger = new ImmutableLinearGradientBrush([new(0, Shade(body, dark ? -0.02 : 0.05)), new(1, Shade(body, dark ? -0.16 : -0.2))], startPoint: TopCenter, endPoint: BottomCenter);
            Pad = new ImmutableRadialGradientBrush([new(0, Shade(body, dark ? 0.06 : 0.1)), new(1, Shade(body, dark ? -0.05 : -0.08))], center: Center, gradientOrigin: LightSource, radiusX: Half, radiusY: Half);
            Drop = new ImmutableSolidColorBrush(WithAlpha(Colors.Black, dark ? 0.14 : 0.06));
            Outline = new ImmutablePen(new ImmutableSolidColorBrush(WithAlpha(stroke, 0.7)), 0.45, lineJoin: PenLineJoin.Round);
            Seam = new ImmutablePen(new ImmutableSolidColorBrush(WithAlpha(dark ? Colors.Black : Shade(body, -0.45), dark ? 0.45 : 0.9)), 0.3, lineJoin: PenLineJoin.Round);
            Highlight = new ImmutablePen(new ImmutableSolidColorBrush(WithAlpha(Colors.White, dark ? 0.1 : 0.7)), 0.6, lineJoin: PenLineJoin.Round);
            Shadow = new ImmutablePen(new ImmutableSolidColorBrush(WithAlpha(Colors.Black, dark ? 0.35 : 0.1)), 0.6, lineJoin: PenLineJoin.Round);
            Well = new ImmutableRadialGradientBrush([new(0, Shade(body, dark ? -0.18 : -0.12)), new(0.72, Shade(body, dark ? -0.2 : -0.14)), new(1, Shade(body, dark ? -0.42 : -0.32))], center: Center, gradientOrigin: Center, radiusX: Half, radiusY: Half);
            WellRim = new ImmutablePen(new ImmutableSolidColorBrush(Shade(body, dark ? -0.5 : -0.38)), 0.35);

            var cap = dark ? Shade(body, -0.3) : Shade(body, -0.72);
            Cap = new ImmutableRadialGradientBrush([new(0, Shade(cap, 0.22)), new(0.65, cap), new(1, Shade(cap, -0.25))], center: Center, gradientOrigin: LightSource, radiusX: Half, radiusY: Half);
            CapTop = new ImmutableRadialGradientBrush([new(0, Shade(cap, -0.14)), new(1, Shade(cap, 0.04))], center: Center, gradientOrigin: Center, radiusX: Half, radiusY: Half);
            CapRim = new ImmutablePen(new ImmutableSolidColorBrush(Shade(cap, 0.22)), 1.1);
            KeyFill = new ImmutableSolidColorBrush(dark ? Shade(body, -0.32) : Shade(body, 0.7));
            KeyRim = new ImmutablePen(new ImmutableSolidColorBrush(dark ? Shade(body, -0.02) : Shade(body, -0.25)), 0.4);

            var foregroundColor = ColorOf(foreground, DefaultForeground);
            Mark = new ImmutableSolidColorBrush(WithAlpha(foregroundColor, 0.85));

            var pressedColor = ColorOf(pressed, DefaultPressed);
            Pressed = pressed is null or ISolidColorBrush ? new ImmutableSolidColorBrush(pressedColor) : pressed;
            PressedTop = new ImmutableSolidColorBrush(Shade(pressedColor, -0.12));
            PressedRim = new ImmutablePen(new ImmutableSolidColorBrush(Shade(pressedColor, 0.35)), 0.6);
            Tilt = new ImmutablePen(new ImmutableSolidColorBrush(pressedColor), 1.4, lineCap: PenLineCap.Round);

            var labelBrush = foreground ?? new ImmutableSolidColorBrush(foregroundColor);
            Labels = new FormattedText[LabelTexts.Length];
            PressedLabels = new FormattedText[LabelTexts.Length];
            for (var i = 0; i < LabelTexts.Length; i++)
            {
                Labels[i] = ChartHelper.CreateText(LabelTexts[i], LabelSizes[i], labelBrush, ChartHelper.BoldTypeface);
                PressedLabels[i] = ChartHelper.CreateText(LabelTexts[i], LabelSizes[i], WhiteBrush, ChartHelper.BoldTypeface);
            }

            Faces = new Face[FaceTexts.Length];
            for (var i = 0; i < FaceTexts.Length; i++)
            {
                Faces[i] = new Face((faces is not null) && (i < faces.Count) ? faces[i] : Colors.Gray, FaceTexts[i], dark);
            }
        }

        public bool Matches(Color body, Color stroke, IBrush? pressed, IBrush? foreground, IReadOnlyList<Color>? faces) =>
            (bodyColor == body) && (strokeColor == stroke) && ReferenceEquals(pressedSource, pressed) && ReferenceEquals(foregroundSource, foreground) && ReferenceEquals(faceSource, faces);
    }

    private sealed class Face
    {
        public ImmutableSolidColorBrush Fill { get; }

        public ImmutablePen Ring { get; }

        public FormattedText Letter { get; }

        public FormattedText PressedLetter { get; }

        public Face(Color color, string text, bool dark)
        {
            Fill = new ImmutableSolidColorBrush(color);
            Ring = new ImmutablePen(new ImmutableSolidColorBrush(Shade(color, 0.35)), 0.4);
            var ink = dark ? Fill : new ImmutableSolidColorBrush(Shade(color, -0.15));
            Letter = ChartHelper.CreateText(text, LetterSize, ink, ChartHelper.BoldTypeface);
            PressedLetter = ChartHelper.CreateText(text, LetterSize, Luminance(color) > 0.6 ? InkBrush : WhiteBrush, ChartHelper.BoldTypeface);
        }
    }
}
