using WSLConfigHelper.Core.Parsing;
using Xunit;

namespace WSLConfigHelper.Core.Tests;

public class IniDocumentTests
{
    [Fact]
    public void PreservesCommentsAndBlankLinesLosslessly()
    {
        var ini = """
            # Global WSL2 configuration file
            ; Created by admin

            [wsl2]
            # Core memory limit
            memory=16GB
            processors=8 # Leave cores for host

            [experimental]
            # Mirrored mode is best for corporate networks
            networkingMode=mirrored
            sparseVhd=true
            """;

        var doc = IniDocument.Parse(ini);
        var serialized = doc.Serialize();

        // Standardize line endings for assertion
        Assert.Equal(ini.Replace("\r\n", "\n"), serialized.Replace("\r\n", "\n"));
    }

    [Fact]
    public void ModifyingExistingKeyPreservesSurroundingComments()
    {
        var ini = """
            [wsl2]
            # Keep memory safe
            memory=8GB
            # Processor setting
            processors=4
            """;

        var doc = IniDocument.Parse(ini);
        doc.SetValue("wsl2", "memory", "32GB");

        var serialized = doc.Serialize();
        Assert.Contains("# Keep memory safe", serialized);
        Assert.Contains("memory=32GB", serialized);
        Assert.Contains("# Processor setting", serialized);
        Assert.Contains("processors=4", serialized);
    }

    [Fact]
    public void AddingKeyToExistingSectionInsertsBeforeNextSection()
    {
        var ini = """
            [wsl2]
            memory=16GB

            [experimental]
            networkingMode=mirrored
            """;

        var doc = IniDocument.Parse(ini);
        doc.SetValue("wsl2", "processors", "6");

        var serialized = doc.Serialize().Replace("\r\n", "\n");
        var expected = """
            [wsl2]
            memory=16GB
            processors=6

            [experimental]
            networkingMode=mirrored
            """.Replace("\r\n", "\n");

        Assert.Equal(expected, serialized);
    }

    [Fact]
    public void AddingNewSectionAppendsToEnd()
    {
        var ini = """
            [wsl2]
            memory=16GB
            """;

        var doc = IniDocument.Parse(ini);
        doc.SetValue("experimental", "sparseVhd", "true");

        var serialized = doc.Serialize();
        Assert.Contains("[wsl2]", serialized);
        Assert.Contains("[experimental]", serialized);
        Assert.Contains("sparseVhd=true", serialized);
    }

    [Fact]
    public void CaseInsensitiveKeyAndSectionLookup()
    {
        var ini = """
            [WSL2]
            Memory=16GB
            """;

        var doc = IniDocument.Parse(ini);
        Assert.True(doc.HasSection("wsl2"));
        Assert.True(doc.HasKey("wsl2", "memory"));
        Assert.Equal("16GB", doc.GetValue("wsl2", "memory"));

        doc.SetValue("wsl2", "MEMORY", "24GB");
        Assert.Equal("24GB", doc.GetValue("WSL2", "memory"));
    }

    [Fact]
    public void PreservesCustomOrUnknownKeys()
    {
        var ini = """
            [wsl2]
            customSuperSecretOption=xyz123
            """;

        var doc = IniDocument.Parse(ini);
        doc.SetValue("wsl2", "memory", "16GB");

        var serialized = doc.Serialize();
        Assert.Contains("customSuperSecretOption=xyz123", serialized);
        Assert.Contains("memory=16GB", serialized);
    }
}
