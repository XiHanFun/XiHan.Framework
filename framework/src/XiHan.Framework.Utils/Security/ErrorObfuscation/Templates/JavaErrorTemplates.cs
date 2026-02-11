#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JavaErrorTemplates
// Guid:4b5c6d7e-9f0a-1b2c-3d4e-5f6a7b8c9d0e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

/// <summary>
/// Java 错误模板
/// </summary>
internal static class JavaErrorTemplates
{
    /// <summary>
    /// Java 错误模板集合
    /// </summary>
    public static readonly string[] Templates =
    [
        "java.lang.NullPointerException: Cannot invoke method on null object\n\tat com.example.service.UserService.findUser(UserService.java:42)\n\tat com.example.controller.HomeController.index(HomeController.java:28)\n\tat javax.servlet.http.HttpServlet.service(HttpServlet.java:731)",
        "java.sql.SQLException: No suitable driver found for jdbc:mysql://localhost:3306/mydb\n\tat java.sql.DriverManager.getConnection(DriverManager.java:689)\n\tat com.example.dao.DatabaseConnection.getConnection(DatabaseConnection.java:15)\n\tat com.example.service.DataService.getData(DataService.java:33)",
        "java.lang.OutOfMemoryError: Java heap space\n\tat java.util.Arrays.copyOf(Arrays.java:3332)\n\tat java.lang.AbstractStringBuilder.expandCapacity(AbstractStringBuilder.java:137)\n\tat com.example.util.StringProcessor.process(StringProcessor.java:56)",
        "java.lang.ClassCastException: java.lang.String cannot be cast to java.lang.Integer\n\tat com.example.service.TypeConverter.convert(TypeConverter.java:23)\n\tat com.example.controller.ApiController.handleRequest(ApiController.java:67)\n\tat org.springframework.web.method.support.InvocableHandlerMethod.invoke(InvocableHandlerMethod.java:221)",
        "java.io.FileNotFoundException: /opt/app/config/application.properties (No such file or directory)\n\tat java.io.FileInputStream.open0(Native Method)\n\tat java.io.FileInputStream.open(FileInputStream.java:195)\n\tat com.example.config.ConfigLoader.loadConfig(ConfigLoader.java:44)",
        "java.net.ConnectException: Connection refused (Connection refused)\n\tat java.net.PlainSocketImpl.socketConnect(Native Method)\n\tat java.net.AbstractPlainSocketImpl.doConnect(AbstractPlainSocketImpl.java:350)\n\tat com.example.service.RemoteService.connect(RemoteService.java:89)",
        "java.lang.IllegalArgumentException: Invalid parameter value\n\tat com.example.validator.InputValidator.validate(InputValidator.java:31)\n\tat com.example.controller.FormController.submitForm(FormController.java:52)\n\tat org.springframework.web.servlet.mvc.method.annotation.ServletInvocableHandlerMethod.invokeAndHandle(ServletInvocableHandlerMethod.java:102)",
        "javax.persistence.PersistenceException: Unable to build Hibernate SessionFactory\n\tat org.hibernate.jpa.boot.internal.EntityManagerFactoryBuilderImpl.build(EntityManagerFactoryBuilderImpl.java:1259)\n\tat com.example.config.DatabaseConfig.entityManagerFactory(DatabaseConfig.java:73)\n\tat com.example.Application.main(Application.java:29)"
    ];
}
