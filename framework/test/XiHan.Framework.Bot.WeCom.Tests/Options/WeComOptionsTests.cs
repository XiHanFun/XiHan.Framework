// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.WeCom.Options;

namespace XiHan.Framework.Bot.WeCom.Tests.Options;

/// <summary>
/// <see cref="WeComOptions"/> 默认值与地址回落语义测试
/// </summary>
/// <remarks>
/// 两个地址属性是「懒初始化 + 赋 null 回落默认」的写法，默认值直接决定机器人往哪个域名发包，
/// 属于对外协议约定，必须锁死。
/// </remarks>
public class WeComOptionsTests
{
    private const string ExpectedWebHookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send";

    private const string ExpectedUploadUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/upload_media";

    /// <summary>
    /// 默认值指向企业微信官方群机器人端点，且默认启用、Key 为空
    /// </summary>
    [Fact]
    public void Defaults_PointToOfficialWeComEndpoints()
    {
        var options = new WeComOptions();

        Assert.True(options.Enabled);
        Assert.Equal(ExpectedWebHookUrl, options.WebHookUrl);
        Assert.Equal(ExpectedUploadUrl, options.UploadUrl);
        Assert.Equal(string.Empty, options.Key);
    }

    /// <summary>
    /// 显式赋值的网络挂钩地址覆盖默认值
    /// </summary>
    [Fact]
    public void WebHookUrl_WhenAssigned_OverridesDefault()
    {
        var options = new WeComOptions
        {
            WebHookUrl = "https://proxy.internal/webhook/send"
        };

        Assert.Equal("https://proxy.internal/webhook/send", options.WebHookUrl);
    }

    /// <summary>
    /// 显式赋值的上传地址覆盖默认值
    /// </summary>
    [Fact]
    public void UploadUrl_WhenAssigned_OverridesDefault()
    {
        var options = new WeComOptions
        {
            UploadUrl = "https://proxy.internal/webhook/upload_media"
        };

        Assert.Equal("https://proxy.internal/webhook/upload_media", options.UploadUrl);
    }

    /// <summary>
    /// 网络挂钩地址被置空引用后回落到默认值
    /// </summary>
    /// <remarks>
    /// 配置绑定缺失该节点时会写入 null，这条保证不会拼出以 "?key=" 开头的畸形地址。
    /// </remarks>
    [Fact]
    public void WebHookUrl_WhenAssignedNull_FallsBackToDefault()
    {
        var options = new WeComOptions
        {
            WebHookUrl = "https://proxy.internal/webhook/send"
        };

        options.WebHookUrl = null!;

        Assert.Equal(ExpectedWebHookUrl, options.WebHookUrl);
    }

    /// <summary>
    /// 上传地址被置空引用后回落到默认值
    /// </summary>
    [Fact]
    public void UploadUrl_WhenAssignedNull_FallsBackToDefault()
    {
        var options = new WeComOptions
        {
            UploadUrl = "https://proxy.internal/webhook/upload_media"
        };

        options.UploadUrl = null!;

        Assert.Equal(ExpectedUploadUrl, options.UploadUrl);
    }

    /// <summary>
    /// 空字符串是显式赋值，不触发默认值回落
    /// </summary>
    [Fact]
    public void WebHookUrl_WhenAssignedEmpty_KeepsEmpty()
    {
        var options = new WeComOptions
        {
            WebHookUrl = string.Empty
        };

        Assert.Equal(string.Empty, options.WebHookUrl);
    }

    /// <summary>
    /// 提供者可以被显式禁用
    /// </summary>
    [Fact]
    public void Enabled_CanBeTurnedOff()
    {
        var options = new WeComOptions
        {
            Enabled = false
        };

        Assert.False(options.Enabled);
    }
}
