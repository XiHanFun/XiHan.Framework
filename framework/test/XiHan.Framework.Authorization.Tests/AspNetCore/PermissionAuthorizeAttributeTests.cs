// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Authorization.AspNetCore;

namespace XiHan.Framework.Authorization.Tests.AspNetCore;

/// <summary>
/// 权限授权特性测试
/// </summary>
/// <remarks>
/// 该特性把权限编码与 ABAC 策略编码编码进 <c>Policy</c> 字符串，这是与策略提供器之间唯一的传输通道，
/// 属于必须锁死的字符串协议：前缀、分隔符、百分号编码一旦改动，所有已标注的接口会集体失去策略。
/// </remarks>
public class PermissionAuthorizeAttributeTests
{
    /// <summary>
    /// 单参构造时 ABAC 策略编码为空串而不是 null
    /// </summary>
    [Fact]
    public void Ctor_WithPermissionOnly_LeavesAbacPolicyEmpty()
    {
        var attribute = new PermissionAuthorizeAttribute("Sys.User.Create");

        Assert.Equal("Sys.User.Create", attribute.PermissionCode);
        Assert.Equal(string.Empty, attribute.AbacPolicyCode);
    }

    /// <summary>
    /// 两个编码都会去掉首尾空白
    /// </summary>
    [Fact]
    public void Ctor_TrimsBothCodes()
    {
        var attribute = new PermissionAuthorizeAttribute("  Sys.User.Create  ", "  same_tenant  ");

        Assert.Equal("Sys.User.Create", attribute.PermissionCode);
        Assert.Equal("same_tenant", attribute.AbacPolicyCode);
    }

    /// <summary>
    /// ABAC 策略编码传 null 时归一成空串
    /// </summary>
    [Fact]
    public void Ctor_WithNullAbacPolicy_NormalizesToEmpty()
    {
        var attribute = new PermissionAuthorizeAttribute("Sys.User.Create", null);

        Assert.Equal(string.Empty, attribute.AbacPolicyCode);
    }

    /// <summary>
    /// 权限编码为 null 时抛空引用参数异常
    /// </summary>
    [Fact]
    public void Ctor_WithNullPermission_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PermissionAuthorizeAttribute(null!));
    }

    /// <summary>
    /// 权限编码为空白时抛参数异常
    /// </summary>
    /// <param name="permissionCode">权限编码</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_WithBlankPermission_Throws(string permissionCode)
    {
        Assert.Throws<ArgumentException>(() => new PermissionAuthorizeAttribute(permissionCode));
    }

    /// <summary>
    /// 策略名严格按 “前缀 + p=权限 + 分号 + a=策略” 的形状生成，取值经过百分号编码
    /// </summary>
    [Fact]
    public void Policy_UsesHybridWireFormat()
    {
        var attribute = new PermissionAuthorizeAttribute("Sys:User:Create", "same_tenant");

        Assert.Equal("xihan.hybrid:p=Sys%3AUser%3ACreate;a=same_tenant", attribute.Policy);
    }

    /// <summary>
    /// 不带 ABAC 策略时 a 段留空但仍然保留
    /// </summary>
    [Fact]
    public void Policy_WithoutAbacPolicy_KeepsEmptySegment()
    {
        var attribute = new PermissionAuthorizeAttribute("read");

        Assert.Equal("xihan.hybrid:p=read;a=", attribute.Policy);
    }

    /// <summary>
    /// 分号与等号这类会破坏协议的字符必须被编码掉
    /// </summary>
    [Fact]
    public void Policy_EncodesProtocolDelimiters()
    {
        var attribute = new PermissionAuthorizeAttribute("a;b=c");

        Assert.Equal("xihan.hybrid:p=a%3Bb%3Dc;a=", attribute.Policy);
    }

    /// <summary>
    /// 特性允许叠加，且可被派生类继承
    /// </summary>
    [Fact]
    public void AttributeUsage_AllowsMultipleAndInherits()
    {
        var usage = typeof(PermissionAuthorizeAttribute).GetCustomAttribute<AttributeUsageAttribute>(inherit: false);

        Assert.NotNull(usage);
        Assert.True(usage!.AllowMultiple);
        Assert.True(usage.Inherited);
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Method, usage.ValidOn);
    }
}
