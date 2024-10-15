#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExceptionsTest
// Guid:cc26a6ea-68f6-44e3-af1f-07d410326150
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/16 3:24:22
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Exceptions;

namespace XiHan.Framework.Utils.Test.Exceptions;

/// <summary>
/// ExceptionsTest
/// </summary>
public class ExceptionsTest
{
    /// <summary>
    /// 测试自定义异常
    /// </summary>
    public static void CustomExceptionTest()
    {
        Console.WriteLine("CustomException Test");

        CustomException.Throw("CustomException Test");
    }

    /// <summary>
    /// 测试曦寒异常
    /// </summary>
    public static void XiHanExceptionTest()
    {
        Console.WriteLine("XiHanException Test");

        XiHanException.Throw("XiHanException Test");
    }
}