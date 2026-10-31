using System.Globalization;
using EmissionLedger.Domain;

namespace EmissionLedger.Tests;

public class UnitsTests
{
    [Theory]
    [InlineData("1", "MWh", "kWh", "1000")]
    [InlineData("3.6", "GJ", "kWh", "1000")]
    [InlineData("2", "m3", "l", "2000")]
    [InlineData("500", "kWh", "MWh", "0.5")]
    public void Converts_within_a_dimension(string quantity, string from, string to, string expected)
    {
        var result = Units.Convert(decimal.Parse(quantity, CultureInfo.InvariantCulture), from, to);

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), result);
    }

    [Fact]
    public void Refuses_to_convert_across_dimensions()
    {
        Assert.Throws<ArgumentException>(() => Units.Convert(10, "l", "kWh"));
    }

    [Fact]
    public void Unit_names_are_case_insensitive()
    {
        Assert.True(Units.IsKnown("mwh"));
        Assert.True(Units.SameDimension("KWH", "gj"));
    }
}
