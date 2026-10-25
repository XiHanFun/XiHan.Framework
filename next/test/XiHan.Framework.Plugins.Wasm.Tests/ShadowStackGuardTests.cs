// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Wasmtime;
using XiHan.Framework.Plugins.Wasm.Instances;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 影子栈检查：测试进程关闭了 CET 兼容，陷阱要表现为异常而不是进程终止。
/// </summary>
public sealed class ShadowStackGuardTests
{
    [Fact]
    public void TestHost_DoesNotEnforceShadowStacks_SoTrapsSurfaceAsExceptions()
    {
        Assert.False(ShadowStackGuard.IsEnforced());
        ShadowStackGuard.EnsureTrapsAreSafe();

        using var engine = new Engine();
        using var module = Module.FromText(engine, "probe", "(module (func (export \"boom\") unreachable))");
        using var store = new Store(engine);
        using var linker = new Linker(engine);
        var boom = linker.Instantiate(store, module).GetAction("boom")!;

        var trap = Assert.Throws<TrapException>(() => boom());
        Assert.Equal(TrapCode.Unreachable, trap.Type);
    }
}
