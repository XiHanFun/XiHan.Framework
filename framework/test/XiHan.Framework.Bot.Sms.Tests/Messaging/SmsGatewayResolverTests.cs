// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Enums;
using XiHan.Framework.Bot.Sms.Messaging;
using XiHan.Framework.Bot.Sms.Options;
using XiHan.Framework.Bot.Sms.Tests.Fakes;

namespace XiHan.Framework.Bot.Sms.Tests.Messaging;

/// <summary>
/// <see cref="SmsGatewayResolver"/> 短信网关解析器测试
/// </summary>
/// <remarks>
/// 解析器是本项目最核心的编排点，三条契约必须钉死：
/// 一是 fail-closed —— 无配置/已禁用一律返回 null，不得静默构造出一个假客户端；
/// 二是按 SmsProviderType 解析到正确的服务商客户端，未知类型必须抛异常而不是回退到默认；
/// 三是按配置指纹缓存 —— 指纹不变复用同一实例，指纹一变立刻重建（改配置热生效）。
/// 客户端构造只做 SDK 侧的本地配置装配，不会产生任何网络请求。
/// </remarks>
public class SmsGatewayResolverTests
{
    /// <summary>
    /// 无配置时返回 null，调用方据此 fail-closed
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenStoreReturnsNull_ReturnsNull()
    {
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore());

        var client = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.Null(client);
    }

    /// <summary>
    /// 配置存在但已禁用时返回 null，且在校验凭证之前就短路（不抛异常）
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenConfigDisabled_ReturnsNullWithoutBuilding()
    {
        var config = CreateAliyunConfig();
        config.IsEnabled = false;
        config.AccessKeySecret = string.Empty;
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(config));

        var client = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.Null(client);
    }

    /// <summary>
    /// 取消令牌原样透传给配置存储
    /// </summary>
    [Fact]
    public async Task ResolveAsync_PassesCancellationTokenToStore()
    {
        var store = new FakeSmsConfigStore();
        var resolver = new SmsGatewayResolver(store);
        using var cts = new CancellationTokenSource();

        await resolver.ResolveAsync(cts.Token);

        Assert.Equal(cts.Token, store.LastCancellationToken);
    }

    /// <summary>
    /// 每次解析都重新读取配置存储，配置改动无需缓存失效事件即可热生效
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ReadsConfigStoreOnEveryCall()
    {
        var store = new FakeSmsConfigStore(CreateAliyunConfig());
        var resolver = new SmsGatewayResolver(store);

        await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, store.GetCount);
    }

    /// <summary>
    /// 启用的配置缺少访问密钥时抛异常，不允许构造出一个必然失败的客户端
    /// </summary>
    /// <param name="accessKeySecret">访问密钥</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_WhenAccessKeySecretBlank_Throws(string accessKeySecret)
    {
        var config = CreateAliyunConfig();
        config.AccessKeySecret = accessKeySecret;
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(config));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await resolver.ResolveAsync(TestContext.Current.CancellationToken));

        Assert.Contains("访问密钥", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 服务商为阿里云时解析到阿里云客户端
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenProviderIsAliyun_ResolvesAliyunClient()
    {
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(CreateAliyunConfig()));

        var client = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(client);
        Assert.IsType<AliyunSmsGatewayClient>(client);
        Assert.Equal(SmsProviderType.Aliyun, client!.Provider);
    }

    /// <summary>
    /// 服务商为腾讯云时解析到腾讯云客户端
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenProviderIsTencentCloud_ResolvesTencentCloudClient()
    {
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(CreateTencentConfig()));

        var client = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(client);
        Assert.IsType<TencentCloudSmsGatewayClient>(client);
        Assert.Equal(SmsProviderType.TencentCloud, client!.Provider);
    }

    /// <summary>
    /// 腾讯云缺少应用ID时抛异常，异常点名 SmsSdkAppId
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenTencentCloudSdkAppIdMissing_Throws()
    {
        var config = CreateTencentConfig();
        config.SdkAppId = null;
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(config));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await resolver.ResolveAsync(TestContext.Current.CancellationToken));

        Assert.Contains("SmsSdkAppId", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 腾讯云缺少地域时抛异常
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenTencentCloudRegionMissing_Throws()
    {
        var config = CreateTencentConfig();
        config.Region = null;
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(config));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await resolver.ResolveAsync(TestContext.Current.CancellationToken));

        Assert.Contains("地域", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 未知服务商类型抛异常，不得回退到任一默认服务商
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenProviderUnknown_Throws()
    {
        var config = CreateAliyunConfig();
        config.Provider = (SmsProviderType)99;
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(config));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await resolver.ResolveAsync(TestContext.Current.CancellationToken));

        Assert.Contains("不支持的短信服务商", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 配置指纹不变时复用同一客户端实例
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenFingerprintUnchanged_ReusesSameClient()
    {
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(CreateAliyunConfig()));

        var first = await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        var second = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    /// <summary>
    /// 短信签名变化即指纹变化，客户端被重建
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenSignNameChanged_RebuildsClient()
    {
        var config = CreateAliyunConfig();
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(config));

        var first = await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        config.SignName = "曦寒科技-新签名";
        var second = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 模板映射变化即指纹变化，客户端被重建（模板映射是客户端构造期快照）
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenTemplateMapChanged_RebuildsClient()
    {
        var config = CreateAliyunConfig();
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(config));

        var first = await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        config.TemplateMap = """{"auth-sms-login-code":{"templateCode":"SMS_999999"}}""";
        var second = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 访问密钥轮换后客户端被重建，不会继续用旧凭证发送
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenCredentialRotated_RebuildsClient()
    {
        var config = CreateAliyunConfig();
        var resolver = new SmsGatewayResolver(new FakeSmsConfigStore(config));

        var first = await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        config.AccessKeySecret = "rotated-secret";
        var second = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 服务商切换后解析到另一个服务商的客户端
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenProviderSwitched_ResolvesOtherProviderClient()
    {
        var store = new FakeSmsConfigStore(CreateAliyunConfig());
        var resolver = new SmsGatewayResolver(store);

        var first = await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        var tencentConfig = CreateTencentConfig();
        tencentConfig.ConfigId = 1L;
        store.Config = tencentConfig;
        var second = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.IsType<AliyunSmsGatewayClient>(first);
        Assert.IsType<TencentCloudSmsGatewayClient>(second);
    }

    /// <summary>
    /// 不同配置标识各自独立缓存，互不覆盖
    /// </summary>
    [Fact]
    public async Task ResolveAsync_DifferentConfigIds_CacheSeparately()
    {
        var first = CreateAliyunConfig();
        var second = CreateAliyunConfig();
        second.ConfigId = 2L;
        second.SignName = "曦寒科技-乙";
        var store = new FakeSmsConfigStore(first);
        var resolver = new SmsGatewayResolver(store);

        var firstClient = await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        store.Config = second;
        var secondClient = await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        store.Config = first;
        var firstClientAgain = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.NotSame(firstClient, secondClient);
        Assert.Same(firstClient, firstClientAgain);
    }

    /// <summary>
    /// 构造一份可用的阿里云配置
    /// </summary>
    /// <returns>阿里云配置</returns>
    private static SmsChannelConfig CreateAliyunConfig()
    {
        return new SmsChannelConfig
        {
            ConfigId = 1L,
            Provider = SmsProviderType.Aliyun,
            AccessKeyId = "test-access-key-id",
            AccessKeySecret = "test-access-key-secret",
            SignName = "曦寒科技",
            TemplateMap = """{"auth-sms-login-code":{"templateCode":"SMS_123456","paramOrder":["code"]}}""",
            IsEnabled = true
        };
    }

    /// <summary>
    /// 构造一份可用的腾讯云配置
    /// </summary>
    /// <returns>腾讯云配置</returns>
    private static SmsChannelConfig CreateTencentConfig()
    {
        return new SmsChannelConfig
        {
            ConfigId = 2L,
            Provider = SmsProviderType.TencentCloud,
            AccessKeyId = "test-secret-id",
            AccessKeySecret = "test-secret-key",
            SdkAppId = "1400000000",
            Region = "ap-guangzhou",
            SignName = "曦寒科技",
            TemplateMap = """{"auth-sms-login-code":{"templateCode":"1234567","paramOrder":["code"]}}""",
            IsEnabled = true
        };
    }
}
