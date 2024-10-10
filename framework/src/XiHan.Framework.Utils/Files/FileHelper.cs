#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2023 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileHelper
// Guid:79b99f42-e72d-4333-bbb2-368d7fed89e2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023/7/20 20:51:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Files;

/// <summary>
/// 文件操作帮助类
/// </summary>
public static class FileHelper
{
    #region 清空文件或目录

    /// <summary>
    /// 清空指定目录下所有文件及子目录
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    public static void CleanDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;
        // 删除目录中所有的文件
        var fileNames = GetFiles(directoryPath);
        foreach (var t in fileNames) DeleteFile(t);
        // 删除目录中所有的子目录
        var directoryNames = GetDirectories(directoryPath);
        foreach (var t in directoryNames) DeleteDirectory(t);
    }

    /// <summary>
    /// 清空文件内容
    /// </summary>
    /// <param name="filePath">文件的绝对路径</param>
    public static void CleanFile(string filePath)
    {
        if (!File.Exists(filePath)) return;
        // 删除文件
        File.Delete(filePath);
        // 重新创建该文件
        CreateFile(filePath, string.Empty, Encoding.UTF8);
    }

    #endregion

    #region 复制文件或目录

    /// <summary>
    /// 将源文件的内容复制到目标文件中
    /// </summary>
    /// <param name="sourceFilePath">源文件的绝对路径</param>
    /// <param name="destFilePath">目标文件的绝对路径</param>
    public static void CopyFile(string sourceFilePath, string destFilePath)
    {
        File.Copy(sourceFilePath, destFilePath, true);
    }

    /// <summary>
    /// 复制文件夹(递归)
    /// </summary>
    /// <param name="varFromDirectory">源文件夹路径</param>
    /// <param name="varToDirectory">目标文件夹路径</param>
    public static void CopyFolder(string varFromDirectory, string varToDirectory)
    {
        Directory.CreateDirectory(varToDirectory);
        if (!Directory.Exists(varFromDirectory)) return;

        var directories = Directory.GetDirectories(varFromDirectory);
        if (directories.Length > 0)
            foreach (var d in directories)
                CopyFolder(d, varToDirectory + d[d.LastIndexOf('\\')..]);

        var files = Directory.GetFiles(varFromDirectory);
        if (files.Length <= 0) return;

        foreach (var s in files)
            File.Copy(s, varToDirectory + s[s.LastIndexOf('\\')..], true);
    }

    #endregion

    #region 移动文件

    /// <summary>
    /// 移动文件(剪贴粘贴)
    /// </summary>
    /// <param name="dir1">旧文件路径</param>
    /// <param name="dir2">新文件路径</param>
    public static void MoveFile(string dir1, string dir2)
    {
        dir1 = dir1.Replace(@"/", @"\");
        dir2 = dir2.Replace(@"/", @"\");
        if (File.Exists(dir1)) File.Move(dir1, dir2);
    }

    /// <summary>
    /// 将文件移动到指定目录
    /// </summary>
    /// <param name="sourceFilePath">文件路径</param>
    /// <param name="descDirectoryPath">目标目录路径</param>
    public static void MoveFileToDirectory(string sourceFilePath, string descDirectoryPath)
    {
        // 获取源文件的名称
        var sourceFileName = GetFileNameWithExtension(sourceFilePath);
        if (!IsExistDirectory(descDirectoryPath)) return;
        // 如果目标中存在同名文件,则删除
        if (IsExistFile(descDirectoryPath + @"\" + sourceFileName))
            DeleteFile(descDirectoryPath + @"\" + sourceFileName);
        // 将文件移动到指定目录
        File.Move(sourceFilePath, descDirectoryPath + @"\" + sourceFileName);
    }

    #endregion

    #region 写入文件

    /// <summary>
    /// 向文本文件中写入内容
    /// </summary>
    /// <param name="filePath">文件的绝对路径</param>
    /// <param name="text">写入的内容</param>
    /// <param name="encoding">编码</param>
    public static void WriteText(string filePath, string text, Encoding encoding)
    {
        // 向文件写入内容
        File.WriteAllText(filePath, text, encoding);
    }

    #endregion

    #region 创建文件或目录

    /// <summary>
    /// 创建包含文件名完整路径的文件夹
    /// </summary>
    /// <param name="filePath">包含文件名完整路径</param>
    public static void CreateDirectory(string filePath)
    {
        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
    }

    /// <summary>
    /// 创建文件
    /// </summary>
    /// <param name="dir">带后缀的文件名</param>
    /// <param name="pageStr">文件内容</param>
    /// <param name="encoding">文件编码</param>
    public static void CreateFile(string dir, string pageStr, Encoding encoding)
    {
        dir = dir.Replace(@"/", @"\");
        if (dir.IndexOf('\\') > -1)
            CreateDirectory(dir[..dir.LastIndexOf('\\')]);

        StreamWriter sw = new(dir, false, encoding);
        sw.Write(pageStr);
        sw.Close();
    }

    #endregion

    #region 删除文件或目录

    /// <summary>
    /// 删除目录及其所有子目录
    /// </summary>
    /// <param name="dir">要删除的目录路径和名称</param>
    public static void DeleteDirectory(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="file">要删除的文件路径和名称</param>
    public static void DeleteFile(string file)
    {
        if (File.Exists(file)) File.Delete(file);
    }

    /// <summary>
    /// 仅删除指定文件夹和子文件夹的文件
    /// </summary>
    /// <param name="varFromDirectory"></param>
    /// <param name="varToDirectory"></param>
    public static void DeleteFolderFiles(string varFromDirectory, string varToDirectory)
    {
        Directory.CreateDirectory(varToDirectory);
        if (!Directory.Exists(varFromDirectory)) return;

        var directories = Directory.GetDirectories(varFromDirectory);
        if (directories.Length > 0)
            foreach (var d in directories)
                DeleteFolderFiles(d,
                    string.Concat(varToDirectory, d.AsSpan(d.LastIndexOf('\\'))));

        var files = Directory.GetFiles(varFromDirectory);
        if (files.Length <= 0) return;

        foreach (var s in files)
            File.Delete(string.Concat(varToDirectory, s.AsSpan(s.LastIndexOf('\\'))));
    }

    #endregion

    #region 获取名称或扩展名称

    /// <summary>
    /// 根据时间得到目录名
    /// yyyyMMdd
    /// </summary>
    /// <returns></returns>
    public static string GetDateDirName()
    {
        return DateTime.Now.ToString("yyyyMMdd");
    }

    /// <summary>
    /// 根据时间得到文件名
    /// yyyyMMddHHmmssfff
    /// </summary>
    /// <returns></returns>
    public static string GetDateFileName()
    {
        return DateTime.Now.ToString("yyyyMMddHHmmssfff");
    }

    /// <summary>
    /// 获取随机文件名
    /// </summary>
    /// <returns></returns>
    public static string GetRandomFileName()
    {
        return Path.GetRandomFileName();
    }

    /// <summary>
    /// 从文件的绝对路径中获取扩展名
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static string GetFileExtension(string filePath)
    {
        return Path.GetExtension(filePath);
    }

    /// <summary>
    /// 从文件的绝对路径中获取文件名(不包含扩展名)
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static string GetFileNameWithoutExtension(string filePath)
    {
        return Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>
    /// 从文件的绝对路径中获取文件名(包含扩展名)
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static string GetFileNameWithExtension(string filePath)
    {
        return Path.GetFileName(filePath);
    }

    /// <summary>
    /// 生成唯一的文件名，上传文件使用
    /// </summary>
    /// <param name="fileName">包含扩展名的源文件名</param>
    /// <returns></returns>
    public static string GetUniqueFileName(string fileName)
    {
        var fileNameWithoutExtension = GetFileNameWithoutExtension(fileName);
        var fileExtension = GetFileExtension(fileName);
        var uniqueFileName = $"{GetDateFileName()}_{fileNameWithoutExtension}_{GetRandomFileName()}";
        return uniqueFileName + fileExtension;
    }

    #endregion

    #region 获取目录信息

    /// <summary>
    /// 获取指定目录中所有子目录列表,若要搜索嵌套的子目录列表,请使用重载方法
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <returns></returns>
    public static IEnumerable<string> GetDirectories(string directoryPath)
    {
        return Directory.GetDirectories(directoryPath);
    }

    /// <summary>
    /// 获取指定目录及子目录中所有子目录列表
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    /// <param name="searchPattern">模式字符串，"*"代表0或N个字符，"?"代表1个字符。范例："Log*.xml"表示搜索所有以Log开头的Xml文件。</param>
    /// <param name="isSearchChild">是否搜索子目录</param>
    /// <returns></returns>
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
    public static long GetDirectorySize(string dirPath)
    {
        // 定义一个DirectoryInfo对象
        DirectoryInfo di = new(dirPath);
        // 通过GetFiles方法,获取di目录中的所有文件的大小
        var len = di.GetFiles().Sum(fi => fi.Length);
        // 获取di中所有的文件夹,并存到一个新的对象数组中,以进行递归
        var dis = di.GetDirectories();
        if (dis.Length <= 0) return len;

        len += dis.Sum(t => GetDirectorySize(t.FullName));
        return len;
    }

    #endregion

    #region 获取文件列表

    /// <summary>
    /// 获取指定目录中所有文件列表
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    /// <returns></returns>
    public static IEnumerable<string> GetFiles(string directoryPath)
    {
        // 如果目录不存在，则抛出异常
        if (!IsExistDirectory(directoryPath)) throw new FileNotFoundException();
        // 获取文件列表
        return Directory.GetFiles(directoryPath);
    }

    /// <summary>
    /// 查找指定目录及子目录中指定名称文件列表
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    /// <param name="searchPattern">模式字符串，"*"代表0或N个字符，"?"代表1个字符。范例："Log*.xml"表示搜索所有以Log开头的Xml文件。</param>
    /// <param name="isSearchChild">是否搜索子目录</param>
    /// <returns></returns>
    public static string[] GetFiles(string directoryPath, string searchPattern, bool isSearchChild)
    {
        // 如果目录不存在，则抛出异常
        return !IsExistDirectory(directoryPath)
            ? throw new FileNotFoundException()
            : Directory.GetFiles(directoryPath, searchPattern,
                isSearchChild ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }

    #endregion

    #region 获取文件信息

    /// <summary>
    /// 获取指定文件大小
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static long GetFileSize(string filePath)
    {
        long temp = 0;
        // 判断当前路径所指向的是否为文件
        if (File.Exists(filePath))
        {
            // 定义一个FileInfo对象,使之与filePath所指向的文件向关联,以获取其大小
            FileInfo fileInfo = new(filePath);
            return fileInfo.Length;
        }

        var str1 = Directory.GetFileSystemEntries(filePath);
        temp += str1.Sum(GetFileSize);
        return temp;
    }

    /// <summary>
    /// 获取文本文件的行数
    /// </summary>
    /// <param name="filePath">文件的绝对路径</param>
    /// <returns></returns>
    public static int GetLineCount(string filePath)
    {
        // 将文本文件的各行读到一个字符串数组中
        var rows = File.ReadAllLines(filePath);
        // 返回行数
        return rows.Length;
    }

    #endregion

    #region 检测文件或目录

    /// <summary>
    /// 检测指定目录中是否存在指定的文件
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    /// <param name="searchPattern">模式字符串，"*"代表0或N个字符，"?"代表1个字符。范例："Log*.xml"表示搜索所有以Log开头的Xml文件。</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static bool IsContainsFiles(string directoryPath, string searchPattern)
    {
        try
        {
            // 获取指定的文件列表
            var fileNames = GetFiles(directoryPath, searchPattern, false);
            // 判断指定文件是否存在
            return fileNames.Length != 0;
        }
        catch (Exception ex)
        {
            ex.ThrowAndConsoleError("获取文件信息出错!");
        }

        return false;
    }

    /// <summary>
    /// 检测指定目录中是否存在指定的文件(搜索子目录)
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    /// <param name="searchPattern">模式字符串，"*"代表0或N个字符，"?"代表1个字符。范例："Log*.xml"表示搜索所有以Log开头的Xml文件。</param>
    /// <param name="isSearchChild">是否搜索子目录</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static bool IsContainsFiles(string directoryPath, string searchPattern, bool isSearchChild)
    {
        try
        {
            // 获取指定的文件列表
            var fileNames = GetFiles(directoryPath, searchPattern, isSearchChild);
            // 判断指定文件是否存在
            return fileNames.Length != 0;
        }
        catch (Exception ex)
        {
            ex.ThrowAndConsoleError("获取文件信息出错!");
        }

        return false;
    }

    /// <summary>
    /// 检测指定目录是否为空
    /// </summary>
    /// <param name="directoryPath">指定目录的绝对路径</param>
    /// <returns></returns>
    public static bool IsEmptyDirectory(string directoryPath)
    {
        // 判断是否存在文件
        var fileNames = GetFiles(directoryPath);
        if (fileNames.Any()) return false;
        // 判断是否存在文件夹
        var directoryNames = GetDirectories(directoryPath);
        return !directoryNames.Any();
    }

    /// <summary>
    /// 检测指定目录是否存在
    /// </summary>
    /// <param name="directoryPath">目录的绝对路径</param>
    /// <returns></returns>
    public static bool IsExistDirectory(string directoryPath)
    {
        return Directory.Exists(directoryPath);
    }

    /// <summary>
    /// 检测指定文件是否存在,如果存在则返回true。
    /// </summary>
    /// <param name="filePath">文件的绝对路径</param>
    /// <returns></returns>
    public static bool IsExistFile(string filePath)
    {
        return File.Exists(filePath);
    }

    /// <summary>
    /// 检查文件,如果文件不存在则创建
    /// </summary>
    /// <param name="filePath">路径,包括文件名</param>
    /// <returns></returns>
    public static void ExistsFile(string filePath)
    {
        if (File.Exists(filePath)) return;

        var fs = File.Create(filePath);
        fs.Close();
    }

    #endregion
}