// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// Wasm 插件的工作目录缺省按用户分开（ADR-0207）
/// </summary>
/// <remarks>
/// 安全审查 S-37：原先缺省是临时目录下名字固定的 <c>xihan-plugins-wasm</c>；目录的权限核对与进程外宿主共用，测试在进程外宿主那边
/// </remarks>
public sealed class WasmWorkDirectoryTests
{
    [Fact]
    public void DefaultRoot_IsPerUser_NotAFixedNameInTheSharedTemp()
    {
        var temp = Path.GetFullPath(Path.GetTempPath());

        Assert.NotEqual(Path.Combine(temp, "xihan-plugins-wasm"), new WasmPluginOptions().ResolveWorkDirectory());
        Assert.StartsWith(Path.Combine(temp, "xihan-plugins-wasm-"), new WasmPluginOptions().ResolveWorkDirectory(), StringComparison.Ordinal);
    }
}
