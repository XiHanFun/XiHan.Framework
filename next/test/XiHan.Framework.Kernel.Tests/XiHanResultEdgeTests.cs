// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Xunit;

namespace XiHan.Framework.Kernel.Tests;

public class XiHanResultEdgeTests
{
    [Fact]
    public void Failure_WithNullError_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => XiHanResult.Failure<int>(null!));
    }

    [Fact]
    public void DefaultResult_ShouldHaveIsSuccessFalse()
    {
        var result = default(XiHanResult<int>);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void DefaultResult_AccessingError_ShouldThrow()
    {
        var result = default(XiHanResult<int>);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Some_WithNullValue_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => XiHanMaybe.Some<string>(null!));
    }
}
