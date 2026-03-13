namespace AllaganSync.Tracking;

public static class AddonTextHelper
{
    public static int? ParseNumericText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var cleaned = text.Replace(",", "").Replace(".", "").Replace(" ", "");
        if (int.TryParse(cleaned, out var value) && value > 0)
            return value;

        return null;
    }
}
