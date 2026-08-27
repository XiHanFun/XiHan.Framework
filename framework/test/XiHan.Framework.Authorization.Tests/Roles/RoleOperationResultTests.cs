// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization.Tests.Roles;

/// <summary>
/// 角色操作结果测试
/// </summary>
/// <remarks>
/// 成功结果里的角色数据是可选的，失败结果里的错误码也是可选的，两个工厂方法的默认值语义要锁死。
/// </remarks>
public class RoleOperationResultTests
{
    /// <summary>
    /// 新建实例默认失败且无任何附加信息
    /// </summary>
    [Fact]
    public void New_ByDefault_IsNotSucceeded()
    {
        var result = new RoleOperationResult();

        Assert.False(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.Role);
    }

    /// <summary>
    /// 成功结果不带角色数据时角色为 null
    /// </summary>
    [Fact]
    public void Success_WithoutRole_LeavesRoleNull()
    {
        var result = RoleOperationResult.Success();

        Assert.True(result.Succeeded);
        Assert.Null(result.Role);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// 成功结果携带角色数据时按引用保存
    /// </summary>
    [Fact]
    public void Success_WithRole_KeepsReference()
    {
        var role = new RoleDefinition("r1", "admin", "管理员");

        var result = RoleOperationResult.Success(role);

        Assert.True(result.Succeeded);
        Assert.Same(role, result.Role);
    }

    /// <summary>
    /// 失败结果不带错误码时错误码为 null
    /// </summary>
    [Fact]
    public void Failure_WithoutErrorCode_LeavesCodeNull()
    {
        var result = RoleOperationResult.Failure("角色不存在");

        Assert.False(result.Succeeded);
        Assert.Equal("角色不存在", result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.Role);
    }

    /// <summary>
    /// 失败结果保留错误码
    /// </summary>
    [Fact]
    public void Failure_WithErrorCode_KeepsCode()
    {
        var result = RoleOperationResult.Failure("角色不存在", "ROLE_NOT_FOUND");

        Assert.Equal("ROLE_NOT_FOUND", result.ErrorCode);
    }
}
