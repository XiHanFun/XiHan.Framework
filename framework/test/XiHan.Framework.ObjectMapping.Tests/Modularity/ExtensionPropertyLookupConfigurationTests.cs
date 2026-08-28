// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectMapping.Modularity;

namespace XiHan.Framework.ObjectMapping.Tests.Modularity;

/// <summary>
/// 扩展属性查找配置测试
/// </summary>
/// <remarks>
/// 四个字段名默认值（items / text / id / filter）是前端下拉框直接依赖的协议常量，
/// 改动会让所有没有显式配置的查找型扩展属性一起失效，因此必须逐个锁死字面量。
/// </remarks>
public class ExtensionPropertyLookupConfigurationTests
{
    /// <summary>
    /// 未配置查找地址时为 null，表示该属性不走远端查找
    /// </summary>
    [Fact]
    public void Url_DefaultsToNull()
    {
        var sut = new ExtensionPropertyLookupConfiguration();

        Assert.Null(sut.Url);
    }

    /// <summary>
    /// 结果列表字段名默认为 items
    /// </summary>
    [Fact]
    public void ResultListPropertyName_DefaultsToItems()
    {
        Assert.Equal("items", new ExtensionPropertyLookupConfiguration().ResultListPropertyName);
    }

    /// <summary>
    /// 显示字段名默认为 text
    /// </summary>
    [Fact]
    public void DisplayPropertyName_DefaultsToText()
    {
        Assert.Equal("text", new ExtensionPropertyLookupConfiguration().DisplayPropertyName);
    }

    /// <summary>
    /// 值字段名默认为 id
    /// </summary>
    [Fact]
    public void ValuePropertyName_DefaultsToId()
    {
        Assert.Equal("id", new ExtensionPropertyLookupConfiguration().ValuePropertyName);
    }

    /// <summary>
    /// 过滤参数名默认为 filter
    /// </summary>
    [Fact]
    public void FilterParamName_DefaultsToFilter()
    {
        Assert.Equal("filter", new ExtensionPropertyLookupConfiguration().FilterParamName);
    }

    /// <summary>
    /// 五个字段均可被覆盖
    /// </summary>
    [Fact]
    public void AllMembers_CanBeOverridden()
    {
        var sut = new ExtensionPropertyLookupConfiguration
        {
            Url = "/api/lookup",
            ResultListPropertyName = "data",
            DisplayPropertyName = "label",
            ValuePropertyName = "value",
            FilterParamName = "keyword"
        };

        Assert.Equal("/api/lookup", sut.Url);
        Assert.Equal("data", sut.ResultListPropertyName);
        Assert.Equal("label", sut.DisplayPropertyName);
        Assert.Equal("value", sut.ValuePropertyName);
        Assert.Equal("keyword", sut.FilterParamName);
    }

    /// <summary>
    /// 每个实例持有独立的默认值，互不影响
    /// </summary>
    [Fact]
    public void Instances_DoNotShareState()
    {
        var first = new ExtensionPropertyLookupConfiguration();
        var second = new ExtensionPropertyLookupConfiguration();

        first.ResultListPropertyName = "data";

        Assert.Equal("items", second.ResultListPropertyName);
    }
}
