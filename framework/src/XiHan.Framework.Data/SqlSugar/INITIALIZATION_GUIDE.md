# SqlSugar 数据库初始化和种子数据功能

## 📝 概述

本模块提供了完整的数据库初始化、表结构创建和种子数据功能，基于 SqlSugar ORM 实现。

## ✨ 主要功能

### 1. 数据库初始化器 (`IDbInitializer`)

- ✅ 自动创建数据库（如果不存在）
- ✅ 使用 CodeFirst 模式创建表结构
- ✅ 执行种子数据
- ✅ 支持同步和异步操作
- ✅ 完善的日志记录

### 2. 种子数据系统 (`IDataSeeder`)

- ✅ 支持多个种子数据提供者
- ✅ 按优先级顺序执行
- ✅ 自动检查数据是否已存在
- ✅ 批量插入数据
- ✅ 详细的执行日志

## 🚀 快速开始

### 1. 配置数据库连接

```csharp
services.AddXiHanDataSqlSugar(options =>
{
    options.ConnectionConfigs.Add(new SqlSugarConnectionConfig
    {
        ConfigId = "Default",
        ConnectionString = "Server=.;Database=XiHanDB;Trusted_Connection=True;",
        DbType = DbType.SqlServer,
        IsAutoCloseConnection = true
    });

    // 注册实体类型（用于创建表结构）
    options.EntityTypes.AddRange(new[]
    {
        typeof(SysUser),
        typeof(SysRole),
        typeof(SysUserRole),
        // ... 其他实体类型
    });

    // 启用数据库初始化
    options.EnableDbInitialization = true;

    // 启用种子数据
    options.EnableDataSeeding = true;

    // 启用SQL日志（开发环境）
    options.EnableSqlLog = true;
    options.SqlLogAction = (sql, parameters) =>
    {
        Console.WriteLine($"SQL: {sql}");
    };
});
```

### 2. 注册种子数据提供者

```csharp
// 在模块中注册
services.AddDataSeeder<SysRoleSeeder>();
services.AddDataSeeder<SysUserSeeder>();
services.AddDataSeeder<SysUserRoleSeeder>();

// 或批量注册
services.AddDataSeeders(
    typeof(SysRoleSeeder),
    typeof(SysUserSeeder),
    typeof(SysUserRoleSeeder)
);
```

### 3. 在应用启动时初始化数据库

```csharp
// Program.cs 或 Startup.cs
public void Configure(IApplicationBuilder app)
{
    // 同步初始化
    app.UseDbInitializer(initialize: true);

    // 或异步初始化
    await app.UseDbInitializerAsync(initialize: true);

    // ... 其他中间件
}
```

## 📖 创建种子数据

### 1. 创建种子数据类

```csharp
public class SysRoleSeeder : DataSeederBase
{
    public SysRoleSeeder(ISqlSugarDbContext dbContext, ILogger<SysRoleSeeder> logger)
        : base(dbContext, logger)
    {
    }

    /// <summary>
    /// 种子数据优先级（数字越小优先级越高）
    /// </summary>
    public override int Order => 10;

    /// <summary>
    /// 种子数据名称
    /// </summary>
    public override string Name => "系统角色种子数据";

    /// <summary>
    /// 同步种子数据实现
    /// </summary>
    protected override void SeedInternal()
    {
        // 检查数据是否已存在
        if (HasData<SysRole>(r => true))
        {
            Logger.LogInformation("系统角色数据已存在，跳过种子数据");
            return;
        }

        var roles = new List<SysRole>
        {
            new()
            {
                RoleCode = "SuperAdmin",
                RoleName = "超级管理员",
                RoleDescription = "系统超级管理员，拥有所有权限",
                IsEnabled = true,
                CreatedTime = DateTime.Now
            }
        };

        // 批量插入
        BulkInsert(roles);
    }

    /// <summary>
    /// 异步种子数据实现
    /// </summary>
    protected override async Task SeedInternalAsync()
    {
        if (await HasDataAsync<SysRole>(r => true))
        {
            Logger.LogInformation("系统角色数据已存在，跳过种子数据");
            return;
        }

        var roles = new List<SysRole>
        {
            new()
            {
                RoleCode = "SuperAdmin",
                RoleName = "超级管理员",
                RoleDescription = "系统超级管理员，拥有所有权限",
                IsEnabled = true,
                CreatedTime = DateTime.Now
            }
        };

        await BulkInsertAsync(roles);
    }
}
```

### 2. DataSeederBase 提供的辅助方法

```csharp
// 检查数据是否存在
bool exists = HasData<SysUser>(u => u.UserName == "admin");
bool exists = await HasDataAsync<SysUser>(u => u.UserName == "admin");

// 批量插入数据
BulkInsert(userList);
await BulkInsertAsync(userList);

// 访问数据库上下文
var db = DbContext.GetClient();
var user = db.Queryable<SysUser>().First(u => u.UserName == "admin");

// 使用日志记录器
Logger.LogInformation("种子数据执行成功");
Logger.LogError(ex, "种子数据执行失败");
```

## 🎨 完整示例

### 示例 1：角色种子数据

```csharp
public class SysRoleSeeder : DataSeederBase
{
    public SysRoleSeeder(ISqlSugarDbContext dbContext, ILogger<SysRoleSeeder> logger)
        : base(dbContext, logger)
    {
    }

    public override int Order => 10;
    public override string Name => "系统角色种子数据";

    protected override async Task SeedInternalAsync()
    {
        if (await HasDataAsync<SysRole>(r => true))
        {
            return;
        }

        var roles = new List<SysRole>
        {
            new() { RoleCode = "SuperAdmin", RoleName = "超级管理员" },
            new() { RoleCode = "Admin", RoleName = "管理员" },
            new() { RoleCode = "User", RoleName = "普通用户" }
        };

        await BulkInsertAsync(roles);
    }

    protected override void SeedInternal()
    {
        // 同步版本实现...
    }
}
```

### 示例 2：用户种子数据（依赖角色）

```csharp
public class SysUserSeeder : DataSeederBase
{
    public SysUserSeeder(ISqlSugarDbContext dbContext, ILogger<SysUserSeeder> logger)
        : base(dbContext, logger)
    {
    }

    // 优先级低于角色种子数据，确保角色先创建
    public override int Order => 20;
    public override string Name => "系统用户种子数据";

    protected override async Task SeedInternalAsync()
    {
        if (await HasDataAsync<SysUser>(u => true))
        {
            return;
        }

        var users = new List<SysUser>
        {
            new()
            {
                UserName = "admin",
                Password = EncryptionHelper.MD5Encrypt("Admin@123"),
                RealName = "系统管理员"
            }
        };

        await BulkInsertAsync(users);
    }

    protected override void SeedInternal()
    {
        // 同步版本实现...
    }
}
```

### 示例 3：关系表种子数据

```csharp
public class SysUserRoleSeeder : DataSeederBase
{
    public SysUserRoleSeeder(ISqlSugarDbContext dbContext, ILogger<SysUserRoleSeeder> logger)
        : base(dbContext, logger)
    {
    }

    // 最低优先级，确保用户和角色都已创建
    public override int Order => 30;
    public override string Name => "系统用户角色关系种子数据";

    protected override async Task SeedInternalAsync()
    {
        if (await HasDataAsync<SysUserRole>(ur => true))
        {
            return;
        }

        // 查询已创建的用户和角色
        var admin = await DbContext.GetClient()
            .Queryable<SysUser>()
            .FirstAsync(u => u.UserName == "admin");

        var superAdminRole = await DbContext.GetClient()
            .Queryable<SysRole>()
            .FirstAsync(r => r.RoleCode == "SuperAdmin");

        if (admin == null || superAdminRole == null)
        {
            Logger.LogWarning("找不到相关用户或角色，跳过关系数据");
            return;
        }

        var userRoles = new List<SysUserRole>
        {
            new()
            {
                UserId = admin.BaseId,
                RoleId = superAdminRole.BaseId
            }
        };

        await BulkInsertAsync(userRoles);
    }

    protected override void SeedInternal()
    {
        // 同步版本实现...
    }
}
```

## ⚙️ 配置选项

### XiHanSqlSugarCoreOptions

```csharp
public class XiHanSqlSugarCoreOptions
{
    /// <summary>
    /// 连接配置集合
    /// </summary>
    public List<SqlSugarConnectionConfig> ConnectionConfigs { get; set; }

    /// <summary>
    /// 实体类型集合（用于创建表结构）
    /// </summary>
    public List<Type> EntityTypes { get; set; }

    /// <summary>
    /// 是否启用数据库初始化
    /// </summary>
    public bool EnableDbInitialization { get; set; }

    /// <summary>
    /// 是否启用种子数据
    /// </summary>
    public bool EnableDataSeeding { get; set; }

    /// <summary>
    /// 是否启用SQL日志
    /// </summary>
    public bool EnableSqlLog { get; set; }

    /// <summary>
    /// SQL日志动作
    /// </summary>
    public Action<string, SugarParameter[]>? SqlLogAction { get; set; }
}
```

## 🔧 高级用法

### 手动执行初始化

```csharp
// 注入初始化器
public class MyService
{
    private readonly IDbInitializer _dbInitializer;

    public MyService(IDbInitializer dbInitializer)
    {
        _dbInitializer = dbInitializer;
    }

    public async Task ManualInitializeAsync()
    {
        // 1. 只创建数据库
        await _dbInitializer.CreateDatabaseAsync();

        // 2. 只创建表结构
        await _dbInitializer.CreateTablesAsync();

        // 3. 只执行种子数据
        await _dbInitializer.SeedDataAsync();

        // 4. 完整初始化流程
        await _dbInitializer.InitializeAsync();
    }
}
```

### 条件性初始化

```csharp
// appsettings.json
{
  "Database": {
    "EnableAutoInitialize": true
  }
}

// Program.cs
if (configuration.GetValue<bool>("Database:EnableAutoInitialize"))
{
    app.UseDbInitializer();
}
```

## 📊 执行顺序

1. **创建数据库** - 如果数据库不存在则创建
2. **创建表结构** - 根据注册的实体类型创建表
3. **执行种子数据** - 按 `Order` 从小到大顺序执行

## ⚠️ 注意事项

1. **优先级设置**

   - 基础数据（如角色）优先级应设置较小的数字
   - 依赖其他表的数据优先级应设置较大的数字
   - 建议以 10 为间隔设置优先级

2. **数据检查**

   - 始终在种子数据开始时检查数据是否已存在
   - 避免重复插入数据

3. **事务处理**

   - 种子数据会自动在事务中执行
   - 如果出错会自动回滚

4. **生产环境**
   - 生产环境建议关闭自动初始化
   - 使用数据库迁移脚本管理表结构变更

## 📚 相关文档

- [SqlSugar 官方文档](https://www.donet5.com/Home/Doc)
- [曦寒框架数据访问文档](../../README.md)

---

**版本**: v1.0.0  
**最后更新**: 2025-01-05
