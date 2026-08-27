// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;

namespace XiHan.Framework.Authorization.Tests.Policies;

/// <summary>
/// 策略定义测试
/// </summary>
/// <remarks>
/// 评估器用 <c>Count &gt; 0</c> 决定要不要检查某一类要求，所以四个集合必须默认非空且为空集合，
/// 否则要么空引用、要么把“没配要求”误判成“要求不满足”。<c>IsEnabled</c> 默认为真同理。
/// </remarks>
public class PolicyDefinitionTests
{
    /// <summary>
    /// 无参构造的默认值
    /// </summary>
    [Fact]
    public void New_ByDefault_UsesSafeDefaults()
    {
        var policy = new PolicyDefinition();

        Assert.Equal(string.Empty, policy.Name);
        Assert.Equal(string.Empty, policy.DisplayName);
        Assert.Null(policy.Description);
        Assert.Empty(policy.RequiredRoles);
        Assert.Empty(policy.RequiredPermissions);
        Assert.Empty(policy.RequiredClaims);
        Assert.Empty(policy.CustomRequirements);
        Assert.True(policy.IsEnabled);
        Assert.Null(policy.Properties);
    }

    /// <summary>
    /// 三参构造按位置写入名称、显示名与描述
    /// </summary>
    [Fact]
    public void Ctor_WithArguments_AssignsFields()
    {
        var policy = new PolicyDefinition("tenant-admin", "租户管理员", "限定租户内的管理操作");

        Assert.Equal("tenant-admin", policy.Name);
        Assert.Equal("租户管理员", policy.DisplayName);
        Assert.Equal("限定租户内的管理操作", policy.Description);
        Assert.True(policy.IsEnabled);
    }

    /// <summary>
    /// 描述参数可省略
    /// </summary>
    [Fact]
    public void Ctor_WithoutDescription_LeavesItNull()
    {
        Assert.Null(new PolicyDefinition("tenant-admin", "租户管理员").Description);
    }

    /// <summary>
    /// 两个实例之间不共享集合引用
    /// </summary>
    [Fact]
    public void New_TwoInstances_DoNotShareCollections()
    {
        var first = new PolicyDefinition();
        var second = new PolicyDefinition();

        first.RequiredRoles.Add("admin");
        first.RequiredPermissions.Add("read");
        first.RequiredClaims["scope"] = "full";

        Assert.Empty(second.RequiredRoles);
        Assert.Empty(second.RequiredPermissions);
        Assert.Empty(second.RequiredClaims);
    }

    /// <summary>
    /// 声明要求的键使用字典默认（区分大小写）比较，重复赋值按后写覆盖
    /// </summary>
    [Fact]
    public void RequiredClaims_OverwritesSameKey()
    {
        var policy = new PolicyDefinition();

        policy.RequiredClaims["scope"] = "read";
        policy.RequiredClaims["scope"] = "write";

        Assert.Equal("write", Assert.Single(policy.RequiredClaims).Value);
    }
}
