using XiHan.Framework.Localization.Abstractions;

namespace XiHan.Framework.Localization;

/// <summary>
/// 表示默认字符串本地化器实现。
/// </summary>
/// <remarks>
/// 当前阶段先提供最小占位实现，后续再接入资源文件、虚拟文件系统或数据库资源源。
/// </remarks>
public sealed class DefaultStringLocalizer : IStringLocalizer
{
    /// <inheritdoc />
    public string this[string name] => name;
}
