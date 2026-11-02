using System.Globalization;

namespace ClaimsModule.Application.Common;

/// <summary>
/// Money as written into permanent audit descriptions: always US dollars (§6.3's thresholds are
/// dollar amounts), never the host's culture — a culture-invariant container would otherwise write
/// "¤9,000.00" into the append-only ClaimAuditLog, where it can never be corrected.
/// </summary>
public static class AuditMoney
{
    private static readonly CultureInfo UsDollars = CultureInfo.GetCultureInfo("en-US");

    public static string Format(decimal amount) => amount.ToString("C", UsDollars);
}
