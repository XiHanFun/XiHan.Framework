#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Region
// Guid:79fd116f-ac47-4228-88c5-bf8e29f0cf5a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/7 16:06:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Text.RegularExpressions;
using XiHan.Framework.Utils.IO;

namespace XiHan.Framework.Integration.Tests;

/// <summary>
/// Region
/// </summary>
public class Region
{
    public static void Main()
    {
        Console.WriteLine("请输入要遍历的文件夹路径：");
        var rootPath = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            Console.WriteLine("❌ 文件夹路径无效！");
            return;
        }

        // 可自定义文件扩展名
        string[] extensions = [".cs", ".txt", ".cshtml", ".razor"];

        var files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine($"📂 共找到 {files.Count} 个文件。");

        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file, Encoding.UTF8);

                // 删除旧的版权注释块（含 #region 到 #endregion）
                var cleanedContent = RemoveOldHeader(content);

                // 添加新的版权头
                var header = GenerateHeader(PathHelper.GetPathComponents(file).FileNameWithoutExtension);
                var newContent = header + Environment.NewLine + cleanedContent.TrimStart();

                File.WriteAllText(file, newContent, Encoding.UTF8);

                Console.WriteLine($"✅ 已更新版权头：{file}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 文件处理失败：{file}\n{ex.Message}");
            }
        }

        Console.WriteLine("🎉 所有文件处理完成！");
    }

    /// <summary>
    /// 删除旧的版权注释区域
    /// </summary>
    private static string RemoveOldHeader(string content)
    {
        // 匹配 #region <<版权版本注释>> 到 #endregion <<版权版本注释>> 之间的任何内容
        var pattern = @"#region\s*<<版权版本注释>>[\s\S]*?#endregion\s*<<版权版本注释>>";
        return Regex.Replace(content, pattern, string.Empty, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// 生成新的版权头注释
    /// </summary>
    private static string GenerateHeader(string fileName)
    {
        var guid = Guid.NewGuid().ToString();
        var createTime = RandomTimeWithinTwoHours().ToString("yyyy/MM/dd HH:mm:ss");

        return
$@"#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:{fileName}
// Guid:{guid}
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:{createTime}
// ----------------------------------------------------------------

#endregion <<版权版本注释>>";
    }

    /// <summary>
    /// 生成当前时间前后两小时内的随机时间
    /// </summary>
    private static DateTime RandomTimeWithinTwoHours()
    {
        var now = DateTime.Now;
        var random = new Random();
        var offsetMinutes = random.Next(-120, 120);
        return now.AddMinutes(offsetMinutes);
    }
}
