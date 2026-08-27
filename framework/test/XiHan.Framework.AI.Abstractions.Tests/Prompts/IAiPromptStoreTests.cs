// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Abstractions.Prompts;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 提示词库契约测试
/// </summary>
/// <remarks>
/// 这个 seam 的关键非对称性是：模板名必填、版本可选。
/// version 省略即取当前版本，是提示词灰度上线（写 v2、当前仍指 v1）能成立的前提。
/// </remarks>
public class IAiPromptStoreTests
{
    /// <summary>
    /// 省略版本时实现侧收到 null，即取当前版本
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenVersionOmitted_RequestsCurrentVersion()
    {
        var store = new InMemoryPromptStore();

        var template = await store.GetAsync("code-review", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(store.LastRequestedVersion);
        Assert.NotNull(template);
        Assert.Equal("当前版正文", template!.Content);
    }

    /// <summary>
    /// 指定版本时按版本精确取回
    /// </summary>
    [Fact]
    public async Task GetAsync_WithVersion_ReturnsThatVersion()
    {
        var store = new InMemoryPromptStore();

        var template = await store.GetAsync("code-review", "v1", TestContext.Current.CancellationToken);

        Assert.Equal("v1", store.LastRequestedVersion);
        Assert.NotNull(template);
        Assert.Equal("历史版正文", template!.Content);
        Assert.Equal("v1", template.Version);
    }

    /// <summary>
    /// 模板不存在时返回 null 而不是抛异常
    /// </summary>
    /// <remarks>提示词缺失是可预期的正常状态（尚未维护），调用方据此走内置兜底而不是让请求失败。</remarks>
    [Fact]
    public async Task GetAsync_WhenTemplateMissing_ReturnsNull()
    {
        var store = new InMemoryPromptStore();

        var template = await store.GetAsync("not-exists", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(template);
    }

    /// <summary>
    /// 指定了不存在的版本时同样返回 null，不静默回退到当前版本
    /// </summary>
    /// <remarks>
    /// 静默回退会让「指定 v3 跑评测」实际跑成当前版，评测结论完全失真，
    /// 因此明确要求版本不匹配就是没有。
    /// </remarks>
    [Fact]
    public async Task GetAsync_WhenVersionMissing_DoesNotFallBackToCurrent()
    {
        var store = new InMemoryPromptStore();

        var template = await store.GetAsync("code-review", "v99", TestContext.Current.CancellationToken);

        Assert.Null(template);
    }

    /// <summary>
    /// 列出全部模板返回只读列表
    /// </summary>
    [Fact]
    public async Task ListAsync_ReturnsEveryTemplate()
    {
        var store = new InMemoryPromptStore();

        var all = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, all.Count);
        Assert.Contains(all, template => template.Name == "code-review");
    }

    /// <summary>
    /// 取模板的签名：名字必填，版本与取消令牌可选
    /// </summary>
    [Fact]
    public void GetAsync_Signature_RequiresNameAndAllowsOptionalVersion()
    {
        var parameters = typeof(IAiPromptStore).GetMethod(nameof(IAiPromptStore.GetAsync))!.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal("name", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.Equal("version", parameters[1].Name);
        Assert.True(parameters[1].IsOptional);
        Assert.Null(parameters[1].DefaultValue);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.True(parameters[2].IsOptional);
    }

    /// <summary>
    /// 列出全部模板的返回类型为只读列表
    /// </summary>
    [Fact]
    public void ListAsync_Signature_ReturnsReadOnlyList()
    {
        var method = typeof(IAiPromptStore).GetMethod(nameof(IAiPromptStore.ListAsync))!;

        Assert.Equal(typeof(Task<IReadOnlyList<AiPromptTemplate>>), method.ReturnType);
        Assert.Single(method.GetParameters());
    }

    /// <summary>
    /// 以内存列表承载模板的 store 替身
    /// </summary>
    /// <remarks>刻意放入同名不同版本的两条记录，用来验证「省略版本取当前版」这条约定。</remarks>
    private sealed class InMemoryPromptStore : IAiPromptStore
    {
        private readonly List<AiPromptTemplate> _templates =
        [
            new AiPromptTemplate { Name = "code-review", Content = "当前版正文" },
            new AiPromptTemplate { Name = "code-review", Content = "历史版正文", Version = "v1" }
        ];

        /// <summary>
        /// 最近一次被请求的版本
        /// </summary>
        public string? LastRequestedVersion { get; private set; }

        /// <summary>
        /// 取模板，未指定版本时取当前版本
        /// </summary>
        /// <param name="name">模板名</param>
        /// <param name="version">版本</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<AiPromptTemplate?> GetAsync(string name, string? version = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestedVersion = version;

            var matched = _templates.FirstOrDefault(template =>
                string.Equals(template.Name, name, StringComparison.Ordinal) &&
                string.Equals(template.Version, version, StringComparison.Ordinal));

            return Task.FromResult<AiPromptTemplate?>(matched);
        }

        /// <summary>
        /// 列出全部模板
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<IReadOnlyList<AiPromptTemplate>> ListAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IReadOnlyList<AiPromptTemplate>>(_templates.ToList());
        }
    }
}
