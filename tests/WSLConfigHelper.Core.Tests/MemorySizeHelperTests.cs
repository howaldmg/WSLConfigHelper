using WSLConfigHelper.Core.Models;
using Xunit;

namespace WSLConfigHelper.Core.Tests;

public class MemorySizeHelperTests
{
    [Theory]
    [InlineData("16GB", 16UL * 1024 * 1024 * 1024)]
    [InlineData("16G", 16UL * 1024 * 1024 * 1024)]
    [InlineData("4096MB", 4096UL * 1024 * 1024)]
    [InlineData("4096M", 4096UL * 1024 * 1024)]
    [InlineData("0", 0UL)]
    [InlineData("1.5GB", (ulong)(1.5 * 1024 * 1024 * 1024))]
    public void SuccessfullyParsesValidMemoryStrings(string input, ulong expectedBytes)
    {
        var success = MemorySizeHelper.TryParseToBytes(input, out ulong bytes);
        Assert.True(success);
        Assert.Equal(expectedBytes, bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("-5GB")]
    [InlineData("GB")]
    public void FailsOnInvalidMemoryStrings(string input)
    {
        var success = MemorySizeHelper.TryParseToBytes(input, out _);
        Assert.False(success);
    }

    [Fact]
    public void FormatsBytesCorrectly()
    {
        Assert.Equal("16GB", MemorySizeHelper.FormatBytes(16UL * 1024 * 1024 * 1024));
        Assert.Equal("512MB", MemorySizeHelper.FormatBytes(512UL * 1024 * 1024));
        Assert.Equal("0", MemorySizeHelper.FormatBytes(0));
    }
}
