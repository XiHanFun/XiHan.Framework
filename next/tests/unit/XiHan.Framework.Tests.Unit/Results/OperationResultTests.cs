using Xunit;
using XiHan.Framework.Shared.Results;

namespace XiHan.Framework.Tests.Unit.Results;

/// <summary>
/// 用于验证通用操作结果模型的基础行为。
/// </summary>
public sealed class OperationResultTests
{
    /// <summary>
    /// 验证成功结果应被正确标记为成功。
    /// </summary>
    [Fact]
    public void Success_Should_Create_Succeeded_Result()
    {
        var result = OperationResult.Success();

        Assert.True(result.Succeeded);
    }
}
