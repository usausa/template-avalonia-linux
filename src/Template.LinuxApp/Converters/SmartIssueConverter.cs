namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

using Template.LinuxApp.Domain.Logic;

public sealed class SmartIssueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not SmartIssue issue
            ? String.Empty
            : issue.Type switch
            {
                SmartIssueType.CriticalWarning => $"Critical warning: {SmartValueConverter.FormatCriticalWarning((byte)issue.Value)}",
                SmartIssueType.SpareBelowThreshold => String.Create(CultureInfo.InvariantCulture, $"Available spare {issue.Value}% is below {issue.Limit}%"),
                SmartIssueType.MediaErrors => String.Create(CultureInfo.InvariantCulture, $"Media errors: {issue.Value:N0}"),
                SmartIssueType.PercentageUsed => String.Create(CultureInfo.InvariantCulture, $"Percentage used: {issue.Value}%"),
                SmartIssueType.ErrorLogEntries => String.Create(CultureInfo.InvariantCulture, $"Error log entries: {issue.Value:N0}"),
                SmartIssueType.UncorrectableSectors => String.Create(CultureInfo.InvariantCulture, $"Offline uncorrectable sectors: {issue.Value:N0}"),
                SmartIssueType.ReportedUncorrectableErrors => String.Create(CultureInfo.InvariantCulture, $"Reported uncorrectable errors: {issue.Value:N0}"),
                SmartIssueType.ReallocatedSectors => String.Create(CultureInfo.InvariantCulture, $"Reallocated sectors: {issue.Value:N0}"),
                SmartIssueType.PendingSectors => String.Create(CultureInfo.InvariantCulture, $"Pending sectors: {issue.Value:N0}"),
                SmartIssueType.ReallocationEvents => String.Create(CultureInfo.InvariantCulture, $"Reallocation events: {issue.Value:N0}"),
                _ => String.Empty
            };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
