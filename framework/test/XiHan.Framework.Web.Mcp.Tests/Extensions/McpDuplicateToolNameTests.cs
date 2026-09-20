// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Mcp.Tests.Extensions;

/// <summary>
/// 工具名冲突：两个技能投影出同名工具时，宿主起不来，且错误信息点得出是谁撞了谁
/// </summary>
/// <remarks>
/// 装配两个技能名不同、投影工具名相同的技能，断言启动时抛出的异常同时包含共享工具名与两个技能名。
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
    public async Task Startup_WithDuplicateToolNames_ThrowsNamingBothSkills()
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
