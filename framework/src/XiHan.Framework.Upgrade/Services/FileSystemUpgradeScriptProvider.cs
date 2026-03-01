#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileSystemUpgradeScriptProvider
// Guid:6f9a9b7f-2a7a-4c5c-b5ce-2b2d1b5b4c6b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:27:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Options;
using XiHan.Framework.Upgrade.Utils;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 文件系统升级脚本提供者
/// </summary>
public class FileSystemUpgradeScriptProvider : IUpgradeScriptProvider
{
    private readonly XiHanUpgradeOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options"></param>
    public FileSystemUpgradeScriptProvider(IOptions<XiHanUpgradeOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// 获取升级脚本列表
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IReadOnlyList<UpgradeScript>> GetScriptsAsync(CancellationToken cancellationToken = default)
    {
        var rootPath = ResolveRootPath();
        if (!Directory.Exists(rootPath))
        {
            return Task.FromResult<IReadOnlyList<UpgradeScript>>([]);
        }

        var scripts = new List<UpgradeScript>();
        foreach (var versionDir in Directory.GetDirectories(rootPath))
        {
            var versionText = Path.GetFileName(versionDir);
            if (!SemanticVersion.TryParse(versionText, out _))
            {
                continue;
            }

            var sqlFiles = Directory.GetFiles(versionDir, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var sqlFile in sqlFiles)
            {
                var scriptName = Path.GetFileName(sqlFile);
                scripts.Add(new UpgradeScript(versionText, scriptName, sqlFile));
            }
        }

        var ordered = scripts
            .OrderBy(s => s.Version, new SemanticVersionComparer())
            .ThenBy(s => s.ScriptName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<UpgradeScript>>(ordered);
    }

    /// <summary>
    /// 解析根路径
    /// </summary>
    /// <returns></returns>
    private string ResolveRootPath()
    {
        if (Path.IsPathRooted(_options.MigrationsRootPath))
        {
            return _options.MigrationsRootPath;
        }

        return Path.Combine(AppContext.BaseDirectory, _options.MigrationsRootPath);
    }

    /// <summary>
    /// 语义版本比较器
    /// </summary>
    private sealed class SemanticVersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            return SemanticVersion.Compare(x, y);
        }
    }
}
