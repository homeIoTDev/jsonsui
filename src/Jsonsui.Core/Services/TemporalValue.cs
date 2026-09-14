using System.Globalization;
using System.Text.RegularExpressions;
using Jsonsui.Core.Models;

namespace Jsonsui.Core.Services;

public static class TemporalValue
{
    private static readonly Regex OffsetSuffixRegex = new("(Z|z|[+-]\\d{2}:\\d{2})$", RegexOptions.Compiled);

    public static void Apply(FieldRow row, string fieldType, string? json)
    {
        row.ScalarValue = json ?? "";
        row.DateValue = null;
        row.TimeValue = null;
        row.UseSeconds = false;
        row.HasOffset = false;
        row.Offset = TimeSpan.Zero;
        row.IsUtcSuffix = false;

        if (string.IsNullOrEmpty(json)) return;

        switch (fieldType)
        {
            case "date":
                if (DateOnly.TryParse(json, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
                    row.DateValue = new DateTimeOffset(dateOnly.Year, dateOnly.Month, dateOnly.Day, 0, 0, 0, TimeSpan.Zero);
                break;

            case "time":
                if (TimeSpan.TryParse(json, CultureInfo.InvariantCulture, out var time))
                {
                    row.TimeValue = time;
                    row.UseSeconds = json.Count(c => c == ':') >= 2;
                }
                break;

            case "date-time":
                if (DateTimeOffset.TryParse(json, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                {
                    var match = OffsetSuffixRegex.Match(json);
                    row.DateValue = dto;
                    row.TimeValue = dto.TimeOfDay;
                    row.UseSeconds = HasTimeSeconds(json);
                    row.HasOffset = match.Success;
                    row.Offset = dto.Offset;
                    row.IsUtcSuffix = match.Success && match.Value is "Z" or "z";
                }
                break;
        }
    }

    public static string? FormatDate(DateTimeOffset? date)
        => date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

    public static string? FormatTime(TimeSpan? time, bool useSeconds)
    {
        if (!time.HasValue) return null;
        var t = time.Value;
        var hours = (int)t.TotalHours;
        return useSeconds
            ? $"{hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{hours:D2}:{t.Minutes:D2}";
    }

    public static string? FormatDateTime(DateTimeOffset? date, TimeSpan? time,
        bool useSeconds, bool hasOffset, bool isUtcSuffix, TimeSpan offset)
    {
        if (!date.HasValue || !time.HasValue) return null;

        var d = date.Value;
        var t = time.Value;
        var hours = (int)t.TotalHours;
        var dto = new DateTimeOffset(d.Year, d.Month, d.Day, hours, t.Minutes, t.Seconds,
            hasOffset ? offset : TimeSpan.Zero);

        var body = dto.ToString(useSeconds ? "yyyy-MM-dd'T'HH:mm:ss" : "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

        if (isUtcSuffix) return body + "Z";
        return hasOffset ? body + dto.ToString("zzz", CultureInfo.InvariantCulture) : body;
    }

    private static bool HasTimeSeconds(string json)
    {
        var core = OffsetSuffixRegex.Replace(json, "");
        var tIndex = core.IndexOf('T');
        if (tIndex < 0) return false;
        return core[(tIndex + 1)..].Count(c => c == ':') >= 2;
    }

    /// <summary>
    /// Kanonisiert eine manuell eingegebene Zeichenkette für den Feldtyp.
    /// Liefert null bei ungültiger Eingabe, "" bei leerer Eingabe.
    /// </summary>
    public static string? Normalize(string fieldType, string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        switch (fieldType)
        {
            case "date":
                if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
                    return dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return null;

            case "time":
                if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var time) && time.TotalHours < 24)
                    return text.Count(c => c == ':') >= 2
                        ? $"{((int)time.TotalHours):D2}:{time.Minutes:D2}:{time.Seconds:D2}"
                        : $"{((int)time.TotalHours):D2}:{time.Minutes:D2}";
                return null;

            case "date-time":
                var probe = new FieldRow();
                Apply(probe, "date-time", text);
                if (probe.DateValue == null || probe.TimeValue == null) return null;
                return FormatDateTime(probe.DateValue, probe.TimeValue, probe.UseSeconds, probe.HasOffset, probe.IsUtcSuffix, probe.Offset);
        }

        return text;
    }

    public static string? Format(FieldRow row, string fieldType) => fieldType switch
    {
        "date" => FormatDate(row.DateValue),
        "time" => FormatTime(row.TimeValue, row.UseSeconds),
        "date-time" => FormatDateTime(row.DateValue, row.TimeValue, row.UseSeconds, row.HasOffset, row.IsUtcSuffix, row.Offset),
        _ => row.ScalarValue
    };
}
