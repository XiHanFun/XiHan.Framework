// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Rag;

namespace XiHan.Framework.AI.Abstractions.Tests.Rag;

/// <summary>
/// 文本切片选项测试
/// </summary>
/// <remarks>
/// 切片粒度直接决定检索质量与嵌入成本，默认值改动会让既有知识库的召回效果整体漂移，
/// 且新旧切片混在同一集合里无法比较，因此按字面量锁死。
/// </remarks>
public class ChunkingOptionsTests
{
    /// <summary>
    /// 默认单切片 1000 字符、重叠 100 字符
    /// </summary>
    [Fact]
    public void Defaults_WhenNewInstance_AreThousandAndHundred()
    {
        var options = new ChunkingOptions();

        Assert.Equal(1000, options.MaxChunkSize);
        Assert.Equal(100, options.Overlap);
    }

    /// <summary>
    /// 默认重叠必须小于默认切片长度
    /// </summary>
    /// <remarks>
    /// 固定窗口切片的步长是 MaxChunkSize - Overlap；一旦重叠不小于切片长度，
    /// 步长归零或为负，切片循环将永不前进。这是默认值必须满足的硬约束。
    /// </remarks>
    [Fact]
    public void Defaults_OverlapIsSmallerThanChunkSize()
    {
        var options = new ChunkingOptions();

        Assert.True(options.Overlap < options.MaxChunkSize);
    }

    /// <summary>
    /// 可经对象初始化器覆盖默认值
    /// </summary>
    /// <param name="maxChunkSize">单切片最大字符数</param>
    /// <param name="overlap">相邻切片重叠字符数</param>
    [Theory]
    [InlineData(200, 0)]
    [InlineData(512, 64)]
    [InlineData(2000, 200)]
    public void Initializer_WithCustomValues_OverridesDefaults(int maxChunkSize, int overlap)
    {
        var options = new ChunkingOptions
        {
            MaxChunkSize = maxChunkSize,
            Overlap = overlap
        };

        Assert.Equal(maxChunkSize, options.MaxChunkSize);
        Assert.Equal(overlap, options.Overlap);
    }

    /// <summary>
    /// 属性为 init-only，构造完成后不可再改
    /// </summary>
    /// <remarks>
    /// 切片选项会随请求一路传到切片器；若中途可被改写，同一次摄取可能出现前后不一致的切片粒度。
    /// 按自定义修饰符 IsExternalInit 判断，避免依赖具体语法形态。
    /// </remarks>
    [Theory]
    [InlineData(nameof(ChunkingOptions.MaxChunkSize))]
    [InlineData(nameof(ChunkingOptions.Overlap))]
    public void Properties_AreInitOnly(string propertyName)
    {
        var property = typeof(ChunkingOptions).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!;
        var setter = property.SetMethod!;

        var isInitOnly = setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier => modifier.Name == "IsExternalInit");

        Assert.True(isInitOnly);
    }

    /// <summary>
    /// 可经 System.Text.Json 往返，init-only 属性能被正确还原
    /// </summary>
    /// <param name="useWebNaming">true 用 Web 驼峰命名策略，false 用默认策略</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTrip_WithCustomValues_PreservesValues(bool useWebNaming)
    {
        JsonSerializerOptions? serializerOptions = useWebNaming ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : null;
        var source = new ChunkingOptions
        {
            MaxChunkSize = 800,
            Overlap = 120
        };

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<ChunkingOptions>(json, serializerOptions)!;

        Assert.Equal(800, restored.MaxChunkSize);
        Assert.Equal(120, restored.Overlap);
    }

    /// <summary>
    /// 类型为 sealed，切片粒度语义不允许被派生类改写
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(ChunkingOptions).IsSealed);
    }
}
