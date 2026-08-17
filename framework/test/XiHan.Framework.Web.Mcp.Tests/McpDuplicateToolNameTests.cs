// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// 工具名冲突：两个技能投影出同名工具时，宿主起不来，且错误信息点得出是谁撞了谁
/// </summary>
/// <remarks>
/// 撞名的两个技能里注定有一个既列不出也调不到。以前是 <c>TryAdd</c> 静默丢弃，运维只会看到
/// 「注册过的技能凭空不存在」，现场没有任何线索；现在在装配 MCP 选项时就失败，把工具名与两个技能都写进异常。
/// <para>
/// 注意技能注册表按名（忽略大小写）索引且同名覆盖，所以两个**技能名相同**的技能根本进不了同一张表——
/// 能撞到工具名的，是技能名不同、<c>AsFunction()</c> 却取了同一个工具名的情形，本用例正是这么装配的。
/// </para>
/// <para>
/// 反向对照在 <see cref="McpToolExposurePolicyTests.两个清单都不配时全部技能都暴露且都调得动"/>：
/// 工具名不撞的两个技能能把宿主正常拉起来，所以这里的失败不是「注册两个技能就炸」。
/// </para>
/// </remarks>
public class McpDuplicateToolNameTests
{
    /// <summary>
    /// 宿主配置的正确密钥
    /// </summary>
    private const string ApiKey = "duplicate-tool-key";

    /// <summary>
    /// 两个技能抢的同一个工具名
    /// </summary>
    private const string SharedTool = "xihan_test_shared";

    /// <summary>
    /// 甲技能名
    /// </summary>
    private const string FirstSkill = "xihan_test_first";

    /// <summary>
    /// 乙技能名
    /// </summary>
    private const string SecondSkill = "xihan_test_second";

    /// <summary>
    /// 两个技能投影出同名工具时，宿主启动即失败，异常里点出工具名与冲突双方
    /// </summary>
    [Fact]
    public async Task 工具名冲突时宿主启动即失败并点名冲突双方()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var host = await McpTestHost.StartAsync(
                enabled: true,
                ApiKey,
                new NamedEchoAiSkill(FirstSkill, SharedTool),
                new NamedEchoAiSkill(SecondSkill, SharedTool));
        });

        // 三样都得有：不点工具名就不知道改哪个，不点技能名就得自己翻遍注册表找是谁
        Assert.Contains(SharedTool, exception.Message, StringComparison.Ordinal);
        Assert.Contains(FirstSkill, exception.Message, StringComparison.Ordinal);
        Assert.Contains(SecondSkill, exception.Message, StringComparison.Ordinal);
    }
}
