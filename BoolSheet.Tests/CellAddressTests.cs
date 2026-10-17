using BoolSheet.Core.Model;
using Xunit;

namespace BoolSheet.Tests;

public class CellAddressTests
{
    [Theory]
    [InlineData("A1", 0, 0)]
    [InlineData("Z1", 25, 0)]
    [InlineData("AA1", 26, 0)]
    [InlineData("az10", 51, 9)]
    public void TryParse_ValidAddress_ReturnsZeroBasedCoordinates(string text, int column, int row)
    {
        Assert.True(CellAddress.TryParse(text, out CellAddress address));
        Assert.Equal(column, address.Column);
        Assert.Equal(row, address.Row);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1A")]
    [InlineData("A0")]
    [InlineData("A-1")]
    [InlineData("A 1")]
    [InlineData("Ж1")]
    public void TryParse_InvalidAddress_ReturnsFalse(string text)
    {
        Assert.False(CellAddress.TryParse(text, out _));
    }

    [Theory]
    [InlineData("a1", "A1")]
    [InlineData("az10", "AZ10")]
    [InlineData("ZZ99", "ZZ99")]
    public void ToString_ReturnsNormalizedAddress(string text, string expected)
    {
        Assert.Equal(expected, CellAddress.Parse(text).ToString());
    }
}
