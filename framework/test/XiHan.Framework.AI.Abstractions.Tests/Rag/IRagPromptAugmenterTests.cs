// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Abstractions.Rag;
using XiHan.Framework.AI.Abstractions.Rag.Models;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// RAG 提示增强器契约测试
/// </summary>
/// <remarks>
/// 接口上写明「context 为空则原样返回」，这是 RAG 链路的降级路径：
/// 检索无命中时必须退回纯对话，而不是拼出一段「以下资料为空」的提示去误导模型。
/// 这里用一个按约定实现的替身把该约定固化成可执行用例，供实现方对齐。
/// </remarks>
public class IRagPromptAugmenterTests
{
    /// <summary>
    /// 检索无命中时原样返回用户提问
    /// </summary>
    [Fact]
    public void Augment_WhenContextEmpty_ReturnsPromptUnchanged()
    {
        IRagPromptAugmenter augmenter = new ReferenceRagPromptAugmenter();

        var result = augmenter.Augment("曦寒框架是什么", []);

        Assert.Equal("曦寒框架是什么", result);
    }

    /// <summary>
    /// 有命中片段时，增强结果同时包含原提问与片段正文
    /// </summary>
    /// <remarks>
    /// 只断言「都在里面」而不锁死拼接模板：模板属实现策略（不同模型偏好不同的上下文格式），
    /// 但丢掉原提问或丢掉片段正文，两者都会让这次增强失去意义。
    /// </remarks>
    [Fact]
    public void Augment_WithContext_ContainsBothPromptAndChunkText()
    {
        IRagPromptAugmenter augmenter = new ReferenceRagPromptAugmenter();
        var context = new List<RetrievedChunk>
        {
            new() { DocumentId = "doc-1", Index = 0, Text = "曦寒框架是一套 .NET 基础框架", Score = 0.9d },
            new() { DocumentId = "doc-1", Index = 1, Text = "它由多个可插拔模块组成", Score = 0.8d }
        };

        var result = augmenter.Augment("曦寒框架是什么", context);

        Assert.Contains("曦寒框架是什么", result, StringComparison.Ordinal);
        Assert.Contains("曦寒框架是一套 .NET 基础框架", result, StringComparison.Ordinal);
        Assert.Contains("它由多个可插拔模块组成", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// 增强是同步纯函数，不带取消令牌也不返回 Task
    /// </summary>
    /// <remarks>拼字符串不做 IO；若哪天要异步，说明实现里混进了远程调用，应先质疑设计。</remarks>
    [Fact]
    public void Augment_Signature_IsSynchronousStringFunction()
    {
        var method = typeof(IRagPromptAugmenter).GetMethod(nameof(IRagPromptAugmenter.Augment))!;

        Assert.Equal(typeof(string), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal("userPrompt", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.Equal("context", parameters[1].Name);
        Assert.Equal(typeof(IReadOnlyList<RetrievedChunk>), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);
    }

    /// <summary>
    /// 上下文参数是只读列表而非 IEnumerable
    /// </summary>
    /// <remarks>
    /// 增强器通常要先取 Count 判断是否为空、再按序号编号引用，
    /// 只读列表保证这两步不会触发二次枚举，也不会拿到不同结果。
    /// </remarks>
    [Fact]
    public void Augment_Signature_TakesRandomAccessContext()
    {
        var parameters = typeof(IRagPromptAugmenter).GetMethod(nameof(IRagPromptAugmenter.Augment))!.GetParameters();

        Assert.True(typeof(IReadOnlyList<RetrievedChunk>).IsAssignableFrom(parameters[1].ParameterType));
        Assert.NotEqual(typeof(IEnumerable<RetrievedChunk>), parameters[1].ParameterType);
    }

    /// <summary>
    /// 按接口约定实现的增强器参考实现
    /// </summary>
    /// <remarks>
    /// 这是接口文档里那句「context 为空则原样返回」的可执行形态：
    /// 它不是被测代码，而是把口头约定写成实现方可以照抄的样板。
    /// </remarks>
    private sealed class ReferenceRagPromptAugmenter : IRagPromptAugmenter
    {
        /// <summary>
        /// 用检索片段增强用户提问
        /// </summary>
        /// <param name="userPrompt">用户提问</param>
        /// <param name="context">检索到的片段</param>
        public string Augment(string userPrompt, IReadOnlyList<RetrievedChunk> context)
        {
            if (context.Count == 0)
            {
                return userPrompt;
            }

            var references = string.Join("\n", context.Select(chunk => chunk.Text));

            return "参考资料：\n" + references + "\n\n问题：" + userPrompt;
        }
    }
}
