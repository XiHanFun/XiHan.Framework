// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using XiHan.Framework.Authorization.AspNetCore;

namespace XiHan.Framework.Authorization.Tests.AspNetCore;

/// <summary>
/// 混合权限要求测试
/// </summary>
/// <remarks>
/// 处理器用 <c>string.IsNullOrWhiteSpace</c> 判断该不该做 RBAC 或 ABAC 检查，
/// 因此这个要求对象必须保证两个编码永远是非 null 的规整值，否则会出现“看似配了策略其实没检查”的情况。
/// </remarks>
public class HybridPermissionRequirementTests
{
    /// <summary>
    /// 两个编码都去掉首尾空白
    /// </summary>
    [Fact]
    public void Ctor_TrimsBothCodes()
    {
        var requirement = new HybridPermissionRequirement("  Sys.User.Create  ", "  same_tenant  ");

        Assert.Equal("Sys.User.Create", requirement.PermissionCode);
        Assert.Equal("same_tenant", requirement.AbacPolicyCode);
    }

    /// <summary>
    /// 传 null 时归一成空串而不是保留 null
    /// </summary>
    [Fact]
    public void Ctor_WithNulls_NormalizesToEmpty()
    {
        var requirement = new HybridPermissionRequirement(null!, null!);

        Assert.Equal(string.Empty, requirement.PermissionCode);
        Assert.Equal(string.Empty, requirement.AbacPolicyCode);
    }

    /// <summary>
    /// 空白编码在裁剪后变成空串，等价于“未配置”
    /// </summary>
    [Fact]
    public void Ctor_WithWhitespace_BecomesEmpty()
    {
        var requirement = new HybridPermissionRequirement("   ", "\t");

        Assert.Equal(string.Empty, requirement.PermissionCode);
        Assert.Equal(string.Empty, requirement.AbacPolicyCode);
    }

    /// <summary>
    /// 该要求必须能被 ASP.NET Core 授权管线识别
    /// </summary>
    [Fact]
    public void Requirement_IsAspNetCoreAuthorizationRequirement()
    {
        var requirement = new HybridPermissionRequirement("read", "allow");

        Assert.IsAssignableFrom<IAuthorizationRequirement>(requirement);
    }
}
