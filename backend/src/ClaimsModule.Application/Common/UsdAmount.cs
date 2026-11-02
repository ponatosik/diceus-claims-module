using System.Globalization;

namespace ClaimsModule.Application.Common;

/// <summary>
/// Money as text — always US dollars (§6.3's thresholds are dollar amounts), never the host's
/// culture. Used for permanent audit descriptions and business-rule messages: a culture-invariant
/// host would otherwise write "¤9,000.00" into the append-only ClaimAuditLog. Built from a fixed
/// NumberFormatInfo rather than looking up "en-US", so it also works on ICU-less
/// (globalization-invariant) container images, where culture lookups throw.
/// </summary>
public static class UsdAmount
{
    private static readonly NumberFormatInfo UsDollars = CreateFormat();

    public static string Format(decimal amount) => amount.ToString("C", UsDollars);

    private static NumberFormatInfo CreateFormat()
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.CurrencySymbol = "$";
        format.CurrencyDecimalDigits = 2;
        format.CurrencyPositivePattern = 0; // $n
        format.CurrencyNegativePattern = 1; // -$n
        return NumberFormatInfo.ReadOnly(format);
    }
}
