// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// Windows 上以 POSIX 语义替换改名
/// </summary>
/// <remarks>
/// 普通的替换改名（<c>MoveFileEx</c> 带 <c>MOVEFILE_REPLACE_EXISTING</c>，即 <see cref="File.Move(string, string, bool)"/>）替换不了正被打开的目标，读取方打开时容许删除共享也不行；带 <c>FILE_RENAME_FLAG_POSIX_SEMANTICS</c> 的改名只要目标的句柄都容许删除共享就照样替换，名字立即指向新文件，已打开旧文件的读取方读完旧内容，与 Unix 的 <c>rename(2)</c> 相同（ADR-0241）
/// </remarks>
[SupportedOSPlatform("windows")]
internal static partial class PosixRename
{
    /// <summary>
    /// 卷不支持这种改名：未知的信息类别（如 SMB 共享，本机实测）
    /// </summary>
    private const int ErrorInvalidParameter = 87;

    /// <summary>
    /// 卷不支持这种改名：文件系统不支持该操作
    /// </summary>
    private const int ErrorNotSupported = 50;

    /// <summary>
    /// 卷不支持这种改名：驱动不认这个请求
    /// </summary>
    private const int ErrorInvalidFunction = 1;

    /// <summary>
    /// 文件不存在
    /// </summary>
    private const int ErrorFileNotFound = 2;

    /// <summary>
    /// 路径不存在
    /// </summary>
    private const int ErrorPathNotFound = 3;

    /// <summary>
    /// 拒绝访问
    /// </summary>
    private const int ErrorAccessDenied = 5;

    /// <summary>
    /// 删除权限（改名要求源句柄带它）
    /// </summary>
    private const uint DeleteAccess = 0x00010000;

    /// <summary>
    /// 同步权限
    /// </summary>
    private const uint SynchronizeAccess = 0x00100000;

    /// <summary>
    /// 容许读、写与删除共享
    /// </summary>
    private const uint ShareAll = 0x7;

    /// <summary>
    /// 只打开已有的文件
    /// </summary>
    private const uint OpenExisting = 3;

    /// <summary>
    /// <c>FILE_INFO_BY_HANDLE_CLASS.FileRenameInfoEx</c>
    /// </summary>
    private const int FileRenameInfoEx = 22;

    /// <summary>
    /// <c>FILE_RENAME_FLAG_REPLACE_IF_EXISTS | FILE_RENAME_FLAG_POSIX_SEMANTICS</c>
    /// </summary>
    private const uint ReplaceWithPosixSemantics = 0x1 | 0x2;

    /// <summary>
    /// 把 <paramref name="source"/> 改名为 <paramref name="destination"/>，目标存在就以 POSIX 语义替换
    /// </summary>
    /// <remarks>
    /// 两个都是完整路径（已经 <see cref="Path.GetFullPath(string)"/> 规范化）、在同一个卷上。返回 <see langword="false"/> 表示卷不支持这种改名、什么都没动，调用方改用普通的替换改名；其余失败照常抛出 <see cref="IOException"/> 或 <see cref="UnauthorizedAccessException"/>，目标被不容许删除共享的句柄打开着时是共享冲突
    /// </remarks>
    public static bool TryReplace(string source, string destination)
    {
        using var handle = NativeMethods.CreateFileW(Extended(source), DeleteAccess | SynchronizeAccess, ShareAll, 0, OpenExisting, 0, 0);
        if (handle.IsInvalid)
        {
            throw Failure(Marshal.GetLastPInvokeError(), source);
        }

        // FILE_RENAME_INFO：Flags（与 ReplaceIfExists 共用一个 DWORD）、RootDirectory 句柄（空）、FileNameLength（字节，不含结尾的 0）、FileName；
        // 字段按指针对齐，64 位上名字从第 20 个字节起，32 位上从第 12 个字节起。名字至少有前缀的 4 个字符，缓冲区总不小于结构本身
        var target = Extended(destination);
        var lengthOffset = IntPtr.Size == 8 ? 16 : 8;
        var nameOffset = lengthOffset + sizeof(uint);
        var nameBytes = target.Length * sizeof(char);
        var info = new byte[nameOffset + nameBytes + sizeof(char)];
        BinaryPrimitives.WriteUInt32LittleEndian(info, ReplaceWithPosixSemantics);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(lengthOffset), (uint)nameBytes);
        MemoryMarshal.AsBytes(target.AsSpan()).CopyTo(info.AsSpan(nameOffset));
        if (NativeMethods.SetFileInformationByHandle(handle, FileRenameInfoEx, info, (uint)info.Length))
        {
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        if (error is ErrorInvalidParameter or ErrorNotSupported or ErrorInvalidFunction)
        {
            return false;
        }

        throw Failure(error, destination);
    }

    /// <summary>
    /// 完整路径加上扩展长度前缀
    /// </summary>
    /// <remarks>
    /// 改名经句柄进行，不经 .NET 的路径处理：不加前缀时超过 260 个字符的路径打不开（本机实测 <c>ERROR_PATH_NOT_FOUND</c>）。UNC 路径写成 <c>\\?\UNC\服务器\共享\…</c>，已带前缀的原样返回
    /// </remarks>
    private static string Extended(string path)
        => path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal)
            ? path
            : path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;

    /// <summary>
    /// Win32 错误 → 与 <see cref="File.Move(string, string, bool)"/> 同类的异常
    /// </summary>
    /// <remarks>
    /// 调用方按 <see cref="IOException"/> 与 <see cref="UnauthorizedAccessException"/> 处理，消息只进日志
    /// </remarks>
    private static Exception Failure(int error, string path)
    {
        var message = $"Renaming over '{path}' failed with Win32 error {error}.";
        return error switch
        {
            ErrorFileNotFound => new FileNotFoundException(message, path),
            ErrorPathNotFound => new DirectoryNotFoundException(message),
            ErrorAccessDenied => new UnauthorizedAccessException(message),
            _ => new IOException(message, unchecked((int)0x80070000 | error)),
        };
    }

    /// <summary>
    /// kernel32 导入
    /// </summary>
    private static partial class NativeMethods
    {
        /// <summary>
        /// 打开文件
        /// </summary>
        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, nint securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

        /// <summary>
        /// 按句柄设置文件信息
        /// </summary>
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetFileInformationByHandle(SafeFileHandle file, int fileInformationClass, byte[] fileInformation, uint bufferSize);
    }
}
