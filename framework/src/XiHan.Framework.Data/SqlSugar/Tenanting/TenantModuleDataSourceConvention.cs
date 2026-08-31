// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Data.SqlSugar.Options;

namespace XiHan.Framework.Data.SqlSugar.Tenanting;

/// <summary>
/// 库隔离租户「自带整套模块库」的约定
/// </summary>
/// <remarks>
/// <para>
/// 按默认布局逐条镜像：默认布局给某模块单独分了库，该租户也分，库名由租户主库名派生
/// （见 <see cref="TenantModuleConnectionStringDeriver"/>）；默认布局那条连接串留空
/// （即该模块不分库），该租户同样不分，连接串留空即继承租户主库。
/// </para>
/// <para>
/// 于是「租户声明了库隔离」就意味着它的全部数据都在它自己的库里，不会有一部分悄悄落回公共模块库。
/// 租户连接提供器显式给出的模块库优先，约定只补它没提到的模块。
/// </para>
/// </remarks>
public static class TenantModuleDataSourceConvention
{
    /// <summary>
    /// 合并出该租户这套布局下的全部模块库配置
    /// </summary>
    /// <param name="descriptor">租户连接描述符</param>
    /// <param name="defaultLayoutModuleConfigs">默认布局（平台主连接）下声明的模块库</param>
    /// <param name="enabled">是否启用本约定；关闭则只返回描述符显式声明的部分</param>
    /// <returns>该租户这套布局下的全部模块库配置</returns>
    public static List<SqlSugarModuleDataSourceConfigOptions> Merge(
        SqlSugarTenantConnection descriptor,
        IEnumerable<SqlSugarModuleDataSourceConfigOptions>? defaultLayoutModuleConfigs,
        bool enabled)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var declared = descriptor.ModuleDataSourceConfigs?
            .Where(moduleConfig => !string.IsNullOrWhiteSpace(moduleConfig.ModuleDataSource))
            .ToList() ?? [];

        if (!enabled || defaultLayoutModuleConfigs is null)
        {
            return declared;
        }

        var covered = declared
            .Select(moduleConfig => moduleConfig.ModuleDataSource.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var merged = new List<SqlSugarModuleDataSourceConfigOptions>(declared);
        foreach (var platformModule in defaultLayoutModuleConfigs)
        {
            var moduleDataSource = platformModule.ModuleDataSource?.Trim();
            if (string.IsNullOrWhiteSpace(moduleDataSource) || !covered.Add(moduleDataSource))
            {
                continue;
            }

            merged.Add(new SqlSugarModuleDataSourceConfigOptions
            {
                ModuleDataSource = moduleDataSource,
                // 平台该模块本就不分库：租户跟着不分，留空即继承租户主库
                ConnectionString = string.IsNullOrWhiteSpace(platformModule.ConnectionString)
                    ? null
                    : TenantModuleConnectionStringDeriver.Derive(descriptor.ConnectionString, descriptor.DbType, moduleDataSource)
            });
        }

        return merged;
    }
}
