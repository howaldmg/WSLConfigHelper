using WSLConfigHelper.Core.Guardrails;
using WSLConfigHelper.Core.Parsing;
using WSLConfigHelper.Core.SystemInfo;
using Xunit;

namespace WSLConfigHelper.Core.Tests;

public class GuardrailEngineTests
{
    private readonly HostHardwareMetrics _mockHost = new(
        TotalPhysicalMemoryBytes: 32UL * 1024 * 1024 * 1024, // 32 GB
        AvailablePhysicalMemoryBytes: 20UL * 1024 * 1024 * 1024,
        LogicalProcessors: 16,
        OsDescription: "Microsoft Windows 11 Enterprise",
        IsWindows: true
    );

    [Fact]
    public void WarnsWhenMemoryIsLessThan2GB()
    {
        var ini = """
            [wsl2]
            memory=1GB
            """;
        var doc = IniDocument.Parse(ini);
        var engine = new GuardrailEngine();

        var issues = engine.Evaluate(doc, _mockHost);
        Assert.Contains(issues, i => i.Key == "memory" && i.Severity == GuardrailSeverity.Warning && i.Message.Contains("less than 2GB"));
    }

    [Fact]
    public void WarnsWhenMemoryExceeds85PercentOfHost()
    {
        var ini = """
            [wsl2]
            memory=30GB
            """;
        var doc = IniDocument.Parse(ini);
        var engine = new GuardrailEngine();

        var issues = engine.Evaluate(doc, _mockHost);
        Assert.Contains(issues, i => i.Key == "memory" && i.Severity == GuardrailSeverity.Warning && i.Message.Contains("risks starving Windows"));
    }

    [Fact]
    public void WarnsWhenProcessorsExceedHostCores()
    {
        var ini = """
            [wsl2]
            processors=24
            """;
        var doc = IniDocument.Parse(ini);
        var engine = new GuardrailEngine();

        var issues = engine.Evaluate(doc, _mockHost);
        Assert.Contains(issues, i => i.Key == "processors" && i.Severity == GuardrailSeverity.Warning && i.Message.Contains("exceeds total physical host"));
    }

    [Fact]
    public void RecommendsLeavingCoresWhenAllCoresAssigned()
    {
        var ini = """
            [wsl2]
            processors=16
            """;
        var doc = IniDocument.Parse(ini);
        var engine = new GuardrailEngine();

        var issues = engine.Evaluate(doc, _mockHost);
        Assert.Contains(issues, i => i.Key == "processors" && i.Severity == GuardrailSeverity.Recommendation && i.Message.Contains("stuttering"));
    }

    [Fact]
    public void WarnsWhenSwapIsGreaterThanMemory()
    {
        var ini = """
            [wsl2]
            memory=8GB
            swap=16GB
            """;
        var doc = IniDocument.Parse(ini);
        var engine = new GuardrailEngine();

        var issues = engine.Evaluate(doc, _mockHost);
        Assert.Contains(issues, i => i.Key == "swap" && i.Severity == GuardrailSeverity.Warning && i.Message.Contains("larger than VM memory"));
    }

    [Fact]
    public void RecommendsDnsTunnelingWhenMirroredNetworkingEnabled()
    {
        var ini = """
            [experimental]
            networkingMode=mirrored
            """;
        var doc = IniDocument.Parse(ini);
        var engine = new GuardrailEngine();

        var issues = engine.Evaluate(doc, _mockHost);
        Assert.Contains(issues, i => i.Key == "dnsTunneling" && i.Severity == GuardrailSeverity.Recommendation);
    }
}
