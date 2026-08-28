// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Authorization.Permissions;

namespace XiHan.Framework.Authorization.Tests.Permissions;

/// <summary>
/// 权限定义测试
/// </summary>
/// <remarks>
/// 权限定义会被持久化和跨进程传输，字段名与默认值都是契约：
/// 尤其 <c>IsEnabled</c> 必须默认为真，否则新建的权限会集体失效；而 <c>Name</c> 是唯一键，默认必须是空串不是 null。
/// </remarks>
public class PermissionDefinitionTests
{
    /// <summary>
    /// 无参构造的默认值：名称为空串、启用、排序为零、可空字段为 null
    /// </summary>
    [Fact]
    public void New_ByDefault_UsesSafeDefaults()
    {
        var definition = new PermissionDefinition();

        Assert.Equal(string.Empty, definition.Name);
        Assert.Equal(string.Empty, definition.DisplayName);
        Assert.Null(definition.Description);
        Assert.Null(definition.ParentName);
        Assert.Null(definition.Tag);
        Assert.True(definition.IsEnabled);
        Assert.Equal(0, definition.Order);
        Assert.Null(definition.Properties);
    }

    /// <summary>
    /// 三参构造按位置写入名称、显示名与描述
    /// </summary>
    [Fact]
    public void Ctor_WithArguments_AssignsFields()
    {
        var definition = new PermissionDefinition("sys.user.create", "创建用户", "允许创建用户");

        Assert.Equal("sys.user.create", definition.Name);
        Assert.Equal("创建用户", definition.DisplayName);
        Assert.Equal("允许创建用户", definition.Description);
        Assert.True(definition.IsEnabled);
    }

    /// <summary>
    /// 描述参数可省略，省略时为 null
    /// </summary>
    [Fact]
    public void Ctor_WithoutDescription_LeavesItNull()
    {
        var definition = new PermissionDefinition("sys.user.create", "创建用户");

        Assert.Null(definition.Description);
    }

    /// <summary>
    /// 权限定义是引用相等语义，同值的两个实例不相等
    /// </summary>
    [Fact]
    public void Equality_IsReferenceBased()
    {
        var first = new PermissionDefinition("sys.user.create", "创建用户");
        var second = new PermissionDefinition("sys.user.create", "创建用户");

        Assert.NotEqual(first, second);
        Assert.Equal(first, first);
    }

    /// <summary>
    /// JSON 往返后字段名与取值保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesScalarFields()
    {
        var definition = new PermissionDefinition("sys.user.create", "create-user", "desc")
        {
            ParentName = "sys.user",
            Tag = "system",
            IsEnabled = false,
            Order = 7
        };

        var json = JsonSerializer.Serialize(definition);
        var restored = JsonSerializer.Deserialize<PermissionDefinition>(json);

        Assert.Contains("\"Name\":\"sys.user.create\"", json);
        Assert.Contains("\"IsEnabled\":false", json);
        Assert.NotNull(restored);
        Assert.Equal("sys.user.create", restored!.Name);
        Assert.Equal("create-user", restored.DisplayName);
        Assert.Equal("desc", restored.Description);
        Assert.Equal("sys.user", restored.ParentName);
        Assert.Equal("system", restored.Tag);
        Assert.False(restored.IsEnabled);
        Assert.Equal(7, restored.Order);
        Assert.Null(restored.Properties);
    }
}
