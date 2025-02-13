using NodeAgent.Models;
using Xunit.Abstractions;

namespace NodeAgent.Test;

public static class ITestOutputHelperExtensions
{
    public static void OutputTaskResult(this ITestOutputHelper testout, int? code, string? output, ProcessStatistics? stat)
    {
        var msg = $"""
======================================
OnComplete:
[Code]
{code}
[Stat]
{stat}
[Output]
{output}
======================================
""";
        testout.WriteLine(msg);
    }

    public static void OutputString(this ITestOutputHelper testout, string? output)
    {
        var msg = $"""
======================================
{output}
======================================
""";
        testout.WriteLine(msg);
    }

    public static void OutputObject(this ITestOutputHelper testout, object output)
    {
        var msg = $"""
======================================
{output.ToString()}
======================================
""";
        testout.WriteLine(msg);
    }
}
