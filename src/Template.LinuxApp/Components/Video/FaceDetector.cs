namespace Template.LinuxApp.Components.Video;

using System.Diagnostics.CodeAnalysis;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using Template.LinuxApp.State;

public readonly record struct FaceBox(float Left, float Top, float Right, float Bottom, float Confidence);

public interface IFaceDetector
{
    bool IsEnabled { get; }

    void Detect(ReadOnlySpan<byte> image, int width, int height, ICollection<FaceBox> results);
}

public sealed class FaceDetector : IFaceDetector, IDisposable
{
    private const float ConfidenceThreshold = 0.7f;

    private const float IouThreshold = 0.3f;

    private readonly Lock sync = new();

    private readonly FaceDetectorOption option;

    private readonly DeviceStatus status;

    private InferenceSession? session;

    private bool failed;

    private string inputName = string.Empty;

    private int[] dimensions = [];

    private int modelWidth;

    private int modelHeight;

    private float[] inputBuffer = [];

    public bool IsEnabled => status.IsEnabled;

    public FaceDetector(FaceDetectorOption option, DeviceState deviceState)
    {
        this.option = option;
        status = deviceState.Register("Face detector", option.Enable);
    }

    public void Dispose()
    {
        lock (sync)
        {
            session?.Dispose();
            session = null;

            if (inputBuffer.Length > 0)
            {
                ArrayPool<float>.Shared.Return(inputBuffer);
                inputBuffer = [];
            }
        }
    }

    public void Detect(ReadOnlySpan<byte> image, int width, int height, ICollection<FaceBox> results)
    {
        results.Clear();
        if (!status.IsEnabled)
        {
            return;
        }

        lock (sync)
        {
            if (!LoadSession())
            {
                return;
            }

            var size = 3 * modelWidth * modelHeight;
            if ((width == modelWidth) && (height == modelHeight))
            {
                CopyToTensor(image, inputBuffer.AsSpan(0, size), width, height);
            }
            else
            {
                ResizeToTensor(image, inputBuffer.AsSpan(0, size), width, height, modelWidth, modelHeight);
            }

            var tensor = new DenseTensor<float>(inputBuffer.AsMemory(0, size), dimensions);
            using var outputs = session.Run([NamedOnnxValue.CreateFromTensor(inputName, tensor)]);
            var scores = outputs[0].AsTensor<float>();
            var boxes = outputs[1].AsTensor<float>();
            var count = scores.Dimensions[1];
            if (count > 0)
            {
                var candidates = ArrayPool<FaceBox>.Shared.Rent(count);
                try
                {
                    var found = 0;
                    for (var i = 0; i < count; i++)
                    {
                        var score = scores[0, i, 1];
                        if (score > ConfidenceThreshold)
                        {
                            candidates[found++] = new FaceBox(boxes[0, i, 0], boxes[0, i, 1], boxes[0, i, 2], boxes[0, i, 3], score);
                        }
                    }

                    ApplyNms(candidates.AsSpan(0, found), results);
                }
                finally
                {
                    ArrayPool<FaceBox>.Shared.Return(candidates);
                }
            }
        }

        status.ReportEvent();
    }

    [MemberNotNullWhen(true, nameof(session))]
    private bool LoadSession()
    {
        if (session is not null)
        {
            return true;
        }

        if (failed)
        {
            return false;
        }

        try
        {
            using var sessionOptions = new SessionOptions();
            sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR;
            sessionOptions.EnableCpuMemArena = true;
            sessionOptions.EnableMemoryPattern = true;
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            sessionOptions.ExecutionMode = option.Parallel ? ExecutionMode.ORT_PARALLEL : ExecutionMode.ORT_SEQUENTIAL;
            sessionOptions.IntraOpNumThreads = option.IntraOpNumThreads;
            sessionOptions.InterOpNumThreads = option.InterOpNumThreads;
            session = new InferenceSession(Path.Combine(AppContext.BaseDirectory, option.Model), sessionOptions);
        }
        catch (Exception ex) when (ex is OnnxRuntimeException or IOException or DllNotFoundException or TypeInitializationException)
        {
            failed = true;
            status.ReportError(ex.Message);
            return false;
        }

        var input = session.InputMetadata.First();
        if (input.Value.Dimensions.Length < 4)
        {
            session.Dispose();
            session = null;
            failed = true;
            status.ReportError("Invalid model.");
            return false;
        }

        inputName = input.Key;
        modelHeight = input.Value.Dimensions[2];
        modelWidth = input.Value.Dimensions[3];
        dimensions = [1, 3, modelHeight, modelWidth];
        inputBuffer = ArrayPool<float>.Shared.Rent(3 * modelWidth * modelHeight);
        status.ReportConnected();
        return true;
    }

    private static void ApplyNms(Span<FaceBox> boxes, ICollection<FaceBox> results)
    {
        if (boxes.IsEmpty)
        {
            return;
        }

        boxes.Sort(static (x, y) => y.Confidence.CompareTo(x.Confidence));

        var suppressed = ArrayPool<bool>.Shared.Rent(boxes.Length);
        try
        {
            suppressed.AsSpan(0, boxes.Length).Clear();
            for (var i = 0; i < boxes.Length; i++)
            {
                if (suppressed[i])
                {
                    continue;
                }

                var current = boxes[i];
                results.Add(current);

                for (var j = i + 1; j < boxes.Length; j++)
                {
                    if (!suppressed[j] && (CalculateIou(current, boxes[j]) >= IouThreshold))
                    {
                        suppressed[j] = true;
                    }
                }
            }
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(suppressed);
        }
    }

    private static float CalculateIou(FaceBox box1, FaceBox box2)
    {
        var x1 = Math.Max(box1.Left, box2.Left);
        var y1 = Math.Max(box1.Top, box2.Top);
        var x2 = Math.Min(box1.Right, box2.Right);
        var y2 = Math.Min(box1.Bottom, box2.Bottom);

        var intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var box1Area = (box1.Right - box1.Left) * (box1.Bottom - box1.Top);
        var box2Area = (box2.Right - box2.Left) * (box2.Bottom - box2.Top);
        var unionArea = box1Area + box2Area - intersectionArea;

        return unionArea > 0 ? intersectionArea / unionArea : 0;
    }

    //--------------------------------------------------------------------------------
    // Copy & Resize
    //--------------------------------------------------------------------------------

    private static void CopyToTensor(ReadOnlySpan<byte> source, Span<float> destination, int width, int height)
    {
        var channelSize = width * height;
        var gOffset = channelSize;
        var bOffset = channelSize * 2;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var srcIndex = ((y * width) + x) * 4;
                var dstIndex = (y * width) + x;

                destination[dstIndex] = (source[srcIndex] - 127f) / 128f;
                destination[gOffset + dstIndex] = (source[srcIndex + 1] - 127f) / 128f;
                destination[bOffset + dstIndex] = (source[srcIndex + 2] - 127f) / 128f;
            }
        }
    }

    private static void ResizeToTensor(ReadOnlySpan<byte> source, Span<float> destination, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
    {
        var xRatio = (float)(srcWidth - 1) / dstWidth;
        var yRatio = (float)(srcHeight - 1) / dstHeight;
        var channelSize = dstWidth * dstHeight;
        var gOffset = channelSize;
        var bOffset = channelSize * 2;

        for (var y = 0; y < dstHeight; y++)
        {
            var srcY = y * yRatio;
            var srcYInt = (int)srcY;
            var yDiff = srcY - srcYInt;
            var yDiffInv = 1.0f - yDiff;
            var srcY1 = Math.Min(srcYInt + 1, srcHeight - 1);

            var srcRow0 = srcYInt * srcWidth * 4;
            var srcRow1 = srcY1 * srcWidth * 4;

            for (var x = 0; x < dstWidth; x++)
            {
                var srcX = x * xRatio;
                var srcXInt = (int)srcX;
                var xDiff = srcX - srcXInt;
                var xDiffInv = 1.0f - xDiff;
                var srcX1 = Math.Min(srcXInt + 1, srcWidth - 1);

                var idx00 = srcRow0 + (srcXInt * 4);
                var idx10 = srcRow0 + (srcX1 * 4);
                var idx01 = srcRow1 + (srcXInt * 4);
                var idx11 = srcRow1 + (srcX1 * 4);

                var w00 = xDiffInv * yDiffInv;
                var w10 = xDiff * yDiffInv;
                var w01 = xDiffInv * yDiff;
                var w11 = xDiff * yDiff;

                var dstIndex = (y * dstWidth) + x;

                var r = (source[idx00] * w00) + (source[idx10] * w10) + (source[idx01] * w01) + (source[idx11] * w11);
                destination[dstIndex] = (r - 127f) / 128f;
                var g = (source[idx00 + 1] * w00) + (source[idx10 + 1] * w10) + (source[idx01 + 1] * w01) + (source[idx11 + 1] * w11);
                destination[gOffset + dstIndex] = (g - 127f) / 128f;
                var b = (source[idx00 + 2] * w00) + (source[idx10 + 2] * w10) + (source[idx01 + 2] * w01) + (source[idx11 + 2] * w11);
                destination[bOffset + dstIndex] = (b - 127f) / 128f;
            }
        }
    }
}
