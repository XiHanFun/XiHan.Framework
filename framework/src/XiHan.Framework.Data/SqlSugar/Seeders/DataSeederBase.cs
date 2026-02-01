#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DataSeederBase
// Guid:4a5b6c7d-8e9f-0a1b-2c3d-4e5f6a7b8c9d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/05 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using XiHan.Framework.DistributedIds;

namespace XiHan.Framework.Data.SqlSugar.Seeders;

/// <summary>
/// 数据种子基类
/// </summary>
public abstract class DataSeederBase : IDataSeeder
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// 数据库上下文
    /// </summary>
    protected readonly ISqlSugarDbContext DbContext;

    /// <summary>
    /// 服务提供者
    /// </summary>
    protected readonly IServiceProvider ServiceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">数据库上下文</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="serviceProvider">服务提供者</param>
    protected DataSeederBase(ISqlSugarDbContext dbContext, ILogger logger, IServiceProvider serviceProvider)
    {
        DbContext = dbContext;
        Logger = logger;
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 种子数据优先级（数字越小优先级越高）
    /// </summary>
    public abstract int Order { get; }

    /// <summary>
    /// 种子数据名称
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 种子数据
    /// </summary>
    public virtual async Task SeedAsync()
    {
        try
        {
            Logger.LogInformation("开始执行种子数据: {SeederName}", Name);
            await SeedInternalAsync();
            Logger.LogInformation("种子数据执行成功: {SeederName}", Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "种子数据执行失败: {SeederName}, 错误: {Error}", Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 种子数据内部实现
    /// </summary>
    protected abstract Task SeedInternalAsync();

    /// <summary>
    /// 检查数据是否已存在
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="predicate">条件表达式</param>
    /// <returns></returns>
    protected async Task<bool> HasDataAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : class, new()
    {
        return await DbContext.GetClient().Queryable<T>().AnyAsync(predicate);
    }

    /// <summary>
    /// 批量插入数据
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体列表</param>
    protected async Task BulkInsertAsync<T>(List<T> entities) where T : class, new()
    {
        if (entities.Count == 0)
        {
            return;
        }

        // 自动为实体生成 ID
        AssignIdsIfNeeded(entities);

        await DbContext.GetClient().Insertable(entities).ExecuteCommandAsync();
        Logger.LogInformation("已插入 {Count} 条 {EntityType} 数据", entities.Count, typeof(T).Name);
    }

    /// <summary>
    /// 如果需要，自动为实体分配 ID
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体列表</param>
    private void AssignIdsIfNeeded<T>(List<T> entities) where T : class
    {
        if (entities.Count == 0)
        {
            return;
        }

        var entityType = typeof(T);
        var basicIdProperty = entityType.GetProperty("BasicId");

        // 如果没有 BasicId 属性，直接返回
        if (basicIdProperty == null)
        {
            return;
        }

        // 判断是否需要生成 ID
        var propertyType = basicIdProperty.PropertyType;

        // 处理 long 类型的 ID
        if (propertyType == typeof(long))
        {
            var idGenerator = ServiceProvider.GetService<IDistributedIdGenerator<long>>();
            if (idGenerator == null)
            {
                Logger.LogWarning("未找到 IDistributedIdGenerator<long> 服务，无法自动生成 ID");
                return;
            }

            foreach (var entity in entities)
            {
                var currentId = (long)(basicIdProperty.GetValue(entity) ?? 0L);
                // 如果 ID 为默认值（0），则生成新 ID
                if (currentId == 0L)
                {
                    var newId = idGenerator.NextId();
                    SetBasicId(entity, basicIdProperty, newId);
                }
            }
        }
        // 处理 Guid 类型的 ID
        else if (propertyType == typeof(Guid))
        {
            var idGenerator = ServiceProvider.GetService<IDistributedIdGenerator<Guid>>();
            if (idGenerator == null)
            {
                Logger.LogWarning("未找到 IDistributedIdGenerator<Guid> 服务，无法自动生成 ID");
                return;
            }

            foreach (var entity in entities)
            {
                var currentId = (Guid)(basicIdProperty.GetValue(entity) ?? Guid.Empty);
                // 如果 ID 为默认值（Empty），则生成新 ID
                if (currentId == Guid.Empty)
                {
                    var newId = idGenerator.NextId();
                    SetBasicId(entity, basicIdProperty, newId);
                }
            }
        }
    }

    /// <summary>
    /// 设置实体的 BasicId 属性
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <param name="property">BasicId 属性信息</param>
    /// <param name="id">要设置的 ID 值</param>
    private void SetBasicId<T, TKey>(T entity, PropertyInfo property, TKey id) where T : class
    {
        // 尝试使用 set 方法（如果可访问）
        if (property.CanWrite && property.SetMethod != null)
        {
            try
            {
                property.SetValue(entity, id);
                return;
            }
            catch
            {
                // 如果直接设置失败，使用反射强制设置
            }
        }

        // 使用反射强制设置 protected set 属性
        var backingField = entity.GetType()
            .GetField($"<{property.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        if (backingField != null)
        {
            backingField.SetValue(entity, id);
        }
        else
        {
            // 如果找不到自动属性的后备字段，尝试使用 SetMethod
            property.SetMethod?.Invoke(entity, [id]);
        }
    }
}
