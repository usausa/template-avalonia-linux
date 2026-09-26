namespace Template.LinuxApp.Components.Video;

internal sealed class BufferSlot
{
    private readonly BufferManager manager;

    private readonly byte[] buffer;

    private readonly int bufferSize;

    public int SlotNo { get; }

    public Lock Lock { get; } = new();

    public Collection<FaceBox> FaceBoxes { get; } = [];

    public Span<byte> Buffer => buffer.AsSpan(0, bufferSize);

    public BufferSlot(BufferManager manager, int slotNo, byte[] buffer, int bufferSize)
    {
        this.manager = manager;
        SlotNo = slotNo;
        buffer.AsSpan().Clear();
        this.buffer = buffer;
        this.bufferSize = bufferSize;
    }

    public void MarkUpdated() => manager.NotifyUpdate(SlotNo);
}

internal sealed class BufferManager : IDisposable
{
    private readonly Lock indexLock = new();

    private readonly BufferSlot[] slots;

    private byte[][] buffers;

    private int nextIndex;

    private int lastUpdatedIndex = -1;

    public int Width { get; }

    public int Height { get; }

    public int BufferSize { get; }

    public BufferManager(int slotCount, int width, int height, int depth)
    {
        Width = width;
        Height = height;
        BufferSize = width * height * depth;

        slots = new BufferSlot[slotCount];
        buffers = new byte[slotCount][];
        for (var i = 0; i < slotCount; i++)
        {
            buffers[i] = ArrayPool<byte>.Shared.Rent(BufferSize);
            slots[i] = new BufferSlot(this, i, buffers[i], BufferSize);
        }
    }

    public void Dispose()
    {
        foreach (var buffer in buffers)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        buffers = [];
    }

    public BufferSlot NextSlot()
    {
        lock (indexLock)
        {
            var slot = slots[nextIndex];
            nextIndex = (nextIndex + 1) % slots.Length;
            return slot;
        }
    }

    public BufferSlot? LastUpdatedSlot()
    {
        lock (indexLock)
        {
            return lastUpdatedIndex < 0 ? null : slots[lastUpdatedIndex];
        }
    }

    internal void NotifyUpdate(int slotNo)
    {
        lock (indexLock)
        {
            lastUpdatedIndex = slotNo;
        }
    }
}
