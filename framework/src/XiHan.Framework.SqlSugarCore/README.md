# XiHan.Framework.SqlSugarCore

曦寒框架 SqlSugarCore 库，提供对 SqlSugar ORM 的集成支持。

## 简介

SqlSugar 是一款国产 ORM 框架，具有轻量级、高性能、灵活的特点。本模块提供了 SqlSugar 与曦寒框架的集成，特别是与工作单元（UnitOfWork）模式的集成，使得 SqlSugar 可以方便地在曦寒框架中使用。

## 主要功能

- 与曦寒框架工作单元（UnitOfWork）模式集成
- 提供基础仓储模式实现
- 支持事务管理
- 提供实体基类
- 支持多数据库配置
- 全局过滤器支持
- SQL 执行日志

## 使用方法

### 安装

在你的项目中添加对 `XiHan.Framework.SqlSugarCore` 的引用。

### 配置

```csharp
// 在模块的 ConfigureServices 方法中配置 SqlSugar
public override void ConfigureServices(ServiceConfigurationContext context)
{
    Configure<XiHanSqlSugarCoreOptions>(options =>
    {
        // 添加连接配置
        options.ConnectionConfigs.Add(new SqlSugarConnectionConfig
        {
            ConfigId = "Default",
            ConnectionString = "Server=localhost;Database=TestDb;User ID=sa;Password=123456;",
            DbType = DbType.SqlServer,
            IsAutoCloseConnection = true
        });

        // 启用SQL日志
        options.EnableSqlLog = true;
        options.SqlLogAction = (sql, parameters) =>
        {
            // 记录SQL
            Console.WriteLine(sql);
        };

        // 配置全局过滤器
        options.GlobalFilters.Add(typeof(ISoftDelete), entity => !((ISoftDelete)entity).IsDeleted);

        // 其他配置
        options.ConfigureDbAction = db =>
        {
            // 配置SqlSugar客户端
            db.Aop.OnError = ex =>
            {
                Console.WriteLine(ex.Message);
            };
        };
    });
}
```

### 定义实体

```csharp
[SugarTable("Users")]
public class User : SugarEntityWithAudit<Guid>
{
    [SugarColumn(ColumnDescription = "用户名")]
    public string UserName { get; set; }

    [SugarColumn(ColumnDescription = "邮箱")]
    public string Email { get; set; }

    [SugarColumn(ColumnDescription = "手机号", IsNullable = true)]
    public string PhoneNumber { get; set; }
}
```

### 定义仓储

```csharp
public interface IUserRepository : ISqlSugarRepository<User>
{
    Task<List<User>> GetActiveUsersAsync();
}

public class UserRepository : SqlSugarRepository<User>, IUserRepository
{
    public UserRepository(IUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider)
        : base(unitOfWorkManager, serviceProvider)
    {
    }

    public Task<List<User>> GetActiveUsersAsync()
    {
        return GetListAsync(u => u.IsDeleted == false);
    }
}
```

### 使用仓储

```csharp
[Dependency]
public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public UserService(IUserRepository userRepository, IUnitOfWorkManager unitOfWorkManager)
    {
        _userRepository = userRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    [UnitOfWork]
    public async Task CreateUserAsync(string userName, string email)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email
        };

        await _userRepository.InsertAsync(user);
    }

    public async Task<List<User>> GetUsersAsync()
    {
        using var uow = _unitOfWorkManager.Begin();
        var users = await _userRepository.GetListAsync();
        await uow.CompleteAsync();
        return users;
    }
}
```

### 直接使用 SqlSugar 客户端

```csharp
[Dependency]
public class SomeService
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IServiceProvider _serviceProvider;

    public SomeService(IUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _serviceProvider = serviceProvider;
    }

    [UnitOfWork]
    public async Task SomeMethodAsync()
    {
        // 获取当前工作单元
        var uow = _unitOfWorkManager.Current;

        // 获取SqlSugar客户端
        var client = uow.GetSqlSugarClient(_serviceProvider);

        // 使用客户端
        var count = await client.Queryable<User>()
            .Where(u => u.UserName.Contains("admin"))
            .CountAsync();

        // 也可以获取特定实体的查询器
        var users = await uow.GetSqlSugarQueryable<User>(_serviceProvider)
            .Where(u => u.Email.Contains("@example.com"))
            .ToListAsync();
    }
}
```

## 高级功能

### 多数据库支持

```csharp
Configure<XiHanSqlSugarCoreOptions>(options =>
{
    // 主数据库
    options.ConnectionConfigs.Add(new SqlSugarConnectionConfig
    {
        ConfigId = "Default",
        ConnectionString = "连接字符串1",
        DbType = DbType.SqlServer
    });

    // 第二个数据库
    options.ConnectionConfigs.Add(new SqlSugarConnectionConfig
    {
        ConfigId = "SecondDb",
        ConnectionString = "连接字符串2",
        DbType = DbType.MySql
    });
});

// 使用指定的数据库
var client = _unitOfWorkManager.Current.GetSqlSugarClient(_serviceProvider);
var db = client.GetConnection("SecondDb");
var data = await db.Queryable<SomeEntity>().ToListAsync();
```
