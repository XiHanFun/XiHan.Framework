#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTenantDataFilter
// Guid:304b6e5e-b99b-4a08-849f-f08ea9e4f98f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using SqlSugar;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Data.SqlSugar.Tenancy;

/// <summary>
/// SqlSugar 租户数据过滤器
/// </summary>
public static class XiHanTenantDataFilter
{
    private static readonly AsyncLocal<bool?> FilterEnabled = new();

    /// <summary>
    /// 过滤器是否启用
    /// </summary>
    public static bool IsEnabled => FilterEnabled.Value ?? true;

    /// <summary>
    /// 禁用租户过滤（作用域结束后自动恢复）
    /// </summary>
    /// <returns></returns>
    public static IDisposable Disable()
    {
        var previous = FilterEnabled.Value;
        FilterEnabled.Value = false;
        return new DisposeAction(() => FilterEnabled.Value = previous);
    }

    /// <summary>
    /// 获取当前租户唯一标识（long）
    /// </summary>
    /// <returns></returns>
    public static long? GetCurrentTenantId()
    {
        var tenantInfo = AsyncLocalCurrentTenantAccessor.Instance.Current;
        if (tenantInfo is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(tenantInfo.Name) && long.TryParse(tenantInfo.Name, out var tenantId))
        {
            return tenantId;
        }

        if (tenantInfo.TenantId.HasValue)
        {
            var bytes = tenantInfo.TenantId.Value.ToByteArray();
            return BitConverter.ToInt64(bytes, 0);
        }

        return null;
    }

    /// <summary>
    /// 创建租户过滤查询
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="queryable"></param>
    /// <returns></returns>
    public static ISugarQueryable<TEntity> ApplyTenantFilter<TEntity>(ISugarQueryable<TEntity> queryable)
        where TEntity : class, new()
    {
        if (!IsEnabled)
        {
            return queryable;
        }

        var tenantId = GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            return queryable;
        }

        var property = typeof(TEntity).GetProperty("TenantId");
        if (property is null)
        {
            return queryable;
        }

        if (property.PropertyType != typeof(long) && property.PropertyType != typeof(long?))
        {
            return queryable;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var propertyAccess = Expression.Property(parameter, property);
        Expression targetValue = property.PropertyType == typeof(long)
            ? Expression.Constant(tenantId.Value, typeof(long))
            : Expression.Convert(Expression.Constant(tenantId.Value, typeof(long)), typeof(long?));
        var body = Expression.Equal(propertyAccess, targetValue);
        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        return queryable.Where(lambda);
    }

    /// <summary>
    /// 尝试给实体设置 TenantId
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="entity"></param>
    public static void TrySetTenantId<TEntity>(TEntity entity)
        where TEntity : class
    {
        var tenantId = GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            return;
        }

        var property = typeof(TEntity).GetProperty("TenantId");
        if (property is null || !property.CanWrite)
        {
            return;
        }

        if (property.PropertyType == typeof(long))
        {
            var currentValue = (long)property.GetValue(entity)!;
            if (currentValue == 0)
            {
                property.SetValue(entity, tenantId.Value);
            }
            return;
        }

        if (property.PropertyType == typeof(long?))
        {
            var currentValue = (long?)property.GetValue(entity);
            if (!currentValue.HasValue || currentValue.Value == 0)
            {
                property.SetValue(entity, tenantId.Value);
            }
        }
    }
}
