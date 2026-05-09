// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Xunit;

namespace XiHan.Framework.Kernel.Tests;

public class XiHanErrorTests
{
    [Fact]
    public void Unexpected_ShouldHaveCorrectCode()
    {
        var error = XiHanError.Unexpected("something went wrong");
        Assert.Equal("UNEXPECTED", error.Code);
        Assert.Equal("something went wrong", error.Message);
    }

    [Fact]
    public void Validation_ShouldHaveCorrectCode()
    {
        var error = XiHanError.Validation("name is required");
        Assert.Equal("VALIDATION", error.Code);
    }

    [Fact]
    public void NotFound_ShouldHaveCorrectCode()
    {
        var error = XiHanError.NotFound("user not found");
        Assert.Equal("NOT_FOUND", error.Code);
    }

    [Fact]
    public void CustomCode_ShouldWork()
    {
        var error = new XiHanError("CUSTOM_001", "custom error", new InvalidOperationException("inner"));
        Assert.Equal("CUSTOM_001", error.Code);
        Assert.NotNull(error.InnerException);
    }

    [Fact]
    public void NullCode_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new XiHanError(null!, "msg"));
    }

    [Fact]
    public void ToString_ShouldIncludeCodeAndMessage()
    {
        var error = XiHanError.Unexpected("boom");
        Assert.Equal("[UNEXPECTED] boom", error.ToString());
    }
}
