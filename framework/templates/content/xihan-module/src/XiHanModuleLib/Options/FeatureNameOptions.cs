namespace XiHanModuleLib.Options;

/// <summary>
/// FeatureName 选项
/// </summary>
public class FeatureNameOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "FeatureName";

    /// <summary>
    /// 问候语前缀
    /// </summary>
    public string GreetingPrefix { get; set; } = "你好";
}
