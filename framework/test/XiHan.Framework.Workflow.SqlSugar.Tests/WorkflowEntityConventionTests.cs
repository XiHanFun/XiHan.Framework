// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using System.Reflection;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 工作流实体的共同约定测试
/// </summary>
public class WorkflowEntityConventionTests
{
    /// <summary>
    /// 实体不参与全局租户过滤
    /// </summary>
    [Fact]
    public void 实体不实现多租户接口()
    {
        foreach (var entityType in TestEntityTypes.All)
        {
            Assert.False(typeof(IMultiTenantEntity).IsAssignableFrom(entityType), entityType.Name);
        }
    }

    /// <summary>
    /// 实体没有会被插入审计改写的租户属性名
    /// </summary>
    [Fact]
    public void 实体没有名为TenantId的属性()
    {
        foreach (var entityType in TestEntityTypes.All)
        {
            Assert.Null(entityType.GetProperty("TenantId"));
        }
    }

    /// <summary>
    /// 实体只在平台库建表
    /// </summary>
    [Fact]
    public void 实体只在平台库建表()
    {
        foreach (var entityType in TestEntityTypes.All)
        {
            var attribute = entityType.GetCustomAttribute<TableInitializationAttribute>(inherit: true);

            Assert.NotNull(attribute);
            Assert.Equal(DbInitializationTarget.Platform, attribute.Target);
        }
    }

    /// <summary>
    /// 实体主键为字符串
    /// </summary>
    [Fact]
    public void 实体主键为字符串()
    {
        foreach (var entityType in TestEntityTypes.All)
        {
            var property = entityType.GetProperty("BasicId");

            Assert.NotNull(property);
            Assert.Equal(typeof(string), property.PropertyType);
        }
    }

    /// <summary>
    /// 实体索引名都带表名占位
    /// </summary>
    [Fact]
    public void 索引名都带表名占位()
    {
        foreach (var entityType in TestEntityTypes.All)
        {
            foreach (var index in entityType.GetCustomAttributes<SugarIndexAttribute>(inherit: true))
            {
                Assert.Contains("{table}", index.IndexName, StringComparison.Ordinal);
            }
        }
    }
}
