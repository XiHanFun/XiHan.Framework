// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Xunit;

namespace XiHan.Framework.Kernel.Tests;

public class XiHanExceptionTests
{
    [Fact]
    public void ToString_ShouldIncludeCodeAndStackTrace()
    {
        var ex = new XiHanException("E001", "something failed");
        var str = ex.ToString();
        Assert.Contains("[E001]", str);
        Assert.Contains("something failed", str);
    }

    [Fact]
    public void ToString_WithInnerException_ShouldIncludeInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new XiHanException("E002", "wrapped", inner);
        var str = ex.ToString();
        Assert.Contains("[E002]", str);
        Assert.Contains("inner", str);
    }
}
