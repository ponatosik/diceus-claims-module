using System.Globalization;
using ClaimsModule.Application.Common;

namespace ClaimsModule.UnitTests;

public class UsdAmountTests
{
    [Fact]
    public void Formats_dollars_whatever_the_host_culture_is()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // The docker image runs culture-invariant, where "C" renders "¤9,000.00".
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            Assert.Equal("$9,000.00", UsdAmount.Format(9_000m));
            Assert.Equal("-$2,000.50", UsdAmount.Format(-2_000.5m));
            Assert.Equal("$0.00", UsdAmount.Format(0m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
