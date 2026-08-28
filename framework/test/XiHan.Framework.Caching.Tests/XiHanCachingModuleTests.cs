// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Serialization;
using XiHan.Framework.Threading;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 曦寒缓存模块测试
/// </summary>
/// <remarks>
/// 模块依赖声明决定启动期的装配顺序：缓存的键规范化依赖多租户、序列化依赖序列化模块、
/// 取消令牌依赖线程模块、considerUow 分支依赖工作单元模块。漏声明会表现为启动期随机的解析失败，
/// 只能靠这类静态断言提前拦住。
/// </remarks>
public class XiHanCachingModuleTests
{
    /// <summary>
    /// 缓存模块是标准的曦寒模块
    /// </summary>
    [Fact]
    public void Module_DerivesFromXiHanModule()
    {
        Assert.True(typeof(XiHanCachingModule).IsSubclassOf(typeof(XiHanModule)));
    }

    /// <summary>
    /// 缓存模块声明了全部四个前置依赖模块
    /// </summary>
    [Fact]
    public void Module_DeclaresExpectedDependencies()
    {
        var dependedTypes = GetDependsOnAttribute().GetDependedTypes();

        Assert.Equal(4, dependedTypes.Length);
        Assert.Contains(typeof(XiHanMultiTenancyAbstractionsModule), dependedTypes);
        Assert.Contains(typeof(XiHanSerializationModule), dependedTypes);
        Assert.Contains(typeof(XiHanThreadingModule), dependedTypes);
        Assert.Contains(typeof(XiHanUowModule), dependedTypes);
    }

    /// <summary>
    /// 依赖类型集合与取值方法给出的结果一致
    /// </summary>
    [Fact]
    public void DependsOnAttribute_ExposesSameTypesFromBothAccessors()
    {
        var attribute = GetDependsOnAttribute();

        Assert.Equal(attribute.DependedTypes, attribute.GetDependedTypes());
    }

    /// <summary>
    /// 取缓存模块上的依赖声明特性
    /// </summary>
    /// <returns>依赖声明特性</returns>
    private static DependsOnAttribute GetDependsOnAttribute()
    {
        return typeof(XiHanCachingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .Single();
    }
}
