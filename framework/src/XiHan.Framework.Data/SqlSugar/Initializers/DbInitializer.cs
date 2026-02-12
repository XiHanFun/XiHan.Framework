#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DbInitializer
// Guid:6c7d8e9f-0a1b-2c3d-4e5f-6a7b8c9d0e1f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/05 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Seeders;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 数据库初始化器
/// </summary>
public class DbInitializer : IDbInitializer, IScopedDependency
{
    private readonly ISqlSugarDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DbInitializer> _logger;
    private readonly XiHanSqlSugarCoreOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DbInitializer(
        ISqlSugarDbContext dbContext,
        IServiceProvider serviceProvider,
        ILogger<DbInitializer> logger,
        IOptions<XiHanSqlSugarCoreOptions> options)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 初始化数据库（完整流程）
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("开始数据库初始化");

            // 1. 创建数据库
            if (_options.EnableDbInitialization)
            {
                await CreateDatabaseAsync();
            }
            else
            {
                _logger.LogInformation("数据库初始化已禁用（EnableDbInitialization = false），跳过初始化");
                return;
            }

            // 2. 创建表结构
            if (_options.EnableTableInitialization)
            {
                await CreateTablesAsync();
            }
            else
            {
                _logger.LogInformation("表结构初始化已禁用（EnableTableInitialization = false），跳过初始化");
                return;
            }

            // 3. 执行种子数据
            if (_options.EnableDataSeeding)
            {
                await SeedDataAsync();
            }
            else
            {
                _logger.LogInformation("种子数据已禁用（EnableDataSeeding = false），跳过种子数据");
            }

            _logger.LogInformation("数据库初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库初始化失败: {Error}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 创建数据库（如果不存在）
    /// </summary>
    public async Task CreateDatabaseAsync()
    {
        try
        {
            var db = _dbContext.GetClient();
            await Task.Run(() =>
            {
                //if (!db.DbMaintenance.IsAnySystemTablePermissions())
                //{
                //    _logger.LogWarning("没有创建数据库的权限，跳过数据库创建");
                //    return;
                //}

                if (!db.DbMaintenance.CreateDatabase())
                {
                    _logger.LogInformation("数据库已存在，跳过创建");
                    return;
                }

                _logger.LogInformation("数据库创建成功");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建数据库失败: {Error}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 创建表结构
    /// </summary>
    public async Task CreateTablesAsync()
    {
        try
        {
            var db = _dbContext.GetClient();
            var entityTypes = GetEntityTypes();

            if (entityTypes.Count == 0)
            {
                _logger.LogWarning("没有找到需要创建的实体类型");
                return;
            }

            _logger.LogInformation("开始创建表结构，共 {Count} 个实体", entityTypes.Count);

            // 使用 CodeFirst 模式逐表创建，便于定位失败点
            var successCount = 0;
            foreach (var entityType in entityTypes)
            {
                var entityName = entityType.FullName ?? entityType.Name;
                _logger.LogInformation("开始创建表：{Entity}", entityName);

                try
                {
                    await Task.Run(() => db.CodeFirst.InitTables(entityType));
                    successCount++;
                    _logger.LogInformation("创建表成功：{Entity}", entityName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "创建表失败：{Entity}，错误：{Error}", entityName, ex.Message);
                    throw;
                }
            }

            _logger.LogInformation("表结构创建成功，共创建 {Count} 个表", successCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建表结构失败: {Error}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 执行种子数据
    /// </summary>
    public async Task SeedDataAsync()
    {
        try
        {
            var seeders = _serviceProvider.GetServices<IDataSeeder>()
                .OrderBy(s => s.Order)
                .ToList();

            if (seeders.Count == 0)
            {
                _logger.LogInformation("没有找到种子数据提供者");
                return;
            }

            _logger.LogInformation("开始执行种子数据，共 {Count} 个种子数据提供者", seeders.Count);

            foreach (var seeder in seeders)
            {
                await seeder.SeedAsync();
            }

            _logger.LogInformation("种子数据执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行种子数据失败: {Error}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 获取所有实体类型
    /// </summary>
    /// <returns></returns>
    private static List<Type> GetEntityTypes()
    {
        var dbEntities = ReflectionHelper.GetContainsAttributeSubClasses<IEntityBase, SugarTable>().ToList();
        // 从配置中获取实体类型
        if (dbEntities != null && dbEntities.Count > 0)
        {
            return dbEntities;
        }

        // 如果没有配置，返回空列表
        return [];
    }
}
