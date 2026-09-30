// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Authorization.SqlSugar.Mapping;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 策略映射器测试
/// </summary>
public class PolicyMapperTests
{
    /// <summary>
    /// 策略定义往返后逐字段相等
    /// </summary>
    [Fact]
    public void 策略定义往返后逐字段相等()
    {
        var definition = new PolicyDefinition("AdminOnly", "仅管理员", "只允许管理员访问")
        {
            RequiredRoles = ["admin", "owner"],
            RequiredPermissions = ["User.Create", "User.Delete"],
            RequiredClaims = new Dictionary<string, string> { ["dept"] = "it", ["level"] = "" },
            IsEnabled = false
        };

        var entity = PolicyMapper.ToEntity(definition, 42L);

        Assert.Equal(42L, entity.BasicId);
        Assert.Equal("AdminOnly", entity.PolicyName);

        var restored = PolicyMapper.ToDefinition(entity);

        Assert.Equal("AdminOnly", restored.Name);
        Assert.Equal("仅管理员", restored.DisplayName);
        Assert.Equal("只允许管理员访问", restored.Description);
        Assert.Equal(new[] { "admin", "owner" }, restored.RequiredRoles);
        Assert.Equal(new[] { "User.Create", "User.Delete" }, restored.RequiredPermissions);
        Assert.Equal(2, restored.RequiredClaims.Count);
        Assert.Equal("it", restored.RequiredClaims["dept"]);
        Assert.Equal("", restored.RequiredClaims["level"]);
        Assert.False(restored.IsEnabled);
        Assert.Null(restored.Properties);
    }

    /// <summary>
    /// 空集合存为空数组与空对象
    /// </summary>
    [Fact]
    public void 空集合存为空数组与空对象()
    {
        var entity = PolicyMapper.ToEntity(new PolicyDefinition("P", "策略"), 1L);

        Assert.Equal("[]", entity.RequiredRoles);
        Assert.Equal("[]", entity.RequiredPermissions);
        Assert.Equal("{}", entity.RequiredClaims);
    }

    /// <summary>
    /// 要求集合为空引用时映射抛异常，不兜底为空集合
    /// </summary>
    [Fact]
    public void 要求集合为空引用时映射抛异常()
    {
        Assert.Throws<ArgumentException>(() => PolicyMapper.ToEntity(new PolicyDefinition("P", "策略") { RequiredRoles = null! }, 1L));
        Assert.Throws<ArgumentException>(() => PolicyMapper.ToEntity(new PolicyDefinition("P", "策略") { RequiredPermissions = null! }, 1L));
        Assert.Throws<ArgumentException>(() => PolicyMapper.ToEntity(new PolicyDefinition("P", "策略") { RequiredClaims = null! }, 1L));
    }

    /// <summary>
    /// 列为空文本时读回空集合而不是空引用
    /// </summary>
    [Fact]
    public void 列为空文本时读回空集合()
    {
        var entity = new SysAuthzPolicy(1L)
        {
            PolicyName = "P",
            DisplayName = "策略",
            RequiredRoles = "",
            RequiredPermissions = " ",
            RequiredClaims = ""
        };

        var definition = PolicyMapper.ToDefinition(entity);

        Assert.NotNull(definition.RequiredRoles);
        Assert.Empty(definition.RequiredRoles);
        Assert.NotNull(definition.RequiredPermissions);
        Assert.Empty(definition.RequiredPermissions);
        Assert.NotNull(definition.RequiredClaims);
        Assert.Empty(definition.RequiredClaims);
    }

    /// <summary>
    /// 映射器不读取自定义要求
    /// </summary>
    [Fact]
    public void 映射器不读取自定义要求()
    {
        var definition = new PolicyDefinition("P", "策略")
        {
            CustomRequirements = [new AlwaysPassRequirement()]
        };

        var restored = PolicyMapper.ToDefinition(PolicyMapper.ToEntity(definition, 1L));

        Assert.Empty(restored.CustomRequirements);
    }

    /// <summary>
    /// 恒通过的自定义要求
    /// </summary>
    private sealed class AlwaysPassRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// 要求名称
        /// </summary>
        public string Name => "always-pass";

        /// <summary>
        /// 评估授权要求
        /// </summary>
        /// <param name="context">授权上下文</param>
        /// <returns>恒为 true</returns>
        public Task<bool> EvaluateAsync(AuthorizationContext context)
        {
            return Task.FromResult(true);
        }
    }
}
