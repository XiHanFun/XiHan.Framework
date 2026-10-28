// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 按实体协议配置 SqlSugar 的实体映射，实体不需要继承 SqlSugar 基类或标注 <c>[SugarColumn]</c>：
/// <c>Id</c> 是主键（是否自增沿用实体自己的声明），<see cref="IHasConcurrencyStamp"/> 的并发戳长度 32，字符串列的长度按 <see cref="StringLengthAttribute"/> / <see cref="MaxLengthAttribute"/>，<see cref="LongTextAttribute"/>（或无参的 <c>[MaxLength]</c>）为长文本类型，
/// <see cref="DateTimeOffset"/> 按 UTC 时刻往返，可空值类型与可空引用类型的列可空，没有公开 setter 的属性与集合属性不映射到列；
/// 经 <see cref="Apply"/> 施加时，PostgreSQL 上非可空的数值 / 枚举 / 布尔 / Guid 列另带 CLR 缺省值，<c>InitTables</c> 才能给已有行的表加这类列（见 <see cref="ConfigureDefault"/>）。
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
            // 数据库类型在回调执行时读取：应用可能在 Apply 之后才设置它
            ConfigureDefault(property, column, config.DbType);
        };
        return config;
    }

    /// <summary>
    /// PostgreSQL 上给非可空的数值、枚举、布尔与 <see cref="Guid"/> 列配 CLR 缺省值（<c>0</c>、<c>false</c>、全零 Guid），实体自己的 <c>[SugarColumn(DefaultValue)]</c>、
    /// 列类型或转换器已声明时不动，主键与自增列不配。
    /// 作用是让 <c>InitTables</c> 能给已有行的表加这类列：没有缺省值时 SqlSugar 直接 <c>ADD COLUMN … NOT NULL</c>，PostgreSQL 以 23502 拒绝；有缺省值时它先加可空列、用缺省值回填、再设 NOT NULL。
    /// 只影响建表与加列，不影响写入（写入总带全部列的值）。代价是此后每次 <c>InitTables</c> SqlSugar 都会对这些列重发一次 <c>SET DEFAULT</c>，所以 <c>InitTables</c> 宜只在开发与测试开启。
    /// 不配的情况：SQLite（SqlSugar 加列本来就是可空列、旧行读回 CLR 缺省值；给了缺省值反而会走它的重建列流程，布尔列读回失败）、其他数据库（未验证）、
    /// 非可空字符串（SqlSugar 把空串缺省值当成没有）、日期时间类型（没有可移植的缺省值）。这些列加到已有行的表上时声明成可空，或重建开发库 / 走迁移脚本。
    /// </summary>
    internal static void ConfigureDefault(PropertyInfo property, EntityColumnInfo column, DbType dbType)
    {
        if (dbType != DbType.PostgreSQL
            || IsBclMember(property)
            || column.IsIgnore
            || column.IsNullable
            || column.IsPrimarykey
            || column.IsIdentity
            || !string.IsNullOrEmpty(column.DefaultValue)
            || !string.IsNullOrEmpty(column.DataType)
            || column.SqlParameterDbType is not null)
        {
            return;
        }

        var type = property.PropertyType.IsEnum ? Enum.GetUnderlyingType(property.PropertyType) : property.PropertyType;
        column.DefaultValue = Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => "false",
            TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "0",
            _ when type == typeof(Guid) => Guid.Empty.ToString(),
            _ => null,
        };
    }

    /// <summary>
    /// 对单个属性施加约定。
    /// </summary>
    public static void Configure(PropertyInfo property, EntityColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(column);
        if (IsBclMember(property))
        {
            return;
        }

        // 没有公开 setter 的属性（领域事件缓冲等只读视图）与集合属性不是列
        if (property.SetMethod is not { IsPublic: true } || IsCollection(property.PropertyType))
        {
            column.IsIgnore = true;
            return;
        }

        // Id 是主键；是否自增沿用实体自己的 [SugarColumn(IsIdentity)]（默认不自增，标识由 IIdGenerator 生成），迁移来的自增表不被改掉
        if (string.Equals(property.Name, nameof(IEntity<>.Id), StringComparison.Ordinal))
        {
            column.IsPrimarykey = true;
        }

        // 并发戳由 PersistenceConventions.NewStamp 生成，是 32 位十六进制，长度与之对齐
        if (string.Equals(property.Name, nameof(IHasConcurrencyStamp.ConcurrencyStamp), StringComparison.Ordinal) && property.PropertyType == typeof(string))
        {
            column.Length = 32;
        }

        // 字符串列的长度：[LongText] → 数据库的长文本类型（PostgreSQL text、SQL Server nvarchar(max) 等）；[StringLength(n)] / [MaxLength(n)] → 长度 n（无参 [MaxLength] 也按长文本，
        // 但它的构造函数带 RequiresUnreferencedCode，框架自己的记录用 [LongText]）；实体自己的 [SugarColumn] 已给长度或类型时不动。
        // 没有注解的字符串列沿用 SqlSugar 的默认（PostgreSQL 为 varchar(255)，超长写入整批失败），框架的数据库记录全部显式标注
        if (property.PropertyType == typeof(string) && column.Length == 0 && string.IsNullOrEmpty(column.DataType))
        {
            if (property.IsDefined(typeof(LongTextAttribute), inherit: true))
            {
                column.DataType = StaticConfig.CodeFirst_BigString;
            }
            else if (property.GetCustomAttribute<StringLengthAttribute>() is { MaximumLength: > 0 } stringLength)
            {
                column.Length = stringLength.MaximumLength;
            }
            else if (property.GetCustomAttribute<MaxLengthAttribute>() is { } maxLength)
            {
                if (maxLength.Length > 0)
                {
                    column.Length = maxLength.Length;
                }
                else
                {
                    column.DataType = StaticConfig.CodeFirst_BigString;
                }
            }
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
    /// 属性是否声明在 BCL 类型上（命名空间 <c>System</c> 或 <c>System.*</c>）。SqlSugar 对它内部承载字典更新的 <c>Dictionary&lt;string, object&gt;</c> 也调用实体服务：
    /// 约定只管实体，这类属性原样交还——否则 <c>Count</c>（没有公开 setter）被标成忽略列，字典更新里名为 <c>count</c> 的列被丢掉，更新没有列可写，SqlSugar 报"Update List&lt;T&gt; need Primary key"。
    /// </summary>
    private static bool IsBclMember(PropertyInfo property)
        => property.DeclaringType?.Namespace is { } ns && (string.Equals(ns, "System", StringComparison.Ordinal) || ns.StartsWith("System.", StringComparison.Ordinal));

    /// <summary>
    /// 是否集合类型：除字符串与字节数组之外的可枚举类型。
    /// </summary>
    private static bool IsCollection(Type type)
        => type != typeof(string) && type != typeof(byte[]) && typeof(IEnumerable).IsAssignableFrom(type);
}
