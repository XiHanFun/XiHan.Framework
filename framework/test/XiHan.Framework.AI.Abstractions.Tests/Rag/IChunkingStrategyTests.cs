// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Abstractions.Rag;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 文本切片策略契约测试
/// </summary>
/// <remarks>
/// 切片是同步纯函数：不带取消令牌、不返回 Task，也不给 options 兜默认值——
/// 调用方必须显式决定切片粒度，避免同一份知识库里混入不同粒度的切片。
/// </remarks>
public class IChunkingStrategyTests
{
    /// <summary>
    /// 切片选项原样抵达实现侧
    /// </summary>
    /// <remarks>断言引用相同，确认没有在中途替换成默认选项。</remarks>
    [Fact]
    public void Chunk_WithOptions_PassesSameOptionsInstance()
    {
        var strategy = new RecordingChunkingStrategy();
        var options = new ChunkingOptions
        {
            MaxChunkSize = 300,
            Overlap = 30
        };

        strategy.Chunk("第一段\n第二段", options);

        Assert.Same(options, strategy.LastOptions);
    }

    /// <summary>
    /// 待切文本原样抵达实现侧
    /// </summary>
    [Fact]
    public void Chunk_WithText_PassesTextVerbatim()
    {
        var strategy = new RecordingChunkingStrategy();

        strategy.Chunk("原始正文", new ChunkingOptions());

        Assert.Equal("原始正文", strategy.LastText);
    }

    /// <summary>
    /// 返回值是只读列表，可多次枚举并按序号索引
    /// </summary>
    /// <remarks>
    /// 切片序号（TextChunk.Index）由列表下标推导，因此返回类型必须是有序可索引的只读列表，
    /// 不能退化成 IEnumerable——那样二次枚举可能产生不同结果，序号就失去意义。
    /// </remarks>
    [Fact]
    public void Chunk_Result_IsIndexableAndRepeatable()
    {
        var strategy = new RecordingChunkingStrategy();

        var chunks = strategy.Chunk("第一段\n第二段\n第三段", new ChunkingOptions());

        Assert.Equal(3, chunks.Count);
        Assert.Equal("第一段", chunks[0]);
        Assert.Equal("第三段", chunks[2]);

        // 两次枚举必须得到同一序列：切片序号靠下标推导，惰性序列会让序号与内容对不上
        Assert.Equal(chunks.ToList(), chunks.ToList());
    }

    /// <summary>
    /// 方法签名为同步纯函数：两个参数均必填，返回只读字符串列表
    /// </summary>
    [Fact]
    public void Chunk_Signature_IsSynchronousWithTwoRequiredParameters()
    {
        var method = typeof(IChunkingStrategy).GetMethod(nameof(IChunkingStrategy.Chunk))!;

        Assert.Equal(typeof(IReadOnlyList<string>), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal("text", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.Equal("options", parameters[1].Name);
        Assert.Equal(typeof(ChunkingOptions), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);
    }

    /// <summary>
    /// 切片接口不携带取消令牌
    /// </summary>
    /// <remarks>
    /// 这是「切片是 CPU 内纯计算、不做 IO」的结构声明；
    /// 若哪天需要令牌，说明实现里混进了 IO，应先质疑设计而不是加参数。
    /// </remarks>
    [Fact]
    public void Chunk_Signature_TakesNoCancellationToken()
    {
        var method = typeof(IChunkingStrategy).GetMethod(nameof(IChunkingStrategy.Chunk))!;

        Assert.DoesNotContain(method.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
    }

    /// <summary>
    /// 按换行切分并记录入参的切片策略替身
    /// </summary>
    /// <remarks>
    /// 刻意不实现真实的固定窗口算法：那属于实现包的职责，
    /// 这里只需要一个可预测的产出用于验证参数传递与返回类型。
    /// </remarks>
    private sealed class RecordingChunkingStrategy : IChunkingStrategy
    {
        /// <summary>
        /// 最近一次收到的待切文本
        /// </summary>
        public string? LastText { get; private set; }

        /// <summary>
        /// 最近一次收到的切片选项
        /// </summary>
        public ChunkingOptions? LastOptions { get; private set; }

        /// <summary>
        /// 按换行切分文本
        /// </summary>
        /// <param name="text">待切文本</param>
        /// <param name="options">切片选项</param>
        public IReadOnlyList<string> Chunk(string text, ChunkingOptions options)
        {
            LastText = text;
            LastOptions = options;

            return text.Split('\n');
        }
    }
}
