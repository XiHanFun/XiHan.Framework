#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UpgradeStatusService
// Guid:4e43df64-8e02-4b88-8f3f-ea8c8c5f8e7e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:27:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Options;
using XiHan.Framework.Upgrade.Utils;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 升级状态服务
/// </summary>
public class UpgradeStatusService : IUpgradeStatusService
{
    private readonly IUpgradeVersionStore _versionStore;
    private readonly IEnumerable<IUpgradeScriptProvider> _scriptProviders;
    private readonly XiHanUpgradeOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="versionStore">版本存储服务</param>
    /// <param name="scriptProviders">升级脚本提供者集合</param>
    /// <param name="options">升级选项</param>
    public UpgradeStatusService(
        IUpgradeVersionStore versionStore,
        IEnumerable<IUpgradeScriptProvider> scriptProviders,
        IOptions<XiHanUpgradeOptions> options)
    {
        _versionStore = versionStore;
        _scriptProviders = scriptProviders;
        _options = options.Value;
    }

    /// <summary>
    /// 确保初始化（在应用启动时调用，预先创建版本记录）
    /// </summary>
    /// <returns></returns>
    public async Task EnsureInitializedAsync()
    {
        if (!_options.EnableAutoCheckOnStartup)
        {
            return;
        }

        var currentAppVersion = ResolveCurrentAppVersion();
        await _versionStore.GetOrCreateAsync(currentAppVersion, _options.MinSupportVersion);
    }

    /// <summary>
    /// 获取升级版本快照
    /// </summary>
    /// <param name="clientVersion">客户端版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>升级版本快照</returns>
    public async Task<UpgradeVersionSnapshot> GetVersionSnapshotAsync(string? clientVersion = null, CancellationToken cancellationToken = default)
    {
        var currentAppVersion = ResolveCurrentAppVersion();
        var minSupportVersion = _options.MinSupportVersion;
        var version = await _versionStore.GetOrCreateAsync(currentAppVersion, minSupportVersion, cancellationToken);

        var scripts = await CollectScriptsAsync(cancellationToken);
        var latestScriptVersion = GetLatestScriptVersion(scripts);

        var needDbUpgrade = SemanticVersion.Compare(version.DbVersion, latestScriptVersion) < 0;
        var needAppUpgrade = SemanticVersion.Compare(version.AppVersion, currentAppVersion) < 0;
        var needUpgrade = needDbUpgrade || needAppUpgrade;

        var forceUpgrade = !string.IsNullOrWhiteSpace(clientVersion) &&
            SemanticVersion.Compare(clientVersion, minSupportVersion) < 0;

        var status = await GetUpgradeStatusInternalAsync(version, needUpgrade, cancellationToken);

        return new UpgradeVersionSnapshot
        {
            CurrentAppVersion = currentAppVersion,
            CurrentDbVersion = version.DbVersion,
            MinSupportVersion = minSupportVersion,
            RecordedAppVersion = version.AppVersion,
            NeedUpgrade = needUpgrade,
            ForceUpgrade = forceUpgrade,
            IsCompatible = !forceUpgrade,
            Status = status,
            IsUpgrading = version.IsUpgrading,
            UpgradeNode = version.UpgradeNode,
            UpgradeStartTime = version.UpgradeStartTime
        };
    }

    /// <summary>
    /// 获取升级状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>升级状态</returns>
    public async Task<UpgradeStatus> GetUpgradeStatusAsync(CancellationToken cancellationToken = default)
    {
        var currentAppVersion = ResolveCurrentAppVersion();
        var version = await _versionStore.GetOrCreateAsync(currentAppVersion, _options.MinSupportVersion, cancellationToken);
        var scripts = await CollectScriptsAsync(cancellationToken);
        var latestScriptVersion = GetLatestScriptVersion(scripts);

        var needDbUpgrade = SemanticVersion.Compare(version.DbVersion, latestScriptVersion) < 0;
        var needAppUpgrade = SemanticVersion.Compare(version.AppVersion, currentAppVersion) < 0;
        var needUpgrade = needDbUpgrade || needAppUpgrade;

        return await GetUpgradeStatusInternalAsync(version, needUpgrade, cancellationToken);
    }

    /// <summary>
    /// 获取最新脚本版本
    /// </summary>
    /// <param name="scripts">升级脚本集合</param>
    /// <returns>最新脚本版本</returns>
    private static string GetLatestScriptVersion(IReadOnlyList<UpgradeScript> scripts)
    {
        if (scripts.Count == 0)
        {
            return "0.0.0";
        }

        var maxVersion = scripts
            .Select(s => s.Version)
            .OrderByDescending(v => v, new SemanticVersionComparer())
            .First();

        return maxVersion;
    }

    /// <summary>
    /// 获取升级状态内部逻辑
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <param name="needUpgrade">是否需要升级</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>升级状态</returns>
    private async Task<UpgradeStatus> GetUpgradeStatusInternalAsync(
        UpgradeVersionState version,
        bool needUpgrade,
        CancellationToken cancellationToken)
    {
        if (version.IsUpgrading)
        {
            return UpgradeStatus.Upgrading;
        }

        var lastHistory = await _versionStore.GetLatestHistoryAsync(cancellationToken);
        if (lastHistory != null && !lastHistory.Success)
        {
            return UpgradeStatus.Failed;
        }

        if (!needUpgrade && (lastHistory?.Success == true || version.UpgradeStartTime.HasValue))
        {
            return UpgradeStatus.Completed;
        }

        return UpgradeStatus.Normal;
    }

    /// <summary>
    /// 解析当前应用版本
    /// </summary>
    /// <returns>当前应用版本</returns>
    private string ResolveCurrentAppVersion()
    {
        if (!string.IsNullOrWhiteSpace(_options.AppVersion))
        {
            return _options.AppVersion;
        }

        var version = ReflectionHelper.GetEntryAssemblyVersion();
        return version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>
    /// 收集所有升级脚本
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>升级脚本集合</returns>
    private async Task<IReadOnlyList<UpgradeScript>> CollectScriptsAsync(CancellationToken cancellationToken)
    {
        var scripts = new List<UpgradeScript>();
        foreach (var provider in _scriptProviders)
        {
            var providerScripts = await provider.GetScriptsAsync(cancellationToken);
            if (providerScripts.Count > 0)
            {
                scripts.AddRange(providerScripts);
            }
        }

        return scripts;
    }

    /// <summary>
    /// 语义化版本比较器
    /// </summary>
    private sealed class SemanticVersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            return SemanticVersion.Compare(x, y);
        }
    }
}
