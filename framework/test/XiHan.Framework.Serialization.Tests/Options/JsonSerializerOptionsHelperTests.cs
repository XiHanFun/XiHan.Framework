// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Serialization.Tests.Options;

/// <summary>
/// <see cref="JsonSerializerOptionsHelper"/> 创建序列化选项的测试
/// </summary>
public class JsonSerializerOptionsHelperTests
{
    /// <summary>
    /// 基于 baseOptions 创建新选项时，应复制并保留命名策略与缩进等既有配置
    /// </summary>
    [Fact]
    public void Create_CopiesBaseOptionsAndPreservesSettings()
    {
        var baseOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var result = JsonSerializerOptionsHelper.Create(baseOptions, _ => false);

        Assert.NotSame(baseOptions, result);
        Assert.True(result.WriteIndented);
        Assert.Same(JsonNamingPolicy.CamelCase, result.PropertyNamingPolicy);
        Assert.True(result.PropertyNameCaseInsensitive);
    }

    /// <summary>
    /// 创建新选项时，应按谓词移除匹配的转换器并保留其它转换器
    /// </summary>
    [Fact]
    public void Create_RemovesConverterMatchingPredicate()
    {
        var toRemove = new JsonStringEnumConverter();
        var toKeep = new JsonStringEnumConverter();
        var baseOptions = new JsonSerializerOptions();
        baseOptions.Converters.Add(toRemove);
        baseOptions.Converters.Add(toKeep);

        var result = JsonSerializerOptionsHelper.Create(baseOptions, converter => ReferenceEquals(converter, toRemove));

        Assert.DoesNotContain(toRemove, result.Converters);
        Assert.Contains(toKeep, result.Converters);
        Assert.Single(result.Converters);
    }

    /// <summary>
    /// 使用指定转换器重载创建时，应移除该转换器并添加新增转换器
    /// </summary>
    [Fact]
    public void Create_RemovesAndAddsConvertersByOverload()
    {
        var toRemove = new JsonStringEnumConverter();
        var toKeep = new JsonStringEnumConverter();
        var toAdd = new JsonStringEnumConverter();
        var baseOptions = new JsonSerializerOptions();
        baseOptions.Converters.Add(toRemove);
        baseOptions.Converters.Add(toKeep);

        var result = JsonSerializerOptionsHelper.Create(baseOptions, toRemove, toAdd);

        Assert.DoesNotContain(toRemove, result.Converters);
        Assert.Contains(toKeep, result.Converters);
        Assert.Contains(toAdd, result.Converters);
        Assert.Equal(2, result.Converters.Count);
    }

    /// <summary>
    /// 创建新选项时，应将尚未包含的转换器添加到结果中
    /// </summary>
    [Fact]
    public void Create_AddsConvertersNotAlreadyPresent()
    {
        var existing = new JsonStringEnumConverter();
        var toAdd = new JsonStringEnumConverter();
        var baseOptions = new JsonSerializerOptions();
        baseOptions.Converters.Add(existing);

        var result = JsonSerializerOptionsHelper.Create(baseOptions, _ => false, toAdd);

        Assert.Equal(2, result.Converters.Count);
        Assert.Contains(existing, result.Converters);
        Assert.Contains(toAdd, result.Converters);
    }

    /// <summary>
    /// 重复添加同一转换器实例时，不应产生重复项
    /// </summary>
    [Fact]
    public void Create_DoesNotAddDuplicateConverters()
    {
        var existing = new JsonStringEnumConverter();
        var baseOptions = new JsonSerializerOptions();
        baseOptions.Converters.Add(existing);

        var result = JsonSerializerOptionsHelper.Create(baseOptions, _ => false, existing, existing);

        Assert.Single(result.Converters);
        Assert.Contains(existing, result.Converters);
    }

    /// <summary>
    /// 创建新选项时，不应修改传入的 baseOptions
    /// </summary>
    [Fact]
    public void Create_DoesNotModifyBaseOptions()
    {
        var existing = new JsonStringEnumConverter();
        var toAdd = new JsonStringEnumConverter();
        var baseOptions = new JsonSerializerOptions();
        baseOptions.Converters.Add(existing);

        _ = JsonSerializerOptionsHelper.Create(baseOptions, converter => ReferenceEquals(converter, existing), toAdd);

        Assert.Single(baseOptions.Converters);
        Assert.Contains(existing, baseOptions.Converters);
    }
}
