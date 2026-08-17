// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Docs.Mcp.Web.Tests;

/// <summary>
/// 启动期配置校验：配错的部署必须在开始服务之前就失败
/// </summary>
/// <remarks>
/// 全部用例都走真实的宿主启动路径（<see cref="DocsMcpWebTestHost.StartAsync"/> → <c>app.StartAsync()</c>），
/// 而不是直接 new 一个校验器来调：直接调只能证明校验器自己的判断对，证明不了它真的挂进了启动流程。
/// 「配错了但照样起来了」正是本组要挡的失败模式。
/// </remarks>
public class OptionsValidationTests
{
    /// <summary>
    /// 够长的密钥，用在所有「密钥本身没问题」的用例里
    /// </summary>
    private const string ValidApiKey = "correct-horse-battery-staple";

    /// <summary>
    /// 各种非法的请求头名与端点路径，每条都必须拒绝启动
    /// </summary>
    public static TheoryData<string, string, string, string> 非法的配置 => new()
    {
        { "请求头名为空串", "XiHan:Docs:Mcp:HeaderName", string.Empty, "HeaderName" },
        { "请求头名全是空白", "XiHan:Docs:Mcp:HeaderName", "   ", "HeaderName" },
        { "请求头名中间有空格", "XiHan:Docs:Mcp:HeaderName", "X Api Key", "HeaderName" },
        { "请求头名含冒号", "XiHan:Docs:Mcp:HeaderName", "X-Api-Key:", "HeaderName" },
        { "请求头名含中文", "XiHan:Docs:Mcp:HeaderName", "X-密钥", "HeaderName" },
        { "路径为空串", "XiHan:Docs:Mcp:Path", string.Empty, "Path" },
        { "路径全是空白", "XiHan:Docs:Mcp:Path", "   ", "Path" },
        { "路径不以斜杠开头", "XiHan:Docs:Mcp:Path", "mcp", "Path" },
        { "路径含空格", "XiHan:Docs:Mcp:Path", "/docs mcp", "Path" }
    };

    [Theory]
    [MemberData(nameof(非法的配置))]
    public async Task 配置非法时拒绝启动(string 场景, string 键, string 值, string 应出现在消息里的设置名)
    {
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            async () => await DocsMcpWebTestHost.StartAsync(
                new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
                new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", ValidApiKey),
                new KeyValuePair<string, string?>(键, 值)));

        // 只断言「抛了」不够：抛的可能是别的原因。消息必须点名到底是哪个设置项配错了，
        // 否则运维拿着一条 OptionsValidationException 还是不知道该改哪一行。
        var expected = $"XiHan:Docs:Mcp:{应出现在消息里的设置名}";

        Assert.True(
            exception.Message.Contains(expected, StringComparison.Ordinal),
            $"{场景}：校验消息本应点名 {expected}，实际是「{exception.Message}」。");
    }

    /// <summary>
    /// 短密钥必须被拒绝，且消息要说清最短要多少、怎么生成
    /// </summary>
    /// <remarks>
    /// 15 与 16 两条一起测：只测 15 的话，把下限改成任意大于 15 的数都还是绿的，
    /// 断言不到「16」这个具体门槛。
    /// </remarks>
    [Theory]
    [InlineData("123456789012345")]
    [InlineData("short")]
    [InlineData("a")]
    public async Task 密钥短于十六字符时拒绝启动(string apiKey)
    {
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            async () => await DocsMcpWebTestHost.StartAsync(
                new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
                new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", apiKey)));

        Assert.Contains("XiHan:Docs:Mcp:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("16", exception.Message, StringComparison.Ordinal);

        // 光说「太短」没用，得给出一条能直接粘贴执行的生成命令
        Assert.Contains("openssl rand -base64 32", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 密钥刚好十六字符时可以启动()
    {
        await using var host = await DocsMcpWebTestHost.StartAsync(
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", "0123456789abcdef"));

        Assert.NotNull(host.BaseAddress);
    }

    /// <summary>
    /// 未启用的部署即便其余配置全是非法值，也必须干干净净地起来
    /// </summary>
    /// <remarks>
    /// 仓库里提交的默认配置就是「关闭且没有密钥」。若校验不看 <c>IsExposable</c> 一律执行，
    /// 默认配置自己就会启动失败——那是把 fail-closed 变成 fail-always。
    /// </remarks>
    [Fact]
    public async Task 未启用时即便配置非法也照常启动()
    {
        await using var host = await DocsMcpWebTestHost.StartAsync(
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "false"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", "x"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:HeaderName", "X Api Key"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Path", "mcp 端点"));

        Assert.NotNull(host.BaseAddress);
    }

    /// <summary>
    /// 什么都不配（默认值）时同样要能起来
    /// </summary>
    [Fact]
    public async Task 完全不配时照常启动()
    {
        await using var host = await DocsMcpWebTestHost.StartAsync();

        Assert.NotNull(host.BaseAddress);
    }

    /// <summary>
    /// 一次配错多项时，消息要把每一项都列出来，而不是只报第一条
    /// </summary>
    [Fact]
    public async Task 多项配错时逐条列出()
    {
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            async () => await DocsMcpWebTestHost.StartAsync(
                new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
                new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", "tooshort"),
                new KeyValuePair<string, string?>("XiHan:Docs:Mcp:HeaderName", "X Api Key"),
                new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Path", "mcp")));

        Assert.Contains("XiHan:Docs:Mcp:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("XiHan:Docs:Mcp:HeaderName", exception.Message, StringComparison.Ordinal);
        Assert.Contains("XiHan:Docs:Mcp:Path", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// token 允许的特殊字符不该被误判成非法
    /// </summary>
    /// <remarks>
    /// 这一条是上面那批拒绝用例的对照组：没有它，把校验写成「只允许字母数字与短横线」
    /// 也能让全部拒绝用例变绿，而那会拒掉一堆合法的请求头名。
    /// </remarks>
    [Theory]
    [InlineData("X-Api-Key")]
    [InlineData("X_Api_Key")]
    [InlineData("X.Api.Key")]
    [InlineData("Api~Key!")]
    public async Task 合法的请求头名照常启动(string headerName)
    {
        await using var host = await DocsMcpWebTestHost.StartAsync(
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", ValidApiKey),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:HeaderName", headerName));

        Assert.NotNull(host.BaseAddress);
    }
}
