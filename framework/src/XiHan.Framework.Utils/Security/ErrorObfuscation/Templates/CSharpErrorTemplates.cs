#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CSharpErrorTemplates
// Guid:3a4b5c6d-8e9f-0a1b-2c3d-4e5f6a7b8c9d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

/// <summary>
/// C# 错误模板
/// </summary>
internal static class CSharpErrorTemplates
{
    /// <summary>
    /// C# 错误模板集合
    /// </summary>
    public static readonly string[] Templates =
    [
        "System.NullReferenceException: Object reference not set to an instance of an object.\n   at System.Web.Mvc.Controller.ExecuteCore()\n   at System.Web.Mvc.ControllerBase.Execute(RequestContext requestContext)\n   at System.Web.Mvc.MvcHandler.ProcessRequest(HttpContextBase httpContext)",
        "System.InvalidOperationException: The connection was not closed. The connection's current state is open.\n   at System.Data.SqlClient.SqlInternalConnectionTds.Activate()\n   at System.Data.ProviderBase.DbConnectionInternal.TryOpenConnection()\n   at System.Data.SqlClient.SqlConnection.Open()",
        "System.ArgumentNullException: Value cannot be null. Parameter name: source\n   at System.Linq.Enumerable.Count[TSource](IEnumerable`1 source)\n   at MyApp.Services.UserService.GetUsers()\n   at MyApp.Controllers.HomeController.Index()",
        "System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown.\n   at System.Text.StringBuilder.ExpandByABlock(Int32 minBlockCharCount)\n   at System.Text.StringBuilder.Append(String value)\n   at MyApp.Helpers.LogHelper.WriteLog(String message)",
        "System.UnauthorizedAccessException: Access to the path 'C:\\Program Files\\MyApp\\config.xml' is denied.\n   at System.IO.__Error.WinIOError(Int32 errorCode, String maybeFullPath)\n   at System.IO.FileStream.Init(String path, FileMode mode)\n   at System.IO.File.ReadAllText(String path)",
        "System.TimeoutException: The operation has timed out.\n   at System.Net.HttpWebRequest.GetResponse()\n   at MyApp.Services.ApiService.CallExternalApi()\n   at MyApp.Controllers.ApiController.Process()",
        "System.FormatException: Input string was not in a correct format.\n   at System.Number.ParseDouble(String s, NumberStyles style, NumberFormatInfo info)\n   at MyApp.Helpers.DataConverter.ConvertToDecimal(String value)\n   at MyApp.Services.CalculationService.Calculate()",
        "System.DivideByZeroException: Attempted to divide by zero.\n   at MyApp.Business.MathOperations.Divide(Int32 a, Int32 b)\n   at MyApp.Controllers.CalculatorController.Calculate(Int32 x, Int32 y)"
    ];
}
