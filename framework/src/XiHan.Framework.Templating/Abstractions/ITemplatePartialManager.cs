#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplatePartialManager
// Guid:8f3h7h2d-6g9i-7e2f-3h8h-7d2g9i6f3h8h
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Abstractions;

/// <summary>
/// 模板片段管理器接口
/// </summary>
public interface ITemplatePartialManager
{
    /// <summary>
    /// 注册模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="template">片段模板</param>
    void RegisterPartial(string name, string template);

    /// <summary>
    /// 获取模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    string? GetPartial(string name);

    /// <summary>
    /// 异步获取模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    Task<string?> GetPartialAsync(string name);

    /// <summary>
    /// 移除模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>是否成功移除</returns>
    bool RemovePartial(string name);

    /// <summary>
    /// 获取所有片段名称
    /// </summary>
    /// <returns>片段名称集合</returns>
    IEnumerable<string> GetPartialNames();

    /// <summary>
    /// 渲染模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    Task<string> RenderPartialAsync(string name, ITemplateContext context);

    /// <summary>
    /// 预编译所有片段
    /// </summary>
    /// <returns>预编译任务</returns>
    Task PrecompileAllPartialsAsync();

    /// <summary>
    /// 清空片段缓存
    /// </summary>
    void ClearPartialCache();
}

/// <summary>
/// 模板片段注册表
/// </summary>
public interface IPartialTemplateRegistry
{
    /// <summary>
    /// 注册片段提供者
    /// </summary>
    /// <param name="provider">片段提供者</param>
    void RegisterProvider(IPartialTemplateProvider provider);

    /// <summary>
    /// 移除片段提供者
    /// </summary>
    /// <param name="provider">片段提供者</param>
    void RemoveProvider(IPartialTemplateProvider provider);

    /// <summary>
    /// 解析片段模板
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    Task<string?> ResolvePartialAsync(string name);

    /// <summary>
    /// 批量解析片段模板
    /// </summary>
    /// <param name="names">片段名称集合</param>
    /// <returns>片段模板字典</returns>
    Task<IDictionary<string, string>> ResolvePartialsAsync(IEnumerable<string> names);

    /// <summary>
    /// 监听片段变化
    /// </summary>
    /// <param name="callback">变化回调</param>
    /// <returns>监听器</returns>
    IDisposable WatchPartialChanges(Action<PartialTemplateChangeEvent> callback);
}

/// <summary>
/// 模板片段提供者
/// </summary>
public interface IPartialTemplateProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 优先级
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 是否支持片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>是否支持</returns>
    bool SupportsPartial(string name);

    /// <summary>
    /// 获取片段模板
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    Task<string?> GetPartialTemplateAsync(string name);

    /// <summary>
    /// 获取片段信息
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段信息</returns>
    Task<PartialTemplateInfo?> GetPartialInfoAsync(string name);

    /// <summary>
    /// 监听片段变化
    /// </summary>
    /// <param name="callback">变化回调</param>
    /// <returns>监听器</returns>
    IDisposable? WatchChanges(Action<PartialTemplateChangeEvent> callback);
}

/// <summary>
/// 文件系统片段提供者
/// </summary>
public interface IFileSystemPartialProvider : IPartialTemplateProvider
{
    /// <summary>
    /// 根目录
    /// </summary>
    string RootDirectory { get; }

    /// <summary>
    /// 文件扩展名
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// 是否启用监听
    /// </summary>
    bool EnableWatching { get; }

    /// <summary>
    /// 添加搜索路径
    /// </summary>
    /// <param name="path">搜索路径</param>
    void AddSearchPath(string path);

    /// <summary>
    /// 移除搜索路径
    /// </summary>
    /// <param name="path">搜索路径</param>
    void RemoveSearchPath(string path);
}

/// <summary>
/// 内存片段提供者
/// </summary>
public interface IMemoryPartialProvider : IPartialTemplateProvider
{
    /// <summary>
    /// 添加片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="template">片段模板</param>
    void AddPartial(string name, string template);

    /// <summary>
    /// 更新片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="template">片段模板</param>
    void UpdatePartial(string name, string template);

    /// <summary>
    /// 移除片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>是否成功移除</returns>
    bool RemovePartial(string name);

    /// <summary>
    /// 清空片段
    /// </summary>
    void ClearPartials();
}

/// <summary>
/// 模板片段信息
/// </summary>
public record PartialTemplateInfo
{
    /// <summary>
    /// 片段名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 片段路径
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime LastModified { get; init; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// 内容哈希
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// 依赖的片段
    /// </summary>
    public ICollection<string> Dependencies { get; init; } = [];

    /// <summary>
    /// 元数据
    /// </summary>
    public IDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
}

/// <summary>
/// 模板片段变化事件
/// </summary>
public record PartialTemplateChangeEvent
{
    /// <summary>
    /// 变化类型
    /// </summary>
    public PartialChangeType ChangeType { get; init; }

    /// <summary>
    /// 片段名称
    /// </summary>
    public string PartialName { get; init; } = string.Empty;

    /// <summary>
    /// 片段路径
    /// </summary>
    public string? PartialPath { get; init; }

    /// <summary>
    /// 变化时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;
}

/// <summary>
/// 片段变化类型
/// </summary>
public enum PartialChangeType
{
    /// <summary>
    /// 创建
    /// </summary>
    Created,

    /// <summary>
    /// 修改
    /// </summary>
    Modified,

    /// <summary>
    /// 删除
    /// </summary>
    Deleted,

    /// <summary>
    /// 重命名
    /// </summary>
    Renamed
}
