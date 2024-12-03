using JetBrains.Annotations;
using XiHan.Framework.Utils.CommandLine;

namespace XiHan.Framework.Utils.Test.CommandLine;

[TestSubject(typeof(ScriptExecutor))]
public class ScriptExecutorTest
{
    [Fact]
    public void ExecuteScript_ThrowsArgumentException_WhenScriptFilePathIsNull()
    {
        _ = Assert.Throws<ArgumentException>(() => ScriptExecutor.ExecuteScript(null));
    }

    [Fact]
    public void ExecuteScript_ThrowsArgumentException_WhenScriptFilePathIsEmpty()
    {
        _ = Assert.Throws<ArgumentException>(() => ScriptExecutor.ExecuteScript(string.Empty));
    }

    [Fact]
    public void ExecuteScript_ThrowsArgumentException_WhenScriptFileDoesNotExist()
    {
        _ = Assert.Throws<ArgumentException>(() => ScriptExecutor.ExecuteScript("nonexistent.sh"));
    }

    [Fact]
    public void ExecuteScript_ThrowsNotSupportedException_WhenScriptFileTypeIsUnsupported()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "echo Hello World");
        _ = Assert.Throws<NotSupportedException>(() => ScriptExecutor.ExecuteScript(tempFile));
        File.Delete(tempFile);
    }
}
