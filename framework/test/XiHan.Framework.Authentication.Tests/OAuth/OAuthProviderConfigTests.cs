// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.OAuth;

namespace XiHan.Framework.Authentication.Tests.OAuth;

/// <summary>
/// 提供商配置的测试
/// </summary>
/// <remarks>
/// 覆盖提供商类型与方案名的回退关系，这是同一提供商注册多个方案的前提。
/// </remarks>
public class OAuthProviderConfigTests
{
    /// <summary>
    /// 未填提供商类型时应回退到方案名
    /// </summary>
    /// <param name="name">方案名</param>
    [Theory]
    [InlineData("github")]
    [InlineData("GitHub")]
    public void ResolveProviderType_WithoutProvider_FallsBackToName(string name)
    {
        var config = new OAuthProviderConfig { Name = name };

        Assert.Equal("github", config.ResolveProviderType());
    }

    /// <summary>
    /// 填了提供商类型时应以其为准，且大小写与空白不敏感
    /// </summary>
    [Fact]
    public void ResolveProviderType_WithProvider_TakesPrecedence()
    {
        var config = new OAuthProviderConfig { Name = "wechat-qr", Provider = "  WeChat  " };

        Assert.Equal(OAuthProviderNames.WeChat, config.ResolveProviderType());
    }

    /// <summary>
    /// 默认登录方式是扫码
    /// </summary>
    [Fact]
    public void Mode_DefaultsToQrCode()
    {
        Assert.Equal(OAuthLoginMode.QrCode, new OAuthProviderConfig().Mode);
    }
}
