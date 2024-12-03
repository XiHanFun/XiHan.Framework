using JetBrains.Annotations;
using XiHan.Framework.Utils.CommandLine;

namespace XiHan.Framework.Utils.Test.CommandLine;

[TestSubject(typeof(ShellHelper))]
public class ShellHelperTest
{
    [Fact]
    public void Bash_ReturnsOutput_WhenCommandExecutesSuccessfully()
    {
        var result = ShellHelper.Bash("echo Hello World");
        Assert.Contains("Hello World", result);
    }

    [Fact]
    public void Bash_ReturnsEmptyString_WhenProcessFailsToStart()
    {
        var result = ShellHelper.Bash("nonexistentcommand");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Cmd_ReturnsOutput_WhenCommandExecutesSuccessfully()
    {
        var result = ShellHelper.Cmd("cmd.exe", "/c echo Hello World");
        Assert.Contains("Hello World", result);
    }

    [Fact]
    public void Cmd_ReturnsEmptyString_WhenProcessFailsToStart()
    {
        var result = ShellHelper.Cmd("nonexistentfile.exe", "");
        Assert.Equal(string.Empty, result);
    }
}
