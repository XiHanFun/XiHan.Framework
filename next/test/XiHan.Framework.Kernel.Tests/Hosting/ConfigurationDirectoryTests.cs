// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 挂载配置目录的文件解析：基础文件优先、其余按序数、忽略点开头条目与非 JSON、目录缺失为空。
/// </summary>
public sealed class ConfigurationDirectoryTests
{
    [Fact]
    public void Resolve_OrdersBaseFileFirst_AndSkipsHiddenEntries()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xihan-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var name in new[] { "appsettings.Production.json", "appsettings.json", "..data.json", "zz.json", "notes.txt", "Aaa.json" })
            {
                File.WriteAllText(Path.Combine(directory, name), "{}");
            }

            Assert.Equal(["appsettings.json", "Aaa.json", "appsettings.Production.json", "zz.json"], ConfigurationDirectory.Resolve(directory).Select(Path.GetFileName));
            Assert.Empty(ConfigurationDirectory.Resolve(Path.Combine(directory, "missing")));
            Assert.Empty(ConfigurationDirectory.Resolve(string.Empty));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
