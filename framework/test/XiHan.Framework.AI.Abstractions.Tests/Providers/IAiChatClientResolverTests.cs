// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Providers;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 多 provider 会话客户端解析契约测试
/// </summary>
/// <remarks>
/// 解析器按 provider 名缓存已构建的客户端，因此失效语义是它最容易出错的部分：
/// 传名清指定 provider，传 null 清全部——两者一旦搞反，热切换要么不生效，要么把无关 provider 一并重建。
/// </remarks>
public class IAiChatClientResolverTests
{
    /// <summary>
    /// 省略 provider 名时按默认 provider 解析
    /// </summary>
    [Fact]
    public void Resolve_WhenProviderNameOmitted_RequestsDefaultProvider()
    {
        var resolver = new RecordingChatClientResolver();

        resolver.Resolve();

        Assert.Single(resolver.ResolvedNames);
        Assert.Null(resolver.ResolvedNames[0]);
    }

    /// <summary>
    /// 指定 provider 名时原样传给实现
    /// </summary>
    /// <param name="providerName">provider 名</param>
    [Theory]
    [InlineData("openai")]
    [InlineData("DeepSeek")]
    [InlineData("ollama")]
    public void Resolve_WithProviderName_PassesNameVerbatim(string providerName)
    {
        var resolver = new RecordingChatClientResolver();

        resolver.Resolve(providerName);

        Assert.Equal(providerName, Assert.Single(resolver.ResolvedNames));
    }

    /// <summary>
    /// 省略 provider 名的失效表示清空全部缓存
    /// </summary>
    /// <remarks>配置源整体重载（如 DB store 批量改动）后用这一形态，避免逐个 provider 调用。</remarks>
    [Fact]
    public void Invalidate_WhenProviderNameOmitted_RequestsFullFlush()
    {
        var resolver = new RecordingChatClientResolver();

        resolver.Invalidate();

        Assert.Single(resolver.InvalidatedNames);
        Assert.Null(resolver.InvalidatedNames[0]);
    }

    /// <summary>
    /// 指定 provider 名的失效只针对该 provider
    /// </summary>
    [Fact]
    public void Invalidate_WithProviderName_PassesNameVerbatim()
    {
        var resolver = new RecordingChatClientResolver();

        resolver.Invalidate("openai");

        Assert.Equal("openai", Assert.Single(resolver.InvalidatedNames));
    }

    /// <summary>
    /// 解析与失效是两条独立通道，互不记账
    /// </summary>
    /// <remarks>失效不应顺带触发一次解析——那会在配置尚未就绪时提前建连。</remarks>
    [Fact]
    public void Invalidate_DoesNotImplyResolve()
    {
        var resolver = new RecordingChatClientResolver();

        resolver.Invalidate("openai");

        Assert.Empty(resolver.ResolvedNames);
    }

    /// <summary>
    /// 解析返回原生 IChatClient，不包 XiHan 自有客户端类型
    /// </summary>
    /// <remarks>返回原生类型，调用方才能直接套用 Microsoft.Extensions.AI 的中间件与扩展方法。</remarks>
    [Fact]
    public void Resolve_Signature_ReturnsNativeChatClient()
    {
        var method = typeof(IAiChatClientResolver).GetMethod(nameof(IAiChatClientResolver.Resolve))!;

        Assert.Equal(typeof(IChatClient), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Single(parameters);
        Assert.Equal("providerName", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.True(parameters[0].IsOptional);
        Assert.Null(parameters[0].DefaultValue);
    }

    /// <summary>
    /// 失效是同步无返回值的操作
    /// </summary>
    /// <remarks>
    /// 返回 void 意味着它只做本地缓存清理，不等待任何 IO；
    /// 若哪天改成异步，配置写入路径上的调用点都要跟着改，属破坏性变更。
    /// </remarks>
    [Fact]
    public void Invalidate_Signature_IsSynchronousAndOptional()
    {
        var method = typeof(IAiChatClientResolver).GetMethod(nameof(IAiChatClientResolver.Invalidate))!;

        Assert.Equal(typeof(void), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Single(parameters);
        Assert.Equal("providerName", parameters[0].Name);
        Assert.True(parameters[0].IsOptional);
        Assert.Null(parameters[0].DefaultValue);
    }

    /// <summary>
    /// 只记录调用参数的解析器替身
    /// </summary>
    /// <remarks>
    /// 解析真去构建 IChatClient 就等于连外部模型服务，与抽象包无关；
    /// 这里只验证参数如何抵达实现，故返回值不参与断言。
    /// </remarks>
    private sealed class RecordingChatClientResolver : IAiChatClientResolver
    {
        /// <summary>
        /// 历次解析请求的 provider 名
        /// </summary>
        public List<string?> ResolvedNames { get; } = [];

        /// <summary>
        /// 历次失效请求的 provider 名
        /// </summary>
        public List<string?> InvalidatedNames { get; } = [];

        /// <summary>
        /// 记录解析请求
        /// </summary>
        /// <param name="providerName">provider 名</param>
        public IChatClient Resolve(string? providerName = null)
        {
            ResolvedNames.Add(providerName);

            return null!;
        }

        /// <summary>
        /// 记录失效请求
        /// </summary>
        /// <param name="providerName">provider 名</param>
        public void Invalidate(string? providerName = null)
        {
            InvalidatedNames.Add(providerName);
        }
    }
}
