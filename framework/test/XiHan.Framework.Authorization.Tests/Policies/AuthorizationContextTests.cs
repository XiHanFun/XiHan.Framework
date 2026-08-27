// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;

namespace XiHan.Framework.Authorization.Tests.Policies;

/// <summary>
/// 授权上下文测试
/// </summary>
/// <remarks>
/// 这个上下文交给用户自定义要求读取，集合字段必须默认非空，否则每个自定义要求都要先判空。
/// </remarks>
public class AuthorizationContextTests
{
    /// <summary>
    /// 无参构造的默认值
    /// </summary>
    [Fact]
    public void New_ByDefault_UsesSafeDefaults()
    {
        var context = new AuthorizationContext();

        Assert.Equal(string.Empty, context.UserId);
        Assert.Equal(string.Empty, context.PolicyName);
        Assert.Empty(context.UserRoles);
        Assert.Empty(context.UserPermissions);
        Assert.Empty(context.UserClaims);
        Assert.Null(context.Resource);
        Assert.Null(context.AdditionalData);
    }

    /// <summary>
    /// 两个实例之间不共享集合引用
    /// </summary>
    [Fact]
    public void New_TwoInstances_DoNotShareCollections()
    {
        var first = new AuthorizationContext();
        var second = new AuthorizationContext();

        first.UserRoles.Add("admin");
        first.UserPermissions.Add("read");
        first.UserClaims["scope"] = "full";

        Assert.Empty(second.UserRoles);
        Assert.Empty(second.UserPermissions);
        Assert.Empty(second.UserClaims);
    }

    /// <summary>
    /// 资源对象按引用保存，不做拷贝
    /// </summary>
    [Fact]
    public void Resource_IsStoredByReference()
    {
        var resource = new object();
        var context = new AuthorizationContext { Resource = resource };

        Assert.Same(resource, context.Resource);
    }
}
