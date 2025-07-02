#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DirectoryHelper
// Guid:cf1009e0-c9d4-4bee-b84a-a731e7b161f0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/21 16:36:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.IO;

/// <summary>
/// 目录帮助类
/// </summary>
public static class DirectoryHelper
{
    #region 目录操作

    /// <summary>
    /// 创建一个新目录，如果目录已存在则不执行任何操作
    /// </summary>
    /// <param name="directoryPath">要创建的目录的路径</param>
    public static void CreateIfNotExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _ = Directory.CreateDirectory(directoryPath);
        }
    }

    /// <summary>
    /// 删除一个目录，如果目录存在
    /// </summary>
    /// <param name="directoryPath">要删除的目录的路径</param>
    public static void DeleteIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            // true 表示删除目录及其所有子目录和文件
            Directory.Delete(directoryPath, true);
        }
    }

    /// <summary>
    /// 清空一个目录，不删除目录本身，只删除其中的所有文件和子目录
    /// </summary>
    /// <param name="directoryPath">要清空的目录的路径</param>
    public static void Clear(string directoryPath)
    {
        foreach (var file in Directory.GetFiles(directoryPath))
        {
            File.Delete(file);
        }

        foreach (var dir in Directory.GetDirectories(directoryPath))
        {
            DeleteIfExists(dir);
        }
    }

    /// <summary>
    /// 复制一个目录到另一个位置
    /// </summary>
    /// <param name="sourcePath">当前目录的路径</param>
    /// <param name="destinationPath">目标目录的路径</param>
    /// <param name="overwrite">如果目标位置已经存在同名目录，是否覆盖</param>
    public static void Copy(string sourcePath, string destinationPath, bool overwrite = false)
    {
        try
        {
            // 检查目标目录是否存在，如果存在且 overwrite 为 false，则不执行复制
            if (Directory.Exists(destinationPath) && !overwrite)
            {
                throw new IOException("目标目录已存在且 overwrite 参数为 false ");
            }

            // 复制目录
            DirectoryInfo sourceDir = new(sourcePath);
            if (!Directory.Exists(destinationPath))
            {
                _ = Directory.CreateDirectory(destinationPath);
            }

            var files = sourceDir.GetFiles();
            foreach (var file in files)
            {
                _ = file.CopyTo(Path.Combine(destinationPath, file.Name), overwrite);
            }

            var dirs = sourceDir.GetDirectories();
            foreach (var dir in dirs)
            {
                var newDirPath = Path.Combine(destinationPath, dir.Name);
                Copy(dir.FullName, newDirPath, overwrite);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("复制目录出错", ex);
        }
    }

    #endregion 目录操作

    #region 目录信息

    /// <summary>
    /// 获取目录中所有文件的路径
    /// </summary>
    /// <param name="directoryPath">目录的路径 </param>
    /// <param name="searchPattern">模式字符串，"*"代表0或 N 个字符，"?"代表1个字符 范例:"Log*.xml"表示搜索所有以 Log 开头的 Xml 文件</param>
    /// <param name="isSearchChild">是否搜索子目录</param>
    /// <returns>包含目录中所有文件路径的数组</returns>
    public static string[] GetFiles(string directoryPath, string searchPattern, bool isSearchChild)
    {
        return Directory.GetFiles(directoryPath, searchPattern,
            isSearchChild ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }

    /// <summary>
    /// 获取指定目录及子目录中所有子目录列表
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    /// <param name="searchPattern">模式字符串，"*"代表0或 N 个字符，"?"代表1个字符 范例:"Log*.xml"表示搜索所有以 Log 开头的 Xml 目录</param>
    /// <param name="isSearchChild">是否搜索子目录</param>
    /// <returns>包含目录中所有文件路径的数组</returns>
    public static string[] GetDirectories(string directoryPath, string searchPattern, bool isSearchChild)
    {
        return Directory.GetDirectories(directoryPath, searchPattern,
            isSearchChild ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }

    /// <summary>
    /// 获取指定目录大小
    /// </summary>
    /// <param name="dirPath"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static long GetSize(string dirPath)
    {
        // 定义一个 DirectoryInfo 对象
        DirectoryInfo di = new(dirPath);
        // 通过 GetFiles 方法,获取 di 目录中的所有文件的大小
        var len = di.GetFiles().Sum(fi => fi.Length);
        // 获取 di 中所有的文件夹,并存到一个新的对象数组中,以进行递归
        var dis = di.GetDirectories();
        if (dis.Length <= 0)
        {
            return len;
        }

        len += dis.Sum(t => GetSize(t.FullName));
        return len;
    }

    /// <summary>
    /// 获取随机文件名
    /// </summary>
    /// <returns></returns>
    public static string GetRandomName()
    {
        return Path.GetRandomFileName();
    }

    /// <summary>
    /// 根据时间得到文件名
    /// yyyyMMddHHmmssfff
    /// </summary>
    /// <returns></returns>
    public static string GetDateName()
    {
        return DateTime.Now.ToString("yyyyMMddHHmmssfff");
    }

    #endregion 目录信息

    #region 目录检查

    /// <summary>
    /// 检测指定目录中是否存在指定的文件(搜索子目录)
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    /// <param name="searchPattern">模式字符串，"*"代表0或 N 个字符，"?"代表1个字符 范例:"Log*.xml"表示搜索所有以 Log 开头的 Xml 文件 </param>
    /// <param name="isSearchChild">是否搜索子目录</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static bool IsContainsFiles(string directoryPath, string searchPattern, bool isSearchChild)
    {
        // 获取指定的文件列表
        var fileNames = GetFiles(directoryPath, searchPattern, isSearchChild);
        // 判断指定文件是否存在
        return fileNames.Length != 0;
    }

    /// <summary>
    /// 检测指定目录是否为空
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    /// <returns></returns>
    public static bool IsEmpty(string directoryPath)
    {
        // 判断是否存在文件
        var fileNames = Directory.GetFiles(directoryPath);
        if (fileNames.Length != 0)
        {
            return false;
        }

        // 判断是否存在文件夹
        var directoryNames = Directory.GetDirectories(directoryPath);
        return directoryNames.Length == 0;
    }

    /// <summary>
    /// 返回应用程序的基目录
    /// 程序内部使用的路径，通常是程序所在的文件夹
    /// 举例：如果你的程序安装在 C:\MyApp\ 下，那么该属性通常返回 C:\MyApp\
    /// </summary>
    /// <returns></returns>
    public static string GetBaseDirectory()
    {
        //return AppDomain.CurrentDomain.BaseDirectory;
        // 在大多数情况下，它和 AppDomain.CurrentDomain.BaseDirectory 是一样的
        return AppContext.BaseDirectory;
    }

    /// <summary>
    /// 当前进程的工作目录，即程序启动时或运行过程中当前的“活动目录”
    /// 这个目录可以在程序运行过程中被修改，所以它不一定是程序所在的文件夹
    /// 举例：如果你从命令行的 D:\Projects 目录启动了程序，即使程序实际文件在 C:\MyApp\ 下，这个方法返回的就是 D:\Projects
    /// </summary>
    /// <returns></returns>
    public static string GetCurrentDirectory()
    {
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// 返回应用程序的默认静态文件目录
    /// 用于存放静态文件，如图片、CSS、JS 等
    /// </summary>
    /// <returns></returns>
    public static string GetWwwrootDirectory()
    {
        return Path.Combine(GetBaseDirectory(), "wwwroot");
    }

    #endregion 目录检查
}
