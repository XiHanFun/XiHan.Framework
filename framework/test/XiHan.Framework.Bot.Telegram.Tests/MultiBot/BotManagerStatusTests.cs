// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Telegram.MultiBot;

namespace XiHan.Framework.Bot.Telegram.Tests.MultiBot;

/// <summary>
/// <see cref="BotManagerStatus"/> 管理器运行状态测试
/// </summary>
/// <remarks>
/// 这两个模型是给应用层管理端点直接序列化返回的，字段名属于对外契约；
/// 默认值也要保证「什么都没跑起来」的状态能被如实表达（而不是 null 引发前端崩溃）。
/// </remarks>
public class BotManagerStatusTests
{
    /// <summary>
    /// 默认状态表示未启动、未启用、无机器人
    /// </summary>
    [Fact]
    public void Defaults_RepresentNotStartedAndDisabled()
    {
        var status = new BotManagerStatus();

        Assert.False(status.IsStarted);
        Assert.False(status.Enabled);
        Assert.Equal(string.Empty, status.TransportMode);
        Assert.Equal(0, status.TotalBots);
        Assert.NotNull(status.Bots);
        Assert.Empty(status.Bots);
    }

    /// <summary>
    /// 单个机器人运行信息的默认值
    /// </summary>
    [Fact]
    public void BotRunningInfo_Defaults_AreEmptyAndNotRunning()
    {
        var info = new BotRunningInfo();

        Assert.Equal(string.Empty, info.Name);
        Assert.Equal(string.Empty, info.Mode);
        Assert.Equal(string.Empty, info.Username);
        Assert.Equal(0L, info.BotId);
        Assert.False(info.IsRunning);
    }

    /// <summary>
    /// 每个实例持有独立的机器人列表，互不串改
    /// </summary>
    [Fact]
    public void Bots_AreNotSharedBetweenInstances()
    {
        var first = new BotManagerStatus();
        var second = new BotManagerStatus();

        first.Bots.Add(new BotRunningInfo { Name = "main-bot" });

        Assert.Empty(second.Bots);
    }

    /// <summary>
    /// JSON 往返保持字段名与取值，管理端点的返回结构不漂移
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsFieldNamesAndValues()
    {
        var status = new BotManagerStatus
        {
            IsStarted = true,
            Enabled = true,
            TransportMode = "webhook",
            TotalBots = 1,
            Bots =
            [
                new BotRunningInfo
                {
                    Name = "main-bot",
                    Mode = "webhook",
                    Username = "my_bot",
                    BotId = 123456L,
                    IsRunning = true
                }
            ]
        };

        var json = JsonSerializer.Serialize(status);

        Assert.Contains("\"IsStarted\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Enabled\"", json, StringComparison.Ordinal);
        Assert.Contains("\"TransportMode\"", json, StringComparison.Ordinal);
        Assert.Contains("\"TotalBots\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Bots\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Username\"", json, StringComparison.Ordinal);
        Assert.Contains("\"BotId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"IsRunning\"", json, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<BotManagerStatus>(json);

        Assert.NotNull(restored);
        Assert.True(restored!.IsStarted);
        Assert.Equal("webhook", restored.TransportMode);
        Assert.Equal(1, restored.TotalBots);
        Assert.Single(restored.Bots);
        Assert.Equal("main-bot", restored.Bots[0].Name);
        Assert.Equal(123456L, restored.Bots[0].BotId);
        Assert.True(restored.Bots[0].IsRunning);
    }
}
