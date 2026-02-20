#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EntityAuditExtensions
// Guid:58f8f513-d60e-4f38-9f68-bf8b2f885f1e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 14:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.Reflection;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Extensions;

/// <summary>
/// 实体审计扩展方法。用于在 SqlSugar DataExecuting 等场景下，按属性名映射自动填充创建/修改/删除审计字段及租户标识。
/// </summary>
public static class EntityAuditExtensions
{
    /// <summary>
    /// 主键为空时赋值雪花Id。仅当主键为 long 或 long? 且非自增、当前值为默认值时写入。
    /// </summary>
    /// <param name="entityInfo">SqlSugar 数据过滤模型</param>
    /// <param name="idValue">雪花 Id 值</param>
    public static void TrySetSnowflakeId(this DataFilterModel entityInfo, long idValue)
    {
        if (!entityInfo.EntityColumnInfo.IsPrimarykey || entityInfo.EntityColumnInfo.IsIdentity)
        {
            return;
        }

        var propertyInfo = entityInfo.EntityColumnInfo.PropertyInfo;
        if (propertyInfo is null)
        {
            return;
        }

        if (propertyInfo.PropertyType != typeof(long) && propertyInfo.PropertyType != typeof(long?))
        {
            return;
        }

        if (!IsDefaultValue(propertyInfo.GetValue(entityInfo.EntityValue)))
        {
            return;
        }

        entityInfo.SetValue(idValue);
    }

    /// <summary>
    /// 按“新增”语义赋值：CreatedTime、CreatedId、CreatedBy、TenantId；若当前属性为 IsDeleted 则置为 false。
    /// </summary>
    /// <param name="entityInfo">SqlSugar 数据过滤模型</param>
    /// <param name="context">审计上下文（当前用户、租户等）</param>
    public static void ToCreated(this DataFilterModel entityInfo, EntityAuditContext context)
    {
        CommonTo(entityInfo, new AuditPropertyMap
        {
            HandleId = nameof(ICreationEntity<>.CreatedId),
            HandleBy = nameof(ICreationEntity<>.CreatedBy),
            HandleTime = nameof(ICreationEntity.CreatedTime),
            TenantId = nameof(IMultiTenantEntity.TenantId)
        }, context);

        if (entityInfo.PropertyName == nameof(IDeletionEntity.IsDeleted))
        {
            var propertyInfo = entityInfo.EntityColumnInfo.PropertyInfo;
            if (propertyInfo is not null && propertyInfo.PropertyType == typeof(bool))
            {
                entityInfo.SetValue(false);
            }
        }
    }

    /// <summary>
    /// 按“修改”语义赋值：ModifiedTime、ModifiedId、ModifiedBy（会覆盖已有值）。
    /// </summary>
    /// <param name="entityInfo">SqlSugar 数据过滤模型</param>
    /// <param name="context">审计上下文（当前用户、租户等）</param>
    public static void ToModified(this DataFilterModel entityInfo, EntityAuditContext context)
    {
        CommonTo(entityInfo, new AuditPropertyMap
        {
            HandleId = nameof(IModificationEntity<>.ModifiedId),
            HandleBy = nameof(IModificationEntity<>.ModifiedBy),
            HandleTime = nameof(IModificationEntity.ModifiedTime)
        }, context, overrideHandleValue: true);
    }

    /// <summary>
    /// 按“删除”语义赋值：DeletedTime、DeletedId、DeletedBy。仅当实体 IsDeleted 为 true 时对当前字段生效。
    /// </summary>
    /// <param name="entityInfo">SqlSugar 数据过滤模型</param>
    /// <param name="context">审计上下文（当前用户、租户等）</param>
    public static void ToDeleted(this DataFilterModel entityInfo, EntityAuditContext context)
    {
        if (entityInfo.EntityValue is null)
        {
            return;
        }

        var isDeleted = TryGetBoolProperty(entityInfo.EntityValue, nameof(IDeletionEntity.IsDeleted));
        if (isDeleted != true)
        {
            return;
        }

        CommonTo(entityInfo, new AuditPropertyMap
        {
            HandleId = nameof(IDeletionEntity<>.DeletedId),
            HandleBy = nameof(IDeletionEntity<>.DeletedBy),
            HandleTime = nameof(IDeletionEntity.DeletedTime)
        }, context);
    }

    /// <summary>
    /// 根据属性映射对当前正在执行的字段进行审计赋值（处理人 Id/名称、时间、租户）。仅当 entityInfo.PropertyName 与映射匹配时写入。
    /// </summary>
    /// <param name="entityInfo">数据过滤模型</param>
    /// <param name="propertyMap">属性名映射（HandleId、HandleBy、HandleTime、TenantId）</param>
    /// <param name="context">审计上下文</param>
    /// <param name="overrideHandleValue">为 true 时覆盖已有值；为 false 时仅当当前值为默认值时才写入</param>
    private static void CommonTo(DataFilterModel entityInfo, AuditPropertyMap propertyMap, EntityAuditContext context, bool overrideHandleValue = false)
    {
        if (entityInfo.EntityValue is null)
        {
            return;
        }

        var propertyInfo = entityInfo.EntityColumnInfo.PropertyInfo;
        if (propertyInfo is null)
        {
            return;
        }

        var propertyName = entityInfo.PropertyName;

        if (!string.IsNullOrWhiteSpace(propertyMap.HandleTime) && propertyName == propertyMap.HandleTime)
        {
            SetDateTimeValue(entityInfo, propertyInfo, overrideHandleValue);
            return;
        }

        if (!string.IsNullOrWhiteSpace(propertyMap.HandleId) && propertyName == propertyMap.HandleId)
        {
            SetValueIfNeeded(entityInfo, propertyInfo, context.UserId, overrideHandleValue);
            return;
        }

        if (!string.IsNullOrWhiteSpace(propertyMap.HandleBy) && propertyName == propertyMap.HandleBy)
        {
            if (!string.IsNullOrWhiteSpace(context.UserName))
            {
                SetValueIfNeeded(entityInfo, propertyInfo, context.UserName, overrideHandleValue);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(propertyMap.TenantId) && propertyName == propertyMap.TenantId)
        {
            SetValueIfNeeded(entityInfo, propertyInfo, context.TenantId, overrideHandleValue);
        }
    }

    /// <summary>
    /// 设置时间类型属性：支持 DateTime/DateTimeOffset 及可空类型，根据 overrideHandleValue 或当前是否为默认值决定是否写入。
    /// </summary>
    private static void SetDateTimeValue(DataFilterModel entityInfo, PropertyInfo propertyInfo, bool overrideHandleValue)
    {
        if (propertyInfo.PropertyType == typeof(DateTimeOffset) || propertyInfo.PropertyType == typeof(DateTimeOffset?))
        {
            if (overrideHandleValue || IsDefaultValue(propertyInfo.GetValue(entityInfo.EntityValue)))
            {
                entityInfo.SetValue(DateTimeOffset.Now);
            }
            return;
        }

        if (propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))
        {
            if (overrideHandleValue || IsDefaultValue(propertyInfo.GetValue(entityInfo.EntityValue)))
            {
                entityInfo.SetValue(DateTime.Now);
            }
        }
    }

    /// <summary>
    /// 将 sourceValue 转换为属性类型后写入实体；若 overrideHandleValue 为 false 则仅当属性当前为默认值时写入。
    /// </summary>
    private static void SetValueIfNeeded(DataFilterModel entityInfo, PropertyInfo propertyInfo, object? sourceValue, bool overrideHandleValue)
    {
        if (!TryConvertValue(sourceValue, propertyInfo.PropertyType, out var convertedValue))
        {
            return;
        }

        var currentValue = propertyInfo.GetValue(entityInfo.EntityValue);
        if (!overrideHandleValue && !IsDefaultValue(currentValue))
        {
            return;
        }

        entityInfo.SetValue(convertedValue);
    }

    /// <summary>
    /// 判断值是否为“未设置”的默认值（如 0、default 时间、空字符串、Guid.Empty 等）。
    /// </summary>
    private static bool IsDefaultValue(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is long idValue)
        {
            return idValue == 0;
        }

        if (value is int intValue)
        {
            return intValue == 0;
        }

        if (value is DateTime dateTime)
        {
            return dateTime == default;
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset == default;
        }

        if (value is Guid guid)
        {
            return guid == Guid.Empty;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        return false;
    }

    /// <summary>
    /// 将 value 转为 targetType（支持可空类型及常见类型转换），转换成功时返回 true 并输出 convertedValue。
    /// </summary>
    private static bool TryConvertValue(object? value, Type targetType, out object convertedValue)
    {
        convertedValue = default!;
        if (value is null)
        {
            return false;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var sourceType = value.GetType();

        if (underlyingType.IsAssignableFrom(sourceType))
        {
            convertedValue = value;
            return true;
        }

        try
        {
            if (underlyingType == typeof(string))
            {
                convertedValue = value.ToString()!;
                return true;
            }

            convertedValue = Convert.ChangeType(value, underlyingType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取实体上指定名称的 bool 属性值；若属性不存在或类型不是 bool 则返回 null。
    /// </summary>
    private static bool? TryGetBoolProperty(object entity, string propertyName)
    {
        var propertyInfo = entity.GetType().GetProperty(propertyName);
        if (propertyInfo is null || propertyInfo.PropertyType != typeof(bool))
        {
            return null;
        }

        var value = propertyInfo.GetValue(entity);
        return value is bool boolValue ? boolValue : null;
    }

    /// <summary>
    /// 属性映射
    /// </summary>
    private sealed class AuditPropertyMap
    {
        /// <summary>
        /// 处理人主键
        /// </summary>
        public string? HandleId { get; init; }

        /// <summary>
        /// 处理人名称
        /// </summary>
        public string? HandleBy { get; init; }

        /// <summary>
        /// 处理时间
        /// </summary>
        public string? HandleTime { get; init; }

        /// <summary>
        /// 租户主键
        /// </summary>
        public string? TenantId { get; init; }
    }
}
