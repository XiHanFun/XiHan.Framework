namespace XiHan.Framework.Bootstrap.Abstractions.Application;

/// <summary>
/// 表示应用信息访问器契约。
/// </summary>
public interface IApplicationInfoAccessor
{
    /// <summary>
    /// 获取应用名称。
    /// </summary>
    string? ApplicationName { get; }

    /// <summary>
    /// 获取应用实例标识。
    /// </summary>
    string InstanceId { get; }
}
