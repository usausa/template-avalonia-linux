namespace Template.LinuxApp.Components.Platform;

public sealed record UsbDevice(
    string Port,
    int Bus,
    int Address,
    string VendorId,
    string ProductId,
    string Manufacturer,
    string Product,
    string SerialNumber,
    double Speed,
    string Version,
    string Class,
    int MaxPower,
    IReadOnlyList<string> Drivers,
    IReadOnlyList<string> DeviceFiles);

public static class UsbDeviceReader
{
    private const string DevicesPath = "/sys/bus/usb/devices";

    private const int SearchDepth = 6;

    public static IReadOnlyList<UsbDevice> GetDevices()
    {
        if (!Directory.Exists(DevicesPath))
        {
            return [];
        }

        var names = Directory.EnumerateFileSystemEntries(DevicesPath)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToList();
        var devices = new List<UsbDevice>();
        foreach (var name in names)
        {
            if (name.Contains(':', StringComparison.Ordinal) || name.StartsWith("usb", StringComparison.Ordinal))
            {
                continue;
            }

            var device = ReadDevice(name, names);
            if (device is not null)
            {
                devices.Add(device);
            }
        }

        devices.Sort(static (x, y) => ComparePort(x.Port, y.Port));
        return devices;
    }

    private static UsbDevice? ReadDevice(string name, IEnumerable<string> names)
    {
        var path = Path.Combine(DevicesPath, name);
        var vendorId = ReadText(path, "idVendor");
        if (vendorId.Length == 0)
        {
            return null;
        }

        var interfaceClasses = new List<int>();
        var drivers = new List<string>();
        var deviceFiles = new List<string>();
        foreach (var interfaceName in names.Where(x => x.StartsWith(name + ":", StringComparison.Ordinal)))
        {
            var interfacePath = Path.Combine(DevicesPath, interfaceName);
            if (ReadHex(interfacePath, "bInterfaceClass") is { } interfaceClass)
            {
                interfaceClasses.Add(interfaceClass);
            }

            if (ReadLinkName(Path.Combine(interfacePath, "driver")) is { } driver)
            {
                drivers.Add(driver);
            }

            if (ResolveDirectory(interfacePath) is { } directory)
            {
                CollectDeviceFiles(directory, SearchDepth, deviceFiles);
            }
        }

        return new UsbDevice(
            name,
            ReadInt(path, "busnum"),
            ReadInt(path, "devnum"),
            vendorId,
            ReadText(path, "idProduct"),
            ReadText(path, "manufacturer"),
            ReadText(path, "product"),
            ReadText(path, "serial"),
            Double.TryParse(ReadText(path, "speed"), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) ? speed : Double.NaN,
            ReadText(path, "version"),
            ResolveClass(ReadHex(path, "bDeviceClass") ?? 0, interfaceClasses),
            Int32.TryParse(ReadText(path, "bMaxPower").TrimEnd('m', 'A'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var power) ? power : 0,
            [.. drivers.Distinct(StringComparer.Ordinal)],
            [.. deviceFiles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    private static void CollectDeviceFiles(DirectoryInfo directory, int depth, List<string> files)
    {
        IEnumerable<DirectoryInfo> children;
        try
        {
            children = directory.EnumerateDirectories().ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var child in children)
        {
            if (child.LinkTarget is not null)
            {
                continue;
            }

            if (ToDeviceFile(directory.Name, child.Name) is { } file)
            {
                files.Add(file);
            }
            else if (depth > 0)
            {
                CollectDeviceFiles(child, depth - 1, files);
            }
        }
    }

    private static string? ToDeviceFile(string parent, string name) =>
        parent switch
        {
            "video4linux" or "hidraw" or "block" => $"/dev/{name}",
            "tty" when name.StartsWith("tty", StringComparison.Ordinal) => $"/dev/{name}",
            "usbmisc" => $"/dev/usb/{name}",
            "net" => name,
            "sound" when name.StartsWith("card", StringComparison.Ordinal) => name,
            _ when parent.StartsWith("input", StringComparison.Ordinal) && IsInputNode(name) => $"/dev/input/{name}",
            _ => null
        };

    private static bool IsInputNode(string name) =>
        name.StartsWith("event", StringComparison.Ordinal) ||
        name.StartsWith("js", StringComparison.Ordinal) ||
        name.StartsWith("mouse", StringComparison.Ordinal);

    private static string ResolveClass(int deviceClass, IEnumerable<int> interfaceClasses)
    {
        if ((deviceClass != 0x00) && (deviceClass != 0xEF))
        {
            return GetClassName(deviceClass);
        }

        var names = interfaceClasses
            .Where(static x => x != 0x0A)
            .Distinct()
            .Select(GetClassName)
            .ToList();
        return names.Count > 0 ? String.Join(", ", names) : GetClassName(deviceClass);
    }

    private static string GetClassName(int code) =>
        code switch
        {
            0x00 => "Device",
            0x01 => "Audio",
            0x02 => "Communications",
            0x03 => "HID",
            0x05 => "Physical",
            0x06 => "Image",
            0x07 => "Printer",
            0x08 => "Mass storage",
            0x09 => "Hub",
            0x0A => "CDC data",
            0x0B => "Smart card",
            0x0D => "Content security",
            0x0E => "Video",
            0x0F => "Healthcare",
            0x10 => "Audio/Video",
            0x11 => "Billboard",
            0x12 => "Type-C bridge",
            0xDC => "Diagnostic",
            0xE0 => "Wireless",
            0xEF => "Miscellaneous",
            0xFE => "Application",
            0xFF => "Vendor specific",
            _ => code.ToString("X2", CultureInfo.InvariantCulture)
        };

    private static int ComparePort(string x, string y)
    {
        var left = ParsePort(x);
        var right = ParsePort(y);
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var result = left[i].CompareTo(right[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static int[] ParsePort(string port) =>
        [.. port.Split('-', '.').Select(static x => Int32.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0)];

    private static string ReadText(string path, string name)
    {
        try
        {
            var file = Path.Combine(path, name);
            return File.Exists(file) ? File.ReadAllText(file).Trim() : String.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return String.Empty;
        }
    }

    private static int ReadInt(string path, string name) =>
        Int32.TryParse(ReadText(path, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static int? ReadHex(string path, string name) =>
        Int32.TryParse(ReadText(path, name), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static DirectoryInfo? ResolveDirectory(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            return directory.ResolveLinkTarget(true) as DirectoryInfo ?? directory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadLinkName(string path)
    {
        try
        {
            return new DirectoryInfo(path).LinkTarget is { } target ? Path.GetFileName(target) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
