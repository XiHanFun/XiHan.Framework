using System.Text;
using System.Text.RegularExpressions;

internal class Program
{
    private const string RegionName = "<<版权版本注释>>";
    private const int HeaderLineCount = 12;

    private static readonly Regex HeaderRegex = new(
        @"^\s*#region\s+<<版权版本注释>>[\s\S]*?#endregion\s+<<版权版本注释>>\r?\n\r?\n?",
        RegexOptions.Multiline | RegexOptions.Compiled);

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
        var guid = Guid.NewGuid().ToString();
        var now = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

        if (HeaderRegex.IsMatch(content))
        {
            var oldHeader = HeaderRegex.Match(content).Value;

            if (IsHeaderValid(oldHeader, fileName))
            {
                var newHeader = UpdateHeader(oldHeader, fileName, guid);
                content = content.Replace(oldHeader, newHeader);
                Console.WriteLine($"更新头部：{filePath}");
            }
            else
            {
                var newHeader = BuildHeader(fileName, guid, now);
                content = content.Replace(oldHeader, newHeader);
                Console.WriteLine($"重建头部：{filePath}");
            }
        }
        else
        {
            var header = BuildHeader(fileName, guid, now);
            content = header + content;
            Console.WriteLine($"插入头部：{filePath}");
        }

        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    /// <summary>
    /// 严格校验版权头是否完全符合规范
    /// </summary>
    private static bool IsHeaderValid(string header, string expectedFileName)
    {
        var lines = header
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(l => l.Length > 0)
            .ToArray();

        // 1️ 行数必须严格等于 12
        if (lines.Length != HeaderLineCount)
            return false;

        // 2️ FileName 必须匹配
        var fileNameLine = lines.FirstOrDefault(l => l.Contains("FileName:"));
        if (fileNameLine == null || !fileNameLine.EndsWith(expectedFileName))
            return false;

        // 3️ Guid 必须合法
        var guidLine = lines.FirstOrDefault(l => l.Contains("Guid:"));
        if (guidLine == null ||
            !Guid.TryParse(guidLine.Split(':').Last().Trim(), out _))
            return false;

        // 4️ CreateTime 必须可解析
        var timeLine = lines.FirstOrDefault(l => l.Contains("CreateTime:"));
        if (timeLine == null ||
            !DateTime.TryParse(timeLine.Split(':', 2).Last().Trim(), out _))
            return false;

        return true;
    }

    private static string UpdateHeader(string header, string fileName, string guid)
    {
        header = Regex.Replace(header,
            @"//\s*FileName\s*:.*",
            $"// FileName:{fileName}");

        header = Regex.Replace(header,
            @"//\s*Guid\s*:.*",
            $"// Guid:{guid}");

        return header;
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

#endregion <<版权版本注释>>

";
    }
}
