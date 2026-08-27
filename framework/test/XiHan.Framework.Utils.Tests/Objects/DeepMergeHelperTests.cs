// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Objects;

namespace XiHan.Framework.Utils.Tests.Objects;

/// <summary>
/// 深度合并帮助类测试
/// </summary>
/// <remarks>
/// 覆盖"标量按优先级取第一个非默认值、字典按键合并、嵌套对象逐属性合并"三条主线，
/// 以及单配置输入时的深拷贝语义。
/// 不覆盖 List 属性的合并，原因见交付报告的疑似缺陷段落。
/// </remarks>
public class DeepMergeHelperTests
{
    /// <summary>
    /// 没有配置时返回全新的默认对象
    /// </summary>
    [Fact]
    public void DeepMerge_WithoutConfigs_ReturnsDefaultInstance()
    {
        var merged = DeepMergeHelper.DeepMerge<Config>();

        Assert.NotNull(merged);
        Assert.Null(merged.Name);
        Assert.Equal(0, merged.Timeout);
        Assert.Empty(merged.Settings);
        Assert.Null(merged.Child);
    }

    /// <summary>
    /// 配置数组为 null 时同样返回默认对象
    /// </summary>
    [Fact]
    public void DeepMerge_WhenConfigsIsNull_ReturnsDefaultInstance()
    {
        var merged = DeepMergeHelper.DeepMerge<Config>((Config[]?)null);

        Assert.NotNull(merged);
        Assert.Null(merged.Name);
    }

    /// <summary>
    /// 只有一个配置时返回深拷贝，引用型成员不与源共享
    /// </summary>
    [Fact]
    public void DeepMerge_WithSingleConfig_ReturnsDeepClone()
    {
        var source = new Config
        {
            Name = "源",
            Timeout = 5,
            Child = new Nested { Host = "localhost", Port = 80 }
        };
        source.Settings["k"] = "v";

        var merged = DeepMergeHelper.DeepMerge(source);

        Assert.NotSame(source, merged);
        Assert.Equal("源", merged.Name);
        Assert.Equal(5, merged.Timeout);
        Assert.Equal("v", merged.Settings["k"]);
        Assert.NotSame(source.Settings, merged.Settings);
        Assert.NotSame(source.Child, merged.Child);
        Assert.Equal("localhost", merged.Child!.Host);
        Assert.Equal(80, merged.Child.Port);
    }

    /// <summary>
    /// 深拷贝后修改结果不会影响源配置
    /// </summary>
    [Fact]
    public void DeepMerge_WithSingleConfig_ResultIsIndependentFromSource()
    {
        var source = new Config { Name = "源" };
        source.Settings["k"] = "v";

        var merged = DeepMergeHelper.DeepMerge(source);
        merged.Settings["k"] = "changed";
        merged.Name = "改过";

        Assert.Equal("v", source.Settings["k"]);
        Assert.Equal("源", source.Name);
    }

    /// <summary>
    /// 标量属性取优先级最高的非默认值
    /// </summary>
    [Fact]
    public void DeepMerge_ScalarProperties_PreferHighestPriorityNonDefault()
    {
        var high = new Config { Name = "高" };
        var low = new Config { Name = "低", Timeout = 30 };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.Equal("高", merged.Name);
        Assert.Equal(30, merged.Timeout);
    }

    /// <summary>
    /// 高优先级为默认值时回落到低优先级
    /// </summary>
    [Fact]
    public void DeepMerge_WhenHighPriorityIsDefault_FallsBackToLowPriority()
    {
        var high = new Config();
        var low = new Config { Name = "低", Timeout = 30 };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.Equal("低", merged.Name);
        Assert.Equal(30, merged.Timeout);
    }

    /// <summary>
    /// 三个配置时按顺序取第一个非默认值
    /// </summary>
    [Fact]
    public void DeepMerge_WithThreeConfigs_TakesFirstNonDefaultInOrder()
    {
        var first = new Config();
        var second = new Config { Name = "第二" };
        var third = new Config { Name = "第三", Timeout = 9 };

        var merged = DeepMergeHelper.DeepMerge(first, second, third);

        Assert.Equal("第二", merged.Name);
        Assert.Equal(9, merged.Timeout);
    }

    /// <summary>
    /// 字典按键合并，键冲突时保留高优先级的值
    /// </summary>
    [Fact]
    public void DeepMerge_Dictionaries_MergeByKeyKeepingHighPriorityValue()
    {
        var high = new Config();
        high.Settings["a"] = "高-a";
        var low = new Config();
        low.Settings["a"] = "低-a";
        low.Settings["b"] = "低-b";

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.Equal(2, merged.Settings.Count);
        Assert.Equal("高-a", merged.Settings["a"]);
        Assert.Equal("低-b", merged.Settings["b"]);
    }

    /// <summary>
    /// 高优先级字典为空时整体采用低优先级字典
    /// </summary>
    [Fact]
    public void DeepMerge_WhenHighPriorityDictionaryEmpty_UsesLowPriorityDictionary()
    {
        var high = new Config();
        var low = new Config();
        low.Settings["b"] = "低-b";

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.Equal("低-b", merged.Settings["b"]);
        Assert.NotSame(low.Settings, merged.Settings);
    }

    /// <summary>
    /// 同类型嵌套对象逐属性合并
    /// </summary>
    [Fact]
    public void DeepMerge_NestedObjects_MergePropertyByProperty()
    {
        var high = new Config { Child = new Nested { Host = "高-host" } };
        var low = new Config { Child = new Nested { Host = "低-host", Port = 8080 } };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.NotNull(merged.Child);
        Assert.Equal("高-host", merged.Child!.Host);
        Assert.Equal(8080, merged.Child.Port);
        Assert.NotSame(high.Child, merged.Child);
        Assert.NotSame(low.Child, merged.Child);
    }

    /// <summary>
    /// 高优先级缺少嵌套对象时整体采用低优先级的副本
    /// </summary>
    [Fact]
    public void DeepMerge_WhenHighPriorityNestedIsNull_ClonesLowPriorityNested()
    {
        var high = new Config();
        var low = new Config { Child = new Nested { Host = "低-host", Port = 8080 } };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.NotNull(merged.Child);
        Assert.Equal("低-host", merged.Child!.Host);
        Assert.Equal(8080, merged.Child.Port);
        Assert.NotSame(low.Child, merged.Child);
    }

    /// <summary>
    /// 测试用配置对象
    /// </summary>
    private sealed class Config
    {
        public string? Name { get; set; }

        public int Timeout { get; set; }

        public Dictionary<string, string> Settings { get; set; } = [];

        public Nested? Child { get; set; }
    }

    /// <summary>
    /// 测试用嵌套配置对象
    /// </summary>
    private sealed class Nested
    {
        public string? Host { get; set; }

        public int Port { get; set; }
    }
}
