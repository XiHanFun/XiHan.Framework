using XiHan.Framework.Localization.Resources;

namespace XiHan.Framework.Web.Test.Localization;

/// <summary>
/// 测试资源类
/// </summary>
public class TestResource : BaseLocalizationResource
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <remarks>
    /// 资源名称必须与文件夹名称匹配，不需要包含完整命名空间
    /// </remarks>
    public TestResource() : base(
        // 使用类型完全限定名
        resourceName: typeof(TestResource).FullName!,
        defaultCulture: "en",
        // 确保路径格式正确
        basePath: "Localization/TestResource")
    {
        System.Console.WriteLine($"初始化TestResource, 资源名称: {ResourceName}");
        System.Console.WriteLine($"初始化TestResource, 基础路径: {BasePath}");

        // 添加调试日志验证文件
        var enJsonPath = Path.Combine(Path.Combine(AppContext.BaseDirectory, BasePath), "TestResource.en.json");
        var zhJsonPath = Path.Combine(Path.Combine(AppContext.BaseDirectory, BasePath), "TestResource.zh-CN.json");

        System.Console.WriteLine($"英文资源文件: {enJsonPath}, 存在: {File.Exists(enJsonPath)}");
        if (File.Exists(enJsonPath))
        {
            try
            {
                var content = File.ReadAllText(enJsonPath);
                System.Console.WriteLine($"英文资源内容预览: {content[..Math.Min(100, content.Length)]}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"读取英文资源失败: {ex.Message}");
            }
        }

        System.Console.WriteLine($"中文资源文件: {zhJsonPath}, 存在: {File.Exists(zhJsonPath)}");
        if (File.Exists(zhJsonPath))
        {
            try
            {
                var content = File.ReadAllText(zhJsonPath);
                System.Console.WriteLine($"中文资源内容预览: {content[..Math.Min(100, content.Length)]}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"读取中文资源失败: {ex.Message}");
            }
        }
    }
}
