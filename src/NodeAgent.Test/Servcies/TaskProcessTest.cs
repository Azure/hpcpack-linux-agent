using NodeAgent.Services;

namespace NodeAgent.Test.Servcies;

public class TaskProcessTest
{
    /*
     * NOTE
     *
     * The lines of results (including the empty lines) are deliberately selected. 
     * Be careful when you change them.
     */
    [Fact]
    public void TestParseStatisticsResult()
    {
        var result =
@"


".Replace("\r\n", "\n");

        var stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(0ul, stat.UserTimeMs);
        Assert.Equal(0ul, stat.KernelTimeMs);
        Assert.Equal(0ul, stat.WorkingSetKb);
        Assert.Empty(stat.ProcessIds);

        result = 
@"1



".Replace("\r\n", "\n");

        stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(10ul, stat.UserTimeMs);
        Assert.Equal(0ul, stat.KernelTimeMs);
        Assert.Equal(0ul, stat.WorkingSetKb);
        Assert.Empty(stat.ProcessIds);

        result =
@"
1


".Replace("\r\n", "\n");

        stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(0ul, stat.UserTimeMs);
        Assert.Equal(10ul, stat.KernelTimeMs);
        Assert.Equal(0ul, stat.WorkingSetKb);
        Assert.Empty(stat.ProcessIds);

        result =
@"

1025

".Replace("\r\n", "\n");

        stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(0ul, stat.UserTimeMs);
        Assert.Equal(0ul, stat.KernelTimeMs);
        Assert.Equal(1ul, stat.WorkingSetKb);
        Assert.Empty(stat.ProcessIds);

        result =
@"


1   2

3   4


".Replace("\r\n", "\n");

        stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(0ul, stat.UserTimeMs);
        Assert.Equal(0ul, stat.KernelTimeMs);
        Assert.Equal(0ul, stat.WorkingSetKb);
        Assert.Equal([1, 2, 3, 4], stat.ProcessIds.ToArray());
    }

    [Fact]
    public void TestParseStatisticsResult2()
    {
        var result =
@"

".Replace("\r\n", "\n");

        Assert.Throws<FormatException>(() =>
        {
            TaskProcess.ParseStatisticsResult(result);
        });

        result =
@"x



".Replace("\r\n", "\n");

        Assert.Throws<FormatException>(() =>
        {
            TaskProcess.ParseStatisticsResult(result);
        });
    }
}
