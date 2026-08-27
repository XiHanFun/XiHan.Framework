// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Lark.Options;

namespace XiHan.Framework.Bot.Lark.Tests.Options;

/// <summary>
/// 飞书提供者配置测试
/// </summary>
/// <remarks>
/// WebHookUrl / UploadUrl 采用「显式赋值优先，未赋值时惰性回落官方地址」的语义（get 里的 ??=），
/// 这里把默认地址、回落时机与赋值覆盖一起锁死，避免改写属性时悄悄破坏未配置场景。
/// </remarks>
public class LarkOptionsTests
{
    /// <summary>
    /// 飞书自定义机器人 Webhook 官方前缀
    /// </summary>
    private const string DefaultWebHookUrl = "https://open.feishu.cn/open-apis/bot/v2/hook";

    /// <summary>
    /// 飞书图片上传官方地址
    /// </summary>
    private const string DefaultUploadUrl = "https://open.feishu.cn/open-apis/im/v1/images";

    /// <summary>
    /// 新建配置默认「已启用但未授权」
    /// </summary>
    [Fact]
    public void Defaults_WhenNewInstance_AreEnabledWithOfficialEndpoints()
    {
        var options = new LarkOptions();

        Assert.True(options.Enabled);
        Assert.Equal(DefaultWebHookUrl, options.WebHookUrl);
        Assert.Equal(DefaultUploadUrl, options.UploadUrl);
        Assert.Equal(string.Empty, options.AccessToken);
        Assert.Equal(string.Empty, options.Secret);
        Assert.Null(options.KeyWord);
    }

    /// <summary>
    /// 显式赋值的 Webhook 地址优先于默认地址
    /// </summary>
    [Fact]
    public void WebHookUrl_WhenAssigned_ReturnsAssignedValue()
    {
        var options = new LarkOptions
        {
            WebHookUrl = "https://self-hosted.example.com/hook"
        };

        Assert.Equal("https://self-hosted.example.com/hook", options.WebHookUrl);
    }

    /// <summary>
    /// 先读取默认值再赋值，赋值仍然生效
    /// </summary>
    /// <remarks>
    /// get 里的 ??= 会把默认值写回后备字段，若实现改成「只读一次」会让后续配置失效，故单独覆盖。
    /// </remarks>
    [Fact]
    public void WebHookUrl_WhenReadBeforeAssignment_StillHonorsLaterAssignment()
    {
        var options = new LarkOptions();

        Assert.Equal(DefaultWebHookUrl, options.WebHookUrl);

        options.WebHookUrl = "https://later.example.com/hook";

        Assert.Equal("https://later.example.com/hook", options.WebHookUrl);
    }

    /// <summary>
    /// 赋 null 时回落到默认 Webhook 地址
    /// </summary>
    [Fact]
    public void WebHookUrl_WhenAssignedNull_FallsBackToDefault()
    {
        var options = new LarkOptions
        {
            WebHookUrl = "https://temp.example.com/hook"
        };

        options.WebHookUrl = null!;

        Assert.Equal(DefaultWebHookUrl, options.WebHookUrl);
    }

    /// <summary>
    /// 赋空串时保留空串而不回落
    /// </summary>
    /// <remarks>
    /// ??= 只在 null 时回落，空串属于「用户显式配置成空」，语义上与 null 不同，这里明确固化。
    /// </remarks>
    [Fact]
    public void WebHookUrl_WhenAssignedEmpty_KeepsEmpty()
    {
        var options = new LarkOptions
        {
            WebHookUrl = string.Empty
        };

        Assert.Equal(string.Empty, options.WebHookUrl);
    }

    /// <summary>
    /// 显式赋值的上传地址优先于默认地址
    /// </summary>
    [Fact]
    public void UploadUrl_WhenAssigned_ReturnsAssignedValue()
    {
        var options = new LarkOptions
        {
            UploadUrl = "https://self-hosted.example.com/images"
        };

        Assert.Equal("https://self-hosted.example.com/images", options.UploadUrl);
    }

    /// <summary>
    /// 赋 null 时回落到默认上传地址
    /// </summary>
    [Fact]
    public void UploadUrl_WhenAssignedNull_FallsBackToDefault()
    {
        var options = new LarkOptions
        {
            UploadUrl = "https://temp.example.com/images"
        };

        options.UploadUrl = null!;

        Assert.Equal(DefaultUploadUrl, options.UploadUrl);
    }

    /// <summary>
    /// 两个默认地址互不相同且都指向飞书开放平台
    /// </summary>
    [Fact]
    public void DefaultEndpoints_Always_PointToFeishuOpenPlatform()
    {
        var options = new LarkOptions();

        Assert.StartsWith("https://open.feishu.cn/", options.WebHookUrl);
        Assert.StartsWith("https://open.feishu.cn/", options.UploadUrl);
        Assert.NotEqual(options.WebHookUrl, options.UploadUrl);
    }

    /// <summary>
    /// 提供者可被显式关闭
    /// </summary>
    [Fact]
    public void Enabled_WhenSetFalse_IsFalse()
    {
        var options = new LarkOptions
        {
            Enabled = false
        };

        Assert.False(options.Enabled);
    }

    /// <summary>
    /// 凭据类属性可读可写
    /// </summary>
    [Fact]
    public void Credentials_WhenAssigned_AreStoredAsIs()
    {
        var options = new LarkOptions
        {
            AccessToken = "0d1a2b3c-token",
            Secret = "sign-secret",
            KeyWord = "告警"
        };

        Assert.Equal("0d1a2b3c-token", options.AccessToken);
        Assert.Equal("sign-secret", options.Secret);
        Assert.Equal("告警", options.KeyWord);
    }

    /// <summary>
    /// 关键字可被显式置空表示不启用关键词校验
    /// </summary>
    [Fact]
    public void KeyWord_WhenAssignedNull_IsNull()
    {
        var options = new LarkOptions
        {
            KeyWord = "告警"
        };

        options.KeyWord = null;

        Assert.Null(options.KeyWord);
    }
}
