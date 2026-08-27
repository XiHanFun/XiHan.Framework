// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Providers;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 多 provider 嵌入生成器解析契约测试
/// </summary>
/// <remarks>
/// 最需要锁死的是泛型实参：嵌入元素类型固定为 float。
/// 向量库里的集合是按 float 维度建好的，元素类型一旦改动（如换成 Half/double），
/// 既有集合全部读不回来，且失败发生在写入时而非编译时。
/// </remarks>
public class IAiEmbeddingGeneratorResolverTests
{
    /// <summary>
    /// 省略 provider 名时按默认 provider 解析
    /// </summary>
    [Fact]
    public void Resolve_WhenProviderNameOmitted_RequestsDefaultProvider()
    {
        var resolver = new RecordingEmbeddingGeneratorResolver();

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
    [InlineData("ollama")]
    public void Resolve_WithProviderName_PassesNameVerbatim(string providerName)
    {
        var resolver = new RecordingEmbeddingGeneratorResolver();

        resolver.Resolve(providerName);

        Assert.Equal(providerName, Assert.Single(resolver.ResolvedNames));
    }

    /// <summary>
    /// 省略 provider 名的失效表示清空全部缓存
    /// </summary>
    [Fact]
    public void Invalidate_WhenProviderNameOmitted_RequestsFullFlush()
    {
        var resolver = new RecordingEmbeddingGeneratorResolver();

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
        var resolver = new RecordingEmbeddingGeneratorResolver();

        resolver.Invalidate("openai");

        Assert.Equal("openai", Assert.Single(resolver.InvalidatedNames));
    }

    /// <summary>
    /// 解析返回以 string 为输入、float 向量为输出的原生嵌入生成器
    /// </summary>
    /// <remarks>float 这个泛型实参与向量库集合的物理维度绑定，属不可静默变更的契约。</remarks>
    [Fact]
    public void Resolve_Signature_ReturnsStringToFloatEmbeddingGenerator()
    {
        var method = typeof(IAiEmbeddingGeneratorResolver).GetMethod(nameof(IAiEmbeddingGeneratorResolver.Resolve))!;

        Assert.Equal(typeof(IEmbeddingGenerator<string, Embedding<float>>), method.ReturnType);
    }

    /// <summary>
    /// 解析与失效两个方法的 provider 名参数都可选且默认为 null
    /// </summary>
    /// <param name="methodName">被检查的方法名</param>
    [Theory]
    [InlineData(nameof(IAiEmbeddingGeneratorResolver.Resolve))]
    [InlineData(nameof(IAiEmbeddingGeneratorResolver.Invalidate))]
    public void Methods_HaveOptionalProviderNameDefaultingToNull(string methodName)
    {
        var parameters = typeof(IAiEmbeddingGeneratorResolver).GetMethod(methodName)!.GetParameters();

        Assert.Single(parameters);
        Assert.Equal("providerName", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.True(parameters[0].IsOptional);
        Assert.Null(parameters[0].DefaultValue);
    }

    /// <summary>
    /// 失效是同步无返回值的操作
    /// </summary>
    [Fact]
    public void Invalidate_Signature_IsSynchronous()
    {
        var method = typeof(IAiEmbeddingGeneratorResolver).GetMethod(nameof(IAiEmbeddingGeneratorResolver.Invalidate))!;

        Assert.Equal(typeof(void), method.ReturnType);
    }

    /// <summary>
    /// 与会话客户端解析器保持同构的解析/失效双方法形态
    /// </summary>
    /// <remarks>
    /// 两个解析器在实现上共用同一套「按名缓存 + 热切换」骨架，
    /// 方法名与参数名保持一致，调用方才能用同一套配置变更钩子驱动两者。
    /// </remarks>
    [Fact]
    public void Interface_MirrorsChatClientResolverShape()
    {
        var embeddingMethods = typeof(IAiEmbeddingGeneratorResolver).GetMethods().Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal);
        var chatMethods = typeof(IAiChatClientResolver).GetMethods().Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(chatMethods, embeddingMethods);
    }

    /// <summary>
    /// 只记录调用参数的嵌入生成器解析器替身
    /// </summary>
    private sealed class RecordingEmbeddingGeneratorResolver : IAiEmbeddingGeneratorResolver
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
        public IEmbeddingGenerator<string, Embedding<float>> Resolve(string? providerName = null)
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
