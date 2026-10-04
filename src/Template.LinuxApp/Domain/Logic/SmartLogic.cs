namespace Template.LinuxApp.Domain.Logic;

using System.Collections.Frozen;

using LinuxDotNet.Disk;

public enum SmartHealth
{
    Unknown,
    Good,
    Warning,
    Critical
}

public enum SmartValueUnit
{
    None,
    Count,
    Percent,
    Celsius,
    Minutes,
    Hours,
    Days,
    Bytes,
    CriticalWarning
}

public enum SmartIssueType
{
    CriticalWarning,
    SpareBelowThreshold,
    MediaErrors,
    PercentageUsed,
    ErrorLogEntries,
    UncorrectableSectors,
    ReportedUncorrectableErrors,
    ReallocatedSectors,
    PendingSectors,
    ReallocationEvents
}

public sealed record SmartIssue(SmartHealth Health, SmartIssueType Type, ulong Value, ulong Limit);

public static class SmartLogic
{
    public const double NvmeDataUnit = 512_000;

    private const double SectorSize = 512;

    private const double MinimumBytesPerHour = 1_000_000;

    private const ulong HoursMask = 0xFFFF_FFFF;

    private const short PreFailureFlag = 0x01;

    private const byte WearLevelingCount = 0xB1;

    private const byte AirflowTemperature = 0xBE;

    private const byte SsdLifeLeft = 0xE7;

    private const byte MediaWearoutIndicator = 0xE9;

    private const byte TotalLbasWritten = 0xF1;

    private const byte TotalLbasRead = 0xF2;

    private const int UsedWarning = 90;

    private const double LifeWarning = 10;

    private const double TemperatureWarning = 60;

    private const double TemperatureCritical = 70;

    private static readonly byte[] LifeIds =
    [
        SsdLifeLeft,
        WearLevelingCount,
        (byte)SmartId.AverageBlockEraseCount,
        (byte)SmartId.PercentageLifetimeRemaining,
        MediaWearoutIndicator
    ];

    private static readonly string[] CriticalWarningNames =
    [
        "Spare below threshold",
        "Temperature",
        "Reliability degraded",
        "Read only",
        "Backup failed",
        "PMR read only"
    ];

    private static readonly FrozenDictionary<byte, string> AttributeNames = new Dictionary<byte, string>
    {
        { 0x01, "Raw Read Error Rate" },
        { 0x02, "Throughput Performance" },
        { 0x03, "Spin Up Time" },
        { 0x04, "Start Stop Count" },
        { 0x05, "Reallocated Sector Count" },
        { 0x06, "Read Channel Margin" },
        { 0x07, "Seek Error Rate" },
        { 0x08, "Seek Time Performance" },
        { 0x09, "Power On Hours" },
        { 0x0A, "Spin Retry Count" },
        { 0x0B, "Calibration Retry Count" },
        { 0x0C, "Power Cycle Count" },
        { 0x0D, "Soft Read Error Rate" },
        { 0x16, "Current Helium Level" },
        { 0xAA, "Available Reserved Space" },
        { 0xAB, "Program Fail Count" },
        { 0xAC, "Erase Fail Count" },
        { 0xAD, "Wear Leveling Count" },
        { 0xAE, "Unexpected Power Loss Count" },
        { 0xAF, "Power Loss Protection Failure" },
        { 0xB0, "Erase Fail Count Chip" },
        { 0xB1, "Wear Range Delta" },
        { 0xB2, "Used Reserved Block Count Chip" },
        { 0xB3, "Used Reserved Block Count Total" },
        { 0xB4, "Unused Reserved Block Count Total" },
        { 0xB5, "Program Fail Count Total" },
        { 0xB6, "Erase Fail Count Total" },
        { 0xB7, "Runtime Bad Block" },
        { 0xB8, "End To End Error" },
        { 0xBB, "Reported Uncorrectable Errors" },
        { 0xBC, "Command Timeout" },
        { 0xBD, "High Fly Writes" },
        { 0xBE, "Airflow Temperature" },
        { 0xBF, "G-Sense Error Rate" },
        { 0xC0, "Power Off Retract Count" },
        { 0xC1, "Load Cycle Count" },
        { 0xC2, "Temperature" },
        { 0xC3, "Hardware ECC Recovered" },
        { 0xC4, "Reallocation Event Count" },
        { 0xC5, "Current Pending Sector Count" },
        { 0xC6, "Offline Uncorrectable" },
        { 0xC7, "UDMA CRC Error Count" },
        { 0xC8, "Multi Zone Error Rate" },
        { 0xC9, "Soft Read Error Rate" },
        { 0xCA, "Percent Lifetime Remaining" },
        { 0xCB, "Run Out Cancel" },
        { 0xCC, "Soft ECC Correction" },
        { 0xCD, "Thermal Asperity Rate" },
        { 0xCE, "Flying Height" },
        { 0xCF, "Spin High Current" },
        { 0xD0, "Spin Buzz" },
        { 0xD1, "Offline Seek Performance" },
        { 0xDC, "Disk Shift" },
        { 0xDD, "G-Sense Error Rate" },
        { 0xDE, "Loaded Hours" },
        { 0xDF, "Load Retry Count" },
        { 0xE0, "Load Friction" },
        { 0xE1, "Load Cycle Count" },
        { 0xE2, "Load In Time" },
        { 0xE3, "Torque Amplification Count" },
        { 0xE4, "Power Off Retract Count" },
        { 0xE6, "Head Amplitude" },
        { 0xE7, "SSD Life Left" },
        { 0xE8, "Endurance Remaining" },
        { 0xE9, "Media Wearout Indicator" },
        { 0xEA, "Average Erase Count" },
        { 0xEB, "Good Block Count" },
        { 0xF0, "Head Flying Hours" },
        { 0xF1, "Total LBAs Written" },
        { 0xF2, "Total LBAs Read" },
        { 0xF3, "Total LBAs Written Expanded" },
        { 0xF4, "Total LBAs Read Expanded" },
        { 0xF6, "Total Host Sector Writes" },
        { 0xF7, "Host Program Page Count" },
        { 0xF8, "FTL Program Page Count" },
        { 0xF9, "NAND Writes" },
        { 0xFA, "Read Error Retry Rate" },
        { 0xFB, "Minimum Spares Remaining" },
        { 0xFC, "Newly Added Bad Flash Block" },
        { 0xFE, "Free Fall Protection" }
    }.ToFrozenDictionary();

    public static string GetAttributeName(byte id) =>
        AttributeNames.TryGetValue(id, out var name) ? name : "Vendor Specific";

    public static bool IsPreFailure(SmartAttribute attribute) =>
        (attribute.Flags & PreFailureFlag) != 0;

    public static bool IsTemperature(byte id) =>
        id is (byte)SmartId.Temperature or AirflowTemperature;

    public static (ulong Current, ulong Minimum, ulong Maximum) GetTemperatureRange(ulong raw) =>
        (raw & 0xFF, (raw >> 16) & 0xFF, (raw >> 32) & 0xFF);

    public static IReadOnlyList<string> GetCriticalWarningNames(byte value) =>
        [.. CriticalWarningNames.Where((_, i) => (value & (1 << i)) != 0)];

    public static SmartAttribute? Find(IReadOnlyList<SmartAttribute> attributes, SmartId id) =>
        TryFind(attributes, (byte)id, out var attribute) ? attribute : null;

    public static double GetRaw(IReadOnlyList<SmartAttribute> attributes, SmartId id) =>
        TryFind(attributes, (byte)id, out var attribute) ? attribute.RawValue : Double.NaN;

    public static double GetPowerOnHours(IReadOnlyList<SmartAttribute> attributes) =>
        TryFind(attributes, (byte)SmartId.PowerOnHours, out var attribute) ? attribute.RawValue & HoursMask : Double.NaN;

    public static double GetTemperature(IReadOnlyList<SmartAttribute> attributes) =>
        TryFind(attributes, (byte)SmartId.Temperature, out var attribute) || TryFind(attributes, AirflowTemperature, out attribute)
            ? attribute.RawValue & 0xFF
            : Double.NaN;

    public static double GetLife(IReadOnlyList<SmartAttribute> attributes)
    {
        foreach (var id in LifeIds)
        {
            if (TryFind(attributes, id, out var attribute) && (attribute.CurrentValue <= 100))
            {
                return attribute.CurrentValue;
            }
        }

        return Double.NaN;
    }

    public static double GetDataWritten(IReadOnlyList<SmartAttribute> attributes) =>
        TryFind(attributes, (byte)SmartId.TotalHostSectorWrite, out var attribute)
            ? attribute.RawValue * SectorSize
            : GetLbaBytes(attributes, TotalLbasWritten);

    public static double GetDataRead(IReadOnlyList<SmartAttribute> attributes) =>
        GetLbaBytes(attributes, TotalLbasRead);

    public static double GetUnsafeShutdowns(IReadOnlyList<SmartAttribute> attributes) =>
        TryFind(attributes, (byte)SmartId.UnexpectedPowerLoss, out var attribute) || TryFind(attributes, (byte)SmartId.PowerOffRetractCount, out attribute)
            ? attribute.RawValue
            : Double.NaN;

    public static IReadOnlyList<SmartIssue> EvaluateAta(IReadOnlyList<SmartAttribute> attributes)
    {
        var issues = new List<SmartIssue>();
        AddIssue(issues, attributes, SmartId.UncorrectableSectorCount, SmartIssueType.UncorrectableSectors);
        AddIssue(issues, attributes, SmartId.ReportedUncorrectableErrors, SmartIssueType.ReportedUncorrectableErrors);
        AddIssue(issues, attributes, SmartId.ReallocatedSectorCount, SmartIssueType.ReallocatedSectors);
        AddIssue(issues, attributes, SmartId.CurrentPendingSectorCount, SmartIssueType.PendingSectors);
        AddIssue(issues, attributes, SmartId.ReallocationEventCount, SmartIssueType.ReallocationEvents);
        return issues;
    }

    public static IReadOnlyList<SmartIssue> EvaluateNvme(byte criticalWarning, byte spare, byte spareThreshold, byte percentageUsed, ulong mediaErrors, ulong errorLogEntries)
    {
        var issues = new List<SmartIssue>();
        if (criticalWarning != 0)
        {
            issues.Add(new SmartIssue(SmartHealth.Critical, SmartIssueType.CriticalWarning, criticalWarning, 0));
        }

        if (spare < spareThreshold)
        {
            issues.Add(new SmartIssue(SmartHealth.Critical, SmartIssueType.SpareBelowThreshold, spare, spareThreshold));
        }

        if (mediaErrors > 0)
        {
            issues.Add(new SmartIssue(SmartHealth.Critical, SmartIssueType.MediaErrors, mediaErrors, 0));
        }

        if (percentageUsed >= UsedWarning)
        {
            issues.Add(new SmartIssue(SmartHealth.Warning, SmartIssueType.PercentageUsed, percentageUsed, UsedWarning));
        }

        if (errorLogEntries > 0)
        {
            issues.Add(new SmartIssue(SmartHealth.Warning, SmartIssueType.ErrorLogEntries, errorLogEntries, 0));
        }

        return issues;
    }

    public static SmartHealth GetHealth(IEnumerable<SmartIssue> issues) =>
        issues.Select(static x => x.Health).DefaultIfEmpty(SmartHealth.Good).Max();

    public static SmartHealth GetAttributeHealth(SmartAttribute attribute) =>
        (SmartId)attribute.Id switch
        {
            SmartId.UncorrectableSectorCount or SmartId.ReportedUncorrectableErrors => attribute.RawValue > 0 ? SmartHealth.Critical : SmartHealth.Good,
            SmartId.ReallocatedSectorCount or SmartId.CurrentPendingSectorCount or SmartId.ReallocationEventCount => attribute.RawValue > 0 ? SmartHealth.Warning : SmartHealth.Good,
            _ => SmartHealth.Unknown
        };

    public static SmartHealth GetTemperatureHealth(double celsius) =>
        !Double.IsFinite(celsius)
            ? SmartHealth.Unknown
            : celsius >= TemperatureCritical
                ? SmartHealth.Critical
                : celsius >= TemperatureWarning ? SmartHealth.Warning : SmartHealth.Good;

    public static SmartHealth GetLifeHealth(double life) =>
        !Double.IsFinite(life) ? SmartHealth.Unknown : life <= LifeWarning ? SmartHealth.Warning : SmartHealth.Good;

    public static SmartHealth GetSpareHealth(byte spare, byte threshold) =>
        spare < threshold ? SmartHealth.Critical : SmartHealth.Good;

    private static void AddIssue(List<SmartIssue> issues, IEnumerable<SmartAttribute> attributes, SmartId id, SmartIssueType type)
    {
        if (TryFind(attributes, (byte)id, out var attribute) && (attribute.RawValue > 0))
        {
            issues.Add(new SmartIssue(GetAttributeHealth(attribute), type, attribute.RawValue, 0));
        }
    }

    private static double GetLbaBytes(IReadOnlyList<SmartAttribute> attributes, byte id)
    {
        if (!TryFind(attributes, id, out var attribute))
        {
            return Double.NaN;
        }

        var bytes = attribute.RawValue * SectorSize;
        var hours = GetPowerOnHours(attributes);
        return Double.IsFinite(hours) && (bytes >= hours * MinimumBytesPerHour) ? bytes : Double.NaN;
    }

    private static bool TryFind(IEnumerable<SmartAttribute> attributes, byte id, out SmartAttribute value)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.Id == id)
            {
                value = attribute;
                return true;
            }
        }

        value = default;
        return false;
    }
}
