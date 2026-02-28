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
using System.Reflection;
using System.Text.RegularExpressions;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Seeders;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 数据库初始化器
/// </summary>
public class DbInitializer : IDbInitializer, IScopedDependency
{
    private readonly ISqlSugarClientProvider _clientProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DbInitializer> _logger;
    private readonly XiHanSqlSugarCoreOptions _options;
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DbInitializer(
        ISqlSugarClientProvider clientProvider,
        IServiceProvider serviceProvider,
        ILogger<DbInitializer> logger,
        IOptions<XiHanSqlSugarCoreOptions> options,
        ICurrentTenant currentTenant)
    {
        _clientProvider = clientProvider;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 初始化数据库（完整流程）
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("开始数据库初始化");

            if (!_options.EnableDbInitialization)
            {
                _logger.LogInformation("数据库初始化已禁用（EnableDbInitialization = false），跳过初始化");
                return;
            }

            var tenantIds = GetTenantIdsFromConnectionConfigs();
            if (tenantIds.Count == 0)
            {
                _logger.LogInformation("未解析到租户连接配置，使用默认租户上下文初始化");
                await InitializeForTenantAsync(null);
            }
            else
            {
                foreach (var tenantId in tenantIds)
                {
                    await InitializeForTenantAsync(tenantId);
                }
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
            var db = _clientProvider.GetClient();
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
            var db = _clientProvider.GetClient();
            var entityTypes = GetEntityTypes();

            if (entityTypes.Count == 0)
            {
                _logger.LogWarning("没有找到需要创建的实体类型");
                return;
            }

            _logger.LogInformation("开始创建表结构，共 {Count} 个实体", entityTypes.Count);

            // 使用 CodeFirst 模式逐表创建，便于定位失败点
            var successCount = 0;
            var skippedExistsCount = 0;
            var splitTableInitCount = 0;
            foreach (var entityType in entityTypes)
            {
                var entityName = entityType.FullName ?? entityType.Name;
                _logger.LogInformation("开始创建表：{Entity}", entityName);

                try
                {
                    var isSplitTable = entityType.GetCustomAttribute<SplitTableAttribute>() != null;

                    var tableName = entityType.GetCustomAttribute<SugarTable>()?.TableName;
                    if (string.IsNullOrWhiteSpace(tableName))
                    {
                        tableName = entityType.Name;
                    }

                    var tableExists = isSplitTable
                        ? await Task.Run(() => IsAnySplitTableExists(db, tableName))
                        : await Task.Run(() => db.DbMaintenance.IsAnyTable(tableName, false));
                    if (tableExists)
                    {
                        skippedExistsCount++;
                        if (isSplitTable)
                        {
                            _logger.LogInformation("分表已存在，跳过初始化：{Entity}（表名：{TableName}）", entityName, tableName);
                        }
                        else
                        {
                            _logger.LogInformation("表已存在，跳过创建：{Entity}（表名：{TableName}）", entityName, tableName);
                        }
                        continue;
                    }

                    await Task.Run(() => db.CodeFirst.InitTables(entityType));
                    successCount++;
                    if (isSplitTable)
                    {
                        splitTableInitCount++;
                        _logger.LogInformation("分表初始化成功：{Entity}（表名：{TableName}）", entityName, tableName);
                    }
                    else
                    {
                        _logger.LogInformation("创建表成功：{Entity}", entityName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "创建表失败：{Entity}，错误：{Error}", entityName, ex.Message);
                    throw;
                }
            }

            _logger.LogInformation("表结构创建完成，成功创建 {SuccessCount} 个表（含分表初始化 {SplitTableInitCount} 个），跳过 {SkippedExistsCount} 个已存在表", successCount, splitTableInitCount, skippedExistsCount);
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
    /// 从配置Id中解析租户Id（支持 "1" 或 "Tenant_1" 形式）
    /// </summary>
    private static bool TryParseTenantId(string? configId, out long tenantId)
    {
        tenantId = default;
        if (string.IsNullOrWhiteSpace(configId))
        {
            return false;
        }

        var trimmed = configId.Trim();
        if (long.TryParse(trimmed, out tenantId))
        {
            return true;
        }

        const string prefix = "Tenant_";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var idPart = trimmed.Substring(prefix.Length);
            return long.TryParse(idPart, out tenantId);
        }

        return false;
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

    /// <summary>
    /// 判断分表是否已存在（支持 {year}{month}{day} 等占位符模板）
    /// </summary>
    private static bool IsAnySplitTableExists(ISqlSugarClient db, string tableNameTemplate)
    {
        // 无占位符时按普通表判断
        if (!tableNameTemplate.Contains('{') || !tableNameTemplate.Contains('}'))
        {
            return db.DbMaintenance.IsAnyTable(tableNameTemplate, false);
        }

        // 先去掉占位符，再按前缀匹配任意实际分表名
        var normalizedTemplate = Regex.Replace(tableNameTemplate, "\\{[^}]+\\}", string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedTemplate))
        {
            return false;
        }

        var allTables = db.DbMaintenance.GetTableInfoList(false);
        return allTables.Any(t => !string.IsNullOrWhiteSpace(t.Name) && t.Name.StartsWith(normalizedTemplate, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 从连接配置解析租户Id
    /// </summary>
    /// <returns>解析到的租户Id集合</returns>
    private IReadOnlyList<long> GetTenantIdsFromConnectionConfigs()
    {
        var tenantIds = new HashSet<long>();
        foreach (var connConfig in _options.ConnectionConfigs)
        {
            if (TryParseTenantId(connConfig.ConfigId, out var tenantId))
            {
                tenantIds.Add(tenantId);
            }
        }

        return tenantIds.OrderBy(id => id).ToList();
    }

    /// <summary>
    /// 在指定租户上下文中执行初始化
    /// </summary>
    /// <param name="tenantId">租户标识，null 表示默认上下文</param>
    private async Task InitializeForTenantAsync(long? tenantId)
    {
        using var tenantScope = _currentTenant.Change(tenantId, tenantId?.ToString());
        var tenantLabel = tenantId.HasValue ? tenantId.Value.ToString() : "无租户";

        _logger.LogInformation("开始初始化租户数据库: {Tenant}", tenantLabel);

        await CreateDatabaseAsync();

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

        _logger.LogInformation("租户数据库初始化完成: {Tenant}", tenantLabel);
    }
}
