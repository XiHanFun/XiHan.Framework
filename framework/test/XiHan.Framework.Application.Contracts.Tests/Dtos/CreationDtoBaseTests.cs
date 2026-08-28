// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts.Dtos;

namespace XiHan.Framework.Application.Contracts.Tests.Dtos;

/// <summary>
/// 创建 DTO 基类测试
/// </summary>
/// <remarks>
/// 创建入参与更新/删除入参的关键差异是「不带主键」——主键由服务端生成。
/// 这条差异只体现在成员的有无上，没有任何运行期校验兜底，所以用反射把它钉住：
/// 一旦有人给 <see cref="CreationDtoBase{TKey}"/> 加上 BasicId，本用例立刻失败。
/// </remarks>
public class CreationDtoBaseTests
{
    /// <summary>
    /// 两个基类都是抽象类，不允许直接实例化
    /// </summary>
    [Fact]
    public void Bases_AreAbstract()
    {
        Assert.True(typeof(CreationDtoBase).IsAbstract);
        Assert.True(typeof(CreationDtoBase<long>).IsAbstract);
    }

    /// <summary>
    /// 泛型基类继承自非泛型基类
    /// </summary>
    [Fact]
    public void GenericBase_InheritsNonGenericBase()
    {
        Assert.True(typeof(CreationDtoBase).IsAssignableFrom(typeof(CreationDtoBase<long>)));
        Assert.True(typeof(CreationDtoBase<long>).IsAssignableFrom(typeof(CreationDtoBaseTestDto)));
    }

    /// <summary>
    /// 创建 DTO 不携带主键：主键由服务端生成，不接受客户端指定
    /// </summary>
    [Fact]
    public void GenericBase_DoesNotDeclareKeyProperty()
    {
        Assert.Null(typeof(CreationDtoBase<long>).GetProperty("BasicId"));
        Assert.Empty(typeof(CreationDtoBase<long>).GetProperties());
        Assert.Empty(typeof(CreationDtoBase).GetProperties());
    }

    /// <summary>
    /// 具体创建 DTO 只暴露自己声明的字段，基类不会掺入额外字段
    /// </summary>
    [Fact]
    public void ConcreteDto_ExposesOnlyOwnProperties()
    {
        var names = typeof(CreationDtoBaseTestDto).GetProperties().Select(p => p.Name).ToArray();

        Assert.Single(names);
        Assert.Equal("Name", names[0]);
    }

    /// <summary>
    /// 泛型参数被约束为 IEquatable，保证主键类型可比较
    /// </summary>
    [Fact]
    public void KeyTypeParameter_IsConstrainedToEquatable()
    {
        var keyParameter = typeof(CreationDtoBase<>).GetGenericArguments()[0];

        Assert.Contains(keyParameter.GetGenericParameterConstraints(),
            constraint => constraint.IsGenericType
                && constraint.GetGenericTypeDefinition() == typeof(IEquatable<>));
    }
}

/// <summary>
/// 具体创建 DTO
/// </summary>
internal sealed class CreationDtoBaseTestDto : CreationDtoBase<long>
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
