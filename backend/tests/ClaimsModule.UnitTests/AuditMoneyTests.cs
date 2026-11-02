using System.Globalization;
using ClaimsModule.Application.Common;

namespace ClaimsModule.UnitTests;

public class AuditMoneyTests
{
    [Fact]
    public void Formats_dollars_whatever_the_host_culture_is()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // The docker image runs culture-invariant, where "C" renders "¤9,000.00".
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            Assert.Equal("$9,000.00", AuditMoney.Format(9_000m));
            Assert.Equal("-$2,000.50", AuditMoney.Format(-2_000.5m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
