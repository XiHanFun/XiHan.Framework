// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Validation;

/// <summary>
/// 查询修订号的核对
/// </summary>
/// <remarks>
/// 所需修订号不高于 Provider 支持的即成功，更高即 <c>KERNEL_VALIDATION_007</c> 并带上查询名与两个修订号；参数不合法是编程错误
/// </remarks>
public sealed class QueryRevisionTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    public void Check_Succeeds_WhenTheProviderKnowsEveryCriterion(int required, int supported)
    {
        Assert.True(QueryRevision.Check("AuditQuery", required, supported).IsSuccess);
    }

    [Fact]
    public void Check_FailsWithTheRevisions_WhenTheQueryUsesNewerCriteria()
    {
        var result = QueryRevision.Check("AuditQuery", 2, 1);

        var error = Assert.IsType<Error>(result.Error);
        Assert.Equal(KernelErrors.QueryRevisionUnsupported, error.Code);
        Assert.Equal(KernelMessages.QueryRevisionUnsupported, error.MessageKey);
        Assert.Equal("AuditQuery", error.Details?["query"]);
        Assert.Equal(2, error.Details?["required"]);
        Assert.Equal(1, error.Details?["supported"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Check_RejectsAMissingQueryName(string? query)
    {
        Assert.ThrowsAny<ArgumentException>(() => QueryRevision.Check(query!, 1, 1));
    }

    [Fact]
    public void Check_RejectsARequiredRevisionBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => QueryRevision.Check("AuditQuery", 0, 1));
    }
}
