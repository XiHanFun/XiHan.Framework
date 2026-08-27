// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Abstractions.Configuration;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// AI Provider 配置来源契约测试
/// </summary>
/// <remarks>
/// 这个 seam 的两条约定都靠「null」表达且都无编译期保障，只能靠测试固化：
/// 入参 null 表示取默认 provider，返回 null 表示无匹配（调用方须 fail-closed，不得回退成任意一个 provider）。
/// </remarks>
public class IAiProviderConfigStoreTests
{
    /// <summary>
    /// 省略 provider 名时实现侧收到 null，即请求默认 provider
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenProviderNameOmitted_RequestsDefaultProvider()
    {
        var store = new InMemoryConfigStore();

        var resolved = await store.GetAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(store.LastRequestedProvider);
        Assert.NotNull(resolved);
        Assert.Equal("openai", resolved!.Provider);
    }

    /// <summary>
    /// 指定 provider 名时按名取到对应配置
    /// </summary>
    [Fact]
    public async Task GetAsync_WithProviderName_ReturnsMatchingConfiguration()
    {
        var store = new InMemoryConfigStore();

        var resolved = await store.GetAsync("ollama", TestContext.Current.CancellationToken);

        Assert.Equal("ollama", store.LastRequestedProvider);
        Assert.NotNull(resolved);
        Assert.Equal("qwen2.5", resolved!.Model);
    }

    /// <summary>
    /// 无匹配 provider 时返回 null 而不是抛异常或回退
    /// </summary>
    /// <remarks>
    /// 返回 null 让调用方显式决定 fail-closed；
    /// 若这里改成「找不到就返回第一个」，密钥会被发到并非用户指定的服务商。
    /// </remarks>
    [Fact]
    public async Task GetAsync_WhenProviderUnknown_ReturnsNull()
    {
        var store = new InMemoryConfigStore();

        var resolved = await store.GetAsync("not-configured", TestContext.Current.CancellationToken);

        Assert.Null(resolved);
    }

    /// <summary>
    /// 取全部配置返回只读列表
    /// </summary>
    /// <remarks>只读类型防止调用方就地增删，污染 store 的内部状态。</remarks>
    [Fact]
    public async Task GetAllAsync_ReturnsEveryEnabledProvider()
    {
        var store = new InMemoryConfigStore();

        var all = await store.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, all.Count);
        Assert.Contains(all, provider => provider.Provider == "openai");
        Assert.Contains(all, provider => provider.Provider == "ollama");
    }

    /// <summary>
    /// 取单个配置的签名：provider 名与取消令牌均为可选，且默认值为 null
    /// </summary>
    [Fact]
    public void GetAsync_Signature_HasOptionalProviderName()
    {
        var parameters = typeof(IAiProviderConfigStore).GetMethod(nameof(IAiProviderConfigStore.GetAsync))!.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal("providerName", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.True(parameters[0].IsOptional);
        Assert.Null(parameters[0].DefaultValue);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
    }

    /// <summary>
    /// 返回类型为配置的 Task，「无匹配」是正常返回 null 而非异常
    /// </summary>
    /// <remarks>可空注解不进运行期类型，故此处只能断言到 Task&lt;AiProviderOptions&gt;；null 语义由上面的用例覆盖。</remarks>
    [Fact]
    public void GetAsync_Signature_ReturnsConfigurationTask()
    {
        var method = typeof(IAiProviderConfigStore).GetMethod(nameof(IAiProviderConfigStore.GetAsync))!;

        Assert.Equal(typeof(Task<AiProviderOptions>), method.ReturnType);
    }

    /// <summary>
    /// 取全部配置的返回类型为只读列表
    /// </summary>
    [Fact]
    public void GetAllAsync_Signature_ReturnsReadOnlyList()
    {
        var method = typeof(IAiProviderConfigStore).GetMethod(nameof(IAiProviderConfigStore.GetAllAsync))!;

        Assert.Equal(typeof(Task<IReadOnlyList<AiProviderOptions>>), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Single(parameters);
        Assert.True(parameters[0].IsOptional);
    }

    /// <summary>
    /// 以内存字典承载配置的 store 替身
    /// </summary>
    /// <remarks>刻意用大小写不敏感字典，与 XiHanAiOptions.Providers 的既定语义保持一致。</remarks>
    private sealed class InMemoryConfigStore : IAiProviderConfigStore
    {
        private readonly Dictionary<string, AiProviderOptions> _providers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = new AiProviderOptions { Provider = "openai", Model = "gpt-4o-mini" },
            ["ollama"] = new AiProviderOptions { Provider = "ollama", Model = "qwen2.5" }
        };

        /// <summary>
        /// 最近一次被请求的 provider 名
        /// </summary>
        public string? LastRequestedProvider { get; private set; }

        /// <summary>
        /// 取指定 provider 配置，未指定时取默认 provider
        /// </summary>
        /// <param name="providerName">provider 名</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<AiProviderOptions?> GetAsync(string? providerName = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestedProvider = providerName;

            var key = providerName ?? "openai";
            _providers.TryGetValue(key, out var options);

            return Task.FromResult<AiProviderOptions?>(options);
        }

        /// <summary>
        /// 取全部启用的 provider 配置
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<IReadOnlyList<AiProviderOptions>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IReadOnlyList<AiProviderOptions>>(_providers.Values.ToList());
        }
    }
}
