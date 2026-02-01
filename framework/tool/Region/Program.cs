using System.Text;
using System.Text.RegularExpressions;

internal class Program
{
    private static void Main()
    {
        var root = Directory.GetCurrentDirectory();
        var csFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            try
            {
                ProcessFile(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"失败：{file}");
                Console.WriteLine(ex.Message);
            }
        }

        Console.WriteLine("全部处理完成");
        Console.ReadKey();
    }

    private static void ProcessFile(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var newGuid = Guid.NewGuid().ToString();
        var now = DateTime.Now.ToString("yyyy/M/d H:mm:ss");

        var regionPattern =
            @"^#region\s+<<版权版本注释>>[\s\S]*?#endregion\s+<<版权版本注释>>\s*";

        if (Regex.IsMatch(content, regionPattern, RegexOptions.Multiline))
        {
            // 已存在版权头 → 只更新 FileName / Guid
            var regionMatch = Regex.Match(content, regionPattern, RegexOptions.Multiline);
            var regionText = regionMatch.Value;

            regionText = Regex.Replace(regionText,
                @"//\s*FileName\s*:.*",
                $"// FileName:{fileName}");

            regionText = Regex.Replace(regionText,
                @"//\s*Guid\s*:.*",
                $"// Guid:{newGuid}");

            var newContent = content.Replace(regionMatch.Value, regionText);
            File.WriteAllText(filePath, newContent, Encoding.UTF8);

            Console.WriteLine($"🔁 更新头部：{filePath}");
        }
        else
        {
            // 不存在版权头 → 自动插入
            var header = BuildHeader(fileName, newGuid, now);
            var newContent = header + Environment.NewLine + content;

            File.WriteAllText(filePath, newContent, Encoding.UTF8);

            Console.WriteLine($"插入头部：{filePath}");
        }
    }

    private static string BuildHeader(string fileName, string guid, string createTime)
    {
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
}
