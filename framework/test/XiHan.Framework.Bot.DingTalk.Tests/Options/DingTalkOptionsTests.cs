// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Bot.DingTalk.Options;

namespace XiHan.Framework.Bot.DingTalk.Tests.Options;

/// <summary>
/// 钉钉提供者配置测试
/// </summary>
/// <remarks>
/// 该选项类没有 Validate 方法，公共契约集中在默认值语义上：
/// 默认启用、默认指向钉钉官方自定义机器人网关、令牌与密钥默认空串、关键字默认缺省。
/// WebHookUrl 用的是延迟回填（<c>??=</c>）而不是字段初始化，所以"赋 null 后再读"必须仍然回落到官方地址，
/// 否则配置绑定时缺省该节点会直接拼出一个非法 URL。
/// </remarks>
public class DingTalkOptionsTests
{
    /// <summary>
    /// 钉钉官方自定义机器人网关地址
    /// </summary>
    private const string OfficialWebHookUrl = "https://oapi.dingtalk.com/robot/send";

    /// <summary>
    /// 默认值符合钉钉自定义机器人的开箱语义
    /// </summary>
    [Fact]
    public void Defaults_AreExpected()
    {
        var options = new DingTalkOptions();

        Assert.True(options.Enabled);
        Assert.Equal(OfficialWebHookUrl, options.WebHookUrl);
        Assert.Equal(string.Empty, options.AccessToken);
        Assert.Equal(string.Empty, options.Secret);
        Assert.Null(options.KeyWord);
    }

    /// <summary>
    /// 赋 null 后读取仍回落到官方地址
    /// </summary>
    [Fact]
    public void WebHookUrl_WhenAssignedNull_FallsBackToOfficialEndpoint()
    {
        var options = new DingTalkOptions
        {
            WebHookUrl = "https://proxy.invalid/robot/send"
        };

        options.WebHookUrl = null!;

        Assert.Equal(OfficialWebHookUrl, options.WebHookUrl);
    }

    /// <summary>
    /// 显式配置的自建网关地址不会被默认值覆盖
    /// </summary>
    [Fact]
    public void WebHookUrl_WhenAssignedCustomEndpoint_KeepsCustomValue()
    {
        var options = new DingTalkOptions
        {
            WebHookUrl = "https://proxy.invalid/robot/send"
        };

        Assert.Equal("https://proxy.invalid/robot/send", options.WebHookUrl);
    }

    /// <summary>
    /// 连续读取默认地址结果稳定
    /// </summary>
    [Fact]
    public void WebHookUrl_ReadRepeatedly_IsStable()
    {
        var options = new DingTalkOptions();

        var first = options.WebHookUrl;
        var second = options.WebHookUrl;

        Assert.Equal(first, second);
        Assert.Equal(OfficialWebHookUrl, second);
    }

    /// <summary>
    /// 关键字缺省表示不启用关键字安全设置
    /// </summary>
    [Fact]
    public void KeyWord_DefaultsToNull_AndIsAssignable()
    {
        var options = new DingTalkOptions();

        Assert.Null(options.KeyWord);

        options.KeyWord = "监控告警";

        Assert.Equal("监控告警", options.KeyWord);
    }

    /// <summary>
    /// 提供者可被整体停用
    /// </summary>
    [Fact]
    public void Enabled_CanBeTurnedOff()
    {
        var options = new DingTalkOptions
        {
            Enabled = false
        };

        Assert.False(options.Enabled);
    }

    /// <summary>
    /// 配置节可完整绑定到选项对象
    /// </summary>
    /// <remarks>
    /// 选项走 IOptionsMonitor 读取，绑定键名一旦漂移就会静默退回默认值（表现为"配置了却没生效"），因此在这里锁死键名。
    /// </remarks>
    [Fact]
    public void Bind_FromConfiguration_MapsEveryKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Enabled"] = "false",
                ["WebHookUrl"] = "https://proxy.invalid/robot/send",
                ["AccessToken"] = "access-token-value",
                ["Secret"] = "SECsecretvalue",
                ["KeyWord"] = "告警"
            })
            .Build();

        var options = new DingTalkOptions();
        configuration.Bind(options);

        Assert.False(options.Enabled);
        Assert.Equal("https://proxy.invalid/robot/send", options.WebHookUrl);
        Assert.Equal("access-token-value", options.AccessToken);
        Assert.Equal("SECsecretvalue", options.Secret);
        Assert.Equal("告警", options.KeyWord);
    }
}
