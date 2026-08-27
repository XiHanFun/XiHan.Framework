// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Abac;

namespace XiHan.Framework.Authorization.Tests.Abac;

/// <summary>
/// ABAC 属性快照测试
/// </summary>
/// <remarks>
/// 三组属性字典的比较器是整个 ABAC 匹配链的隐含前提：收集器写入的键一律小写，
/// 而策略表达式里的键由人手写，只有大小写不敏感才能匹配上，所以这里把比较器语义锁死。
/// </remarks>
public class AbacAttributeSetTests
{
    /// <summary>
    /// 三组属性字典默认非空且为空集合
    /// </summary>
    [Fact]
    public void New_ByDefault_HasEmptyDictionaries()
    {
        var set = new AbacAttributeSet();

        Assert.Empty(set.SubjectAttributes);
        Assert.Empty(set.ResourceAttributes);
        Assert.Empty(set.EnvironmentAttributes);
    }

    /// <summary>
    /// 主体属性字典忽略键的大小写
    /// </summary>
    [Fact]
    public void SubjectAttributes_IgnoresKeyCase()
    {
        var set = new AbacAttributeSet();

        set.SubjectAttributes["User_Id"] = "u1";

        Assert.True(set.SubjectAttributes.ContainsKey("user_id"));
        Assert.Equal("u1", set.SubjectAttributes["USER_ID"]);
    }

    /// <summary>
    /// 资源属性字典忽略键的大小写
    /// </summary>
    [Fact]
    public void ResourceAttributes_IgnoresKeyCase()
    {
        var set = new AbacAttributeSet();

        set.ResourceAttributes["Tenant_Id"] = "t1";

        Assert.Equal("t1", set.ResourceAttributes["tenant_id"]);
    }

    /// <summary>
    /// 环境属性字典忽略键的大小写
    /// </summary>
    [Fact]
    public void EnvironmentAttributes_IgnoresKeyCase()
    {
        var set = new AbacAttributeSet();

        set.EnvironmentAttributes["Utc_Hour"] = 8;

        Assert.Equal(8, set.EnvironmentAttributes["utc_hour"]);
    }

    /// <summary>
    /// 三组属性字典互相独立，写一组不会串到另一组
    /// </summary>
    [Fact]
    public void Attributes_AreIndependentDictionaries()
    {
        var set = new AbacAttributeSet();

        set.SubjectAttributes["k"] = "v";

        Assert.Empty(set.ResourceAttributes);
        Assert.Empty(set.EnvironmentAttributes);
        Assert.NotSame(set.SubjectAttributes, set.ResourceAttributes);
    }

    /// <summary>
    /// 两个实例之间不共享字典引用
    /// </summary>
    [Fact]
    public void New_TwoInstances_DoNotShareDictionaries()
    {
        var first = new AbacAttributeSet();
        var second = new AbacAttributeSet();

        first.SubjectAttributes["k"] = "v";

        Assert.Empty(second.SubjectAttributes);
    }
}
