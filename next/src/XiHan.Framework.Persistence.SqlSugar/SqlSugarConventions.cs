// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using System.Reflection;
using SqlSugar;
using XiHan.Framework.Domain;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 按实体协议配置 SqlSugar 的实体映射，实体不需要继承 SqlSugar 基类或标注 <c>[SugarColumn]</c>：
/// <c>Id</c> 是主键（不自增），<see cref="IHasConcurrencyStamp"/> 的并发戳长度 32，<see cref="DateTimeOffset"/> 按 UTC 时刻往返，可空值类型与可空引用类型的列可空，没有公开 setter 的属性与集合属性不映射到列。
/// 由本特性创建客户端时自动应用；应用自己注册 <c>ISqlSugarClient</c> 时，须在创建客户端前对 <c>ConnectionConfig</c> 调用 <see cref="Apply"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SqlSugarConventions
{
    /// <summary>
    /// 判定引用类型属性可空性的上下文；线程安全，可复用。
    /// </summary>
    private static readonly NullabilityInfoContext _nullability = new();

    /// <summary>
    /// 在连接配置上登记实体服务回调；配置里已有的回调先执行，本约定之后施加。返回同一配置以便链式调用。
    /// </summary>
    public static ConnectionConfig Apply(ConnectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.ConfigureExternalServices ??= new ConfigureExternalServices();
        var previous = config.ConfigureExternalServices.EntityService;
        config.ConfigureExternalServices.EntityService = (property, column) =>
        {
            previous?.Invoke(property, column);
            Configure(property, column);
        };
        return config;
    }

    /// <summary>
    /// 对单个属性施加约定。
    /// </summary>
    public static void Configure(PropertyInfo property, EntityColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(column);
        // 没有公开 setter 的属性（领域事件缓冲等只读视图）与集合属性不是列
        if (property.SetMethod is not { IsPublic: true } || IsCollection(property.PropertyType))
        {
            column.IsIgnore = true;
            return;
        }

        // 标识由应用（IIdGenerator）生成，不交给数据库自增
        if (string.Equals(property.Name, nameof(IEntity<int>.Id), StringComparison.Ordinal))
        {
            column.IsPrimarykey = true;
            column.IsIdentity = false;
        }

        // 并发戳由 PersistenceConventions.NewStamp 生成，是 32 位十六进制，长度与之对齐
        if (string.Equals(property.Name, nameof(IHasConcurrencyStamp.ConcurrencyStamp), StringComparison.Ordinal) && property.PropertyType == typeof(string))
        {
            column.Length = 32;
        }

        // DateTimeOffset 经转换器按 UTC 时刻往返；SqlSugar 默认按本地时间处理会改变时刻
        var underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (underlying == typeof(DateTimeOffset))
        {
            column.SqlParameterDbType = typeof(DateTimeOffsetConverter);
        }

        // Nullable<T> 值类型与可空引用类型（string?）的列可空：SqlSugar 的 CodeFirst 不从类型推断可空，需要显式告诉它
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null
            || (!property.PropertyType.IsValueType && _nullability.Create(property).WriteState == NullabilityState.Nullable))
        {
            column.IsNullable = true;
        }
    }

    /// <summary>
    /// 是否集合类型：除字符串与字节数组之外的可枚举类型。
    /// </summary>
    private static bool IsCollection(Type type)
        => type != typeof(string) && type != typeof(byte[]) && typeof(IEnumerable).IsAssignableFrom(type);
}
