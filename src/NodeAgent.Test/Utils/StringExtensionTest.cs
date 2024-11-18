using NodeAgent.Utils;

namespace NodeAgent.Test.Utils;

public class StringExtensionTest
{
    [Theory]
    [InlineData("abcd", "abcd", true)]
    [InlineData("abcd", "Abcd", false)]
    [InlineData("abcd", "a*", true)]
    [InlineData("abcd", "ab*", true)]
    [InlineData("abcd", "a*c", false)]
    [InlineData("abcd", "*bc", false)]
    [InlineData("abcd", "*b*", true)]
    [InlineData("abcd", "**b*", true)]
    [InlineData("abcd", "*", true)]
    public void TestAsteriskMatch(string str, string patternStr, bool expected)
    {
        var result = str.AsteriskMatch(patternStr);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("abc 123.45 def", 123.45f)]
    [InlineData("abc 100 def", 100.0f)]
    [InlineData("N/A", 0.0f)]
    public void TestRemoveMeasurement(string str, float expected)
    {
        var result = str.RemoveMeasurement();
        Assert.Equal(expected, result);
    }
}

