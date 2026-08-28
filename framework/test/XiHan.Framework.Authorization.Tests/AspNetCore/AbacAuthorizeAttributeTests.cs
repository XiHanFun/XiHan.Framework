// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Authorization.AspNetCore;

namespace XiHan.Framework.Authorization.Tests.AspNetCore;

/// <summary>
/// ABAC 授权特性测试
/// </summary>
/// <remarks>
/// 该特性只带策略、不带权限码，生成的策略名里 p 段必须留空——策略提供器正是靠 p 段为空来跳过 RBAC 检查的。
/// </remarks>
public class AbacAuthorizeAttributeTests
{
    /// <summary>
    /// 策略编码去掉首尾空白后保存
    /// </summary>
    [Fact]
    public void Ctor_TrimsPolicyCode()
    {
        var attribute = new AbacAuthorizeAttribute("  self_only  ");

        Assert.Equal("self_only", attribute.AbacPolicyCode);
    }

    /// <summary>
    /// 策略编码为 null 时抛空引用参数异常
    /// </summary>
    [Fact]
    public void Ctor_WithNullPolicyCode_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AbacAuthorizeAttribute(null!));
    }

    /// <summary>
    /// 策略编码为空白时抛参数异常
    /// </summary>
    /// <param name="policyCode">策略编码</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_WithBlankPolicyCode_Throws(string policyCode)
    {
        Assert.Throws<ArgumentException>(() => new AbacAuthorizeAttribute(policyCode));
    }

    /// <summary>
    /// 生成的策略名 p 段为空、a 段承载策略编码
    /// </summary>
    [Fact]
    public void Policy_LeavesPermissionSegmentEmpty()
    {
        var attribute = new AbacAuthorizeAttribute("self_only");

        Assert.Equal("xihan.hybrid:p=;a=self_only", attribute.Policy);
    }

    /// <summary>
    /// 策略编码里的分隔符字符会被百分号编码，不会撑破协议
    /// </summary>
    [Fact]
    public void Policy_EncodesProtocolDelimiters()
    {
        var attribute = new AbacAuthorizeAttribute("subject.tenant_id == resource.tenant_id");

        Assert.Equal(
            "xihan.hybrid:p=;a=subject.tenant_id%20%3D%3D%20resource.tenant_id",
            attribute.Policy);
    }

    /// <summary>
    /// 特性允许叠加，且可被派生类继承
    /// </summary>
    [Fact]
    public void AttributeUsage_AllowsMultipleAndInherits()
    {
        var usage = typeof(AbacAuthorizeAttribute).GetCustomAttribute<AttributeUsageAttribute>(inherit: false);

        Assert.NotNull(usage);
        Assert.True(usage!.AllowMultiple);
        Assert.True(usage.Inherited);
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Method, usage.ValidOn);
    }
}
