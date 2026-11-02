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
/// 按实体协议配置 SqlSugar 的实体映射。
/// </summary>
/// <remarks>
/// 实体不需要继承 SqlSugar 基类或标注 <c>[SugarColumn]</c>。<c>Id</c> 是主键（是否自增沿用实体自己的声明），<see cref="IHasConcurrencyStamp"/> 的并发戳长度 32，字符串列的长度按 <see cref="StringLengthAttribute"/> / <see cref="MaxLengthAttribute"/>，<see cref="LongTextAttribute"/>（或无参的 <c>[MaxLength]</c>）为长文本类型，<see cref="DateTimeOffset"/> 按 UTC 时刻往返，可空值类型与可空引用类型的列可空，没有公开 setter 的属性与集合属性不映射到列，实体上的 <see cref="EntityIndexAttribute"/> 在 <c>InitTables</c> 时建成索引（见 <see cref="ConfigureIndexes"/>，软删除实体的部分唯一索引由 <see cref="InitTables"/> 补建）；经 <see cref="Apply"/> 施加时，PostgreSQL 上非可空的数值 / 枚举 / 布尔 / Guid 列另带 CLR 缺省值，<c>InitTables</c> 才能给已有行的表加这类列（见 <see cref="ConfigureDefault"/>），字符串的 <c>Contains</c> / <c>StartsWith</c> / <c>EndsWith</c> 按字面匹配，PostgreSQL 的连接固定 <c>standard_conforming_strings = on</c>（见 <see cref="Apply"/>）。由本特性创建客户端时自动应用；应用自己注册 <c>ISqlSugarClient</c> 时，须在创建客户端前对 <c>ConnectionConfig</c> 调用 <see cref="Apply"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class SqlSugarConventions
{
    /// <summary>
    /// 判定引用类型属性可空性的上下文。
    /// </summary>
    /// <remarks>
    /// 不是线程安全的（内部缓存不加锁），只经 <see cref="IsNullableReference"/> 加锁使用。
    /// </remarks>
    private static readonly NullabilityInfoContext _nullability = new();

    /// <summary>
    /// 在连接配置上施加协议约定。
    /// </summary>
    /// <remarks>
    /// 登记实体服务、表服务回调与字符串匹配的翻译，并固定 PostgreSQL 会话的字面量规则。配置里已有的回调先执行，本约定之后施加：列约定挂在 <c>EntityService</c>，索引挂在 <c>EntityNameService</c>。<c>string.Contains</c> / <c>StartsWith</c> / <c>EndsWith</c> 以同名的外部函数（<c>SqlFuncServices</c>）接管翻译，在 SQLite、PostgreSQL、MySQL、SQL Server、Oracle 上译成按字面匹配的 <c>LIKE … ESCAPE '!'</c>——实参里的 <c>%</c>、<c>_</c>（SQL Server 还有 <c>[</c>）与反斜杠不再是通配符或转义符；经仓储的规约与直接用这个客户端的查询都一样；需要通配的查询用 <c>SqlFunc.Like</c>。列表里已有同名的外部函数时以它为准（应用自己的 <c>SqlFuncServices</c> 在调用本方法之前设好；之后整体替换列表会丢掉这三个翻译）。数据库类型是 PostgreSQL 时，连接串（含读写分离的从库连接串）的启动参数 <c>Options</c> 追加 <c>-c standard_conforming_strings=on</c>：SqlSugar 在多行写入、批量更新、按主键删除、<c>In</c> 与集合 <c>Contains</c> 里内联的字符串字面量只在它为 on 时正确，库或角色把它设成 off 时含反斜杠的值会让语句出错或被静默改掉；数据库类型与连接串要在调用本方法之前设好，之后再改的连接串不带这个参数。返回同一配置以便链式调用。
    /// </remarks>
    /// <exception cref="ArgumentException">PostgreSQL 连接串的启动参数把 <c>standard_conforming_strings</c> 设成了 on 以外的值。</exception>
    public static ConnectionConfig Apply(ConnectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.DbType == DbType.PostgreSQL)
        {
            // SqlSugar 内联的字符串字面量按 standard_conforming_strings = on 转义；用启动参数固定它，库与角色上的 off 不再起作用（ADR-0100）
            if (!string.IsNullOrWhiteSpace(config.ConnectionString))
            {
                config.ConnectionString = PostgreSqlStartupOptions.Enforce(config.ConnectionString);
            }

            foreach (var replica in config.SlaveConnectionConfigs ?? [])
            {
                if (!string.IsNullOrWhiteSpace(replica.ConnectionString))
                {
                    replica.ConnectionString = PostgreSqlStartupOptions.Enforce(replica.ConnectionString);
                }
            }
        }

        config.ConfigureExternalServices ??= new ConfigureExternalServices();
        // SqlSugar 把这三个方法译成不转义的 LIKE，实参里的通配符会误匹配；同名外部函数先于内置翻译被查到（ADR-0098）
        LiteralStringMatching.Register(config.ConfigureExternalServices);
        var previous = config.ConfigureExternalServices.EntityService;
        config.ConfigureExternalServices.EntityService = (property, column) =>
        {
            previous?.Invoke(property, column);
            Configure(property, column);
            // 数据库类型在回调执行时读取：应用可能在 Apply 之后才设置它
            ConfigureDefault(property, column, config.DbType);
        };
        // 表级回调先让已有的回调定下表名，索引名再按最终的表名推出
        var previousEntity = config.ConfigureExternalServices.EntityNameService;
        config.ConfigureExternalServices.EntityNameService = (type, entity) =>
        {
            previousEntity?.Invoke(type, entity);
            ConfigureIndexes(type, entity);
        };
        return config;
    }

    /// <summary>
    /// 把实体上的 <see cref="EntityIndexAttribute"/> 登记为 SqlSugar 的索引。
    /// </summary>
    /// <remarks>
    /// 名字与列由 <see cref="EntityIndexes.Resolve"/> 按表名推出（表名取此刻的映射：<c>[SugarTable]</c> 或此前回调改过的名字），转成 <see cref="SugarIndexAttribute"/> 加进实体信息，<c>CodeFirst.InitTables</c> 建表后按名字查不到才建（已有的索引不改、不删）。实体自己的 <c>[SugarIndex]</c> 与之同名时以实体的为准。软删除实体的唯一索引只约束未删除的行（<see cref="EntityIndex.ExcludesDeleted"/>），SqlSugar 的索引写不出条件，不登记在这里，由 <see cref="InitTables"/> 在建表之后补建。分表实体（<c>[SplitTable]</c>）不支持：SqlSugar 按分表改写索引名时会就地改动缓存里的特性，名字随建表次数变化，分表实体直接用 <c>[SugarIndex]</c> 与 <c>{split_table}</c> 占位符。
    /// </remarks>
    /// <exception cref="InvalidOperationException">索引声明不合法（见 <see cref="EntityIndexes"/>）。</exception>
    /// <exception cref="NotSupportedException">分表实体声明了索引。</exception>
    public static void ConfigureIndexes(Type entityType, EntityInfo entity)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(entity);
        // SqlSugar 对承载字典更新的 BCL 类型也调用表服务，它们没有索引声明
        if (IsBclType(entityType))
        {
            return;
        }

        var indexes = EntityIndexes.Resolve(entityType, entity.DbTableName);
        if (indexes.Count == 0)
        {
            return;
        }

        if (entityType.IsDefined(typeof(SplitTableAttribute), inherit: false))
        {
            throw new NotSupportedException($"Entity '{entityType.FullName}' is a split table; declare its indexes with SqlSugar's [SugarIndex] and the '{{split_table}}' placeholder instead of [EntityIndex].");
        }

        entity.Indexs ??= [];
        foreach (var index in indexes)
        {
            // 部分唯一索引交给 SqlSugar 会被建成没有条件的普通唯一索引（已删除的行也受约束），这里不登记，建表之后另行建出
            if (index.ExcludesDeleted)
            {
                continue;
            }

            if (entity.Indexs.Exists(existing => string.Equals(existing.IndexName, index.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // 列按属性名给出，SqlSugar 建索引时换成列名；全部升序（数据库可以反向扫描索引，单列降序不必单建）
            entity.Indexs.Add(new SugarIndexAttribute(index.Name, [.. index.Properties], [.. index.Properties.Select(static _ => OrderByType.Asc)], index.IsUnique));
        }
    }

    /// <summary>
    /// 按约定为实体建表，并补建软删除实体的部分唯一索引。
    /// </summary>
    /// <remarks>
    /// 普通实体与分表实体各走 SqlSugar 的建表入口，之后补建部分唯一索引。在客户端当前的连接上建；特性开着 <c>InitTables</c> 时按实体的落点逐个连接调用它，应用自己建表（租户独立库、迁移工具）也要经它而不是直接调 <c>CodeFirst.InitTables</c>，否则软删除实体的唯一索引建不出来。部分唯一索引只约束未删除的行（<c>WHERE IsDeleted = false</c>），PostgreSQL、SQLite、SQL Server 支持；连接的数据库类型不支持而实体声明了它时，在建任何表之前抛出，不建出缺了唯一约束的表。已有同名的索引不改、不删（从此前的预览升级、库里已有带 <c>IsDeleted</c> 列的同名唯一索引的，要手动删掉旧索引再建）。
    /// </remarks>
    /// <param name="client">要建表的连接。</param>
    /// <param name="entityTypes">实体类型；带 <c>[SplitTable]</c> 的走分表的建表入口。</param>
    /// <exception cref="FrameworkException">有实体声明了部分唯一索引而连接的数据库类型不支持（<see cref="PersistenceErrors.NotSupported"/>）。</exception>
    public static void InitTables(ISqlSugarClient client, params Type[] entityTypes)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(entityTypes);
        // 先核对再建表：不支持部分索引的库上不建出缺了唯一约束的表
        var dbType = client.CurrentConnectionConfig.DbType;
        if (!PartialUniqueIndexes.IsSupported(dbType) && entityTypes.Where(PartialUniqueIndexes.Declares).ToList() is { Count: > 0 } declaring)
        {
            throw PartialUniqueIndexes.NotSupported(declaring.Select(type => (type, dbType)));
        }

        // 分表实体走分表的建表入口（按当前周期建第一张表，之后的分表由写入时按需建出），普通实体走常规建表；分表实体不支持 [EntityIndex]，没有部分唯一索引
        var split = Array.FindAll(entityTypes, static type => type.IsDefined(typeof(SplitTableAttribute), inherit: false));
        var plain = Array.FindAll(entityTypes, static type => !type.IsDefined(typeof(SplitTableAttribute), inherit: false));
        if (plain.Length > 0)
        {
            client.CodeFirst.InitTables(plain);
            foreach (var type in plain)
            {
                PartialUniqueIndexes.Create(client, type);
            }
        }

        if (split.Length > 0)
        {
            client.CodeFirst.SplitTables().InitTables(split);
        }
    }

    /// <summary>
    /// PostgreSQL 上给非可空的数值、枚举、布尔与 <see cref="Guid"/> 列配 CLR 缺省值。
    /// </summary>
    /// <remarks>
    /// <c>0</c>、<c>false</c>、全零 Guid。实体自己的 <c>[SugarColumn(DefaultValue)]</c>、列类型或转换器已声明时不动，主键与自增列不配。作用是让 <c>InitTables</c> 能给已有行的表加这类列：没有缺省值时 SqlSugar 直接 <c>ADD COLUMN … NOT NULL</c>，PostgreSQL 以 23502 拒绝；有缺省值时它先加可空列、用缺省值回填、再设 NOT NULL。只影响建表与加列，不影响写入（写入总带全部列的值）。代价是此后每次 <c>InitTables</c> SqlSugar 都会对这些列重发一次 <c>SET DEFAULT</c>，所以 <c>InitTables</c> 宜只在开发与测试开启。不配的情况：SQLite（SqlSugar 加列本来就是可空列、旧行读回 CLR 缺省值；给了缺省值反而会走它的重建列流程，布尔列读回失败）、其他数据库（未验证）、非可空字符串（SqlSugar 把空串缺省值当成没有）、日期时间类型（没有可移植的缺省值）。这些列加到已有行的表上时声明成可空，或重建开发库 / 走迁移脚本。
    /// </remarks>
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
            || (!property.PropertyType.IsValueType && IsNullableReference(property)))
        {
            column.IsNullable = true;
        }
    }

    /// <summary>
    /// 引用类型属性是否声明为可空（<c>string?</c> 等）。
    /// </summary>
    private static bool IsNullableReference(PropertyInfo property)
    {
        // NullabilityInfoContext 的缓存是不加锁的字典：多个客户端会并发地首次建实体映射（并行的请求、并行的测试），
        // 共用的上下文不加锁时字典被并发写坏，建映射抛出“已添加了具有相同键的项”，启动或第一次查询随之失败
        lock (_nullability)
        {
            return _nullability.Create(property).WriteState == NullabilityState.Nullable;
        }
    }

    /// <summary>
    /// 属性是否声明在 BCL 类型上（命名空间 <c>System</c> 或 <c>System.*</c>）。
    /// </summary>
    /// <remarks>
    /// SqlSugar 对它内部承载字典更新的 <c>Dictionary&lt;string, object&gt;</c> 也调用实体服务：约定只管实体，这类属性原样交还——否则 <c>Count</c>（没有公开 setter）被标成忽略列，字典更新里名为 <c>count</c> 的列被丢掉，更新没有列可写，SqlSugar 报"Update List&lt;T&gt; need Primary key"。
    /// </remarks>
    private static bool IsBclMember(PropertyInfo property)
        => property.DeclaringType is { } type && IsBclType(type);

    /// <summary>
    /// 类型是否在 BCL 命名空间（<c>System</c> 或 <c>System.*</c>）里。
    /// </summary>
    private static bool IsBclType(Type type)
        => type.Namespace is { } ns && (string.Equals(ns, "System", StringComparison.Ordinal) || ns.StartsWith("System.", StringComparison.Ordinal));

    /// <summary>
    /// 是否集合类型：除字符串与字节数组之外的可枚举类型。
    /// </summary>
    private static bool IsCollection(Type type)
        => type != typeof(string) && type != typeof(byte[]) && typeof(IEnumerable).IsAssignableFrom(type);
}
