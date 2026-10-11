// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.CommandLine;

namespace XiHan.Framework.Utils.Tests.CommandLine;

/// <summary>
/// 脚本执行助手测试
/// </summary>
public class ScriptExecutorTests
{
    /// <summary>
    /// 脚本路径含空格时 Shell 脚本仍能执行并收到参数
    /// </summary>
    [Fact]
    public void ExecuteScript_ShellScriptPathWithSpaces_RunsScript()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Shell 脚本需要 /bin/bash，仅在 Linux/macOS 上验证。");

        var directory = Path.Combine(Path.GetTempPath(), $"xihan script {Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var scriptPath = Path.Combine(directory, "echo args.sh");
            File.WriteAllText(scriptPath, "#!/bin/bash\necho \"arg:$1\"\n");

            var output = ScriptExecutor.ExecuteScript(scriptPath, "hello");

            Assert.Equal("arg:hello", output.Trim());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// 脚本路径含双引号与反斜杠时 Shell 脚本仍能执行
    /// </summary>
    [Fact]
    public void ExecuteScript_ShellScriptPathWithQuoteAndBackslash_RunsScript()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Shell 脚本需要 /bin/bash，仅在 Linux/macOS 上验证。");

        var directory = Path.Combine(Path.GetTempPath(), $"xihan \"q\\ {Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var scriptPath = Path.Combine(directory, "run.sh");
            File.WriteAllText(scriptPath, "#!/bin/bash\necho ok\n");

            var output = ScriptExecutor.ExecuteScript(scriptPath);

            Assert.Equal("ok", output.Trim());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
