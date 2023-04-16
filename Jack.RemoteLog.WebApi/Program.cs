using Jack.RemoteLog.WebApi;
using Jack.RemoteLog.WebApi.Applications;
using Quartz.Impl;
using Quartz;
using Jack.RemoteLog.WebApi.AutoMissions;
using Microsoft.Extensions.FileProviders;
using Quartz.Impl.AdoJobStore.Common;
using System.IO;
using Jack.RemoteLog.WebApi.Controllers;
using JMS.Common;
using Microsoft.Extensions.Primitives;
using System.Text;
using JMS;
using Jack.RemoteLog.WebApi.Dtos;

Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
ThreadPool.GetMinThreads(out int w, out int c);
if(w < 500 || c < 500)
{
    ThreadPool.SetMinThreads(500, 500);
}

CommandArgParser cmdArg = new CommandArgParser(args);
var appSettingPath = cmdArg.TryGetValue<string>("-s");

if (appSettingPath == null)
    appSettingPath = "appsettings.json";

if (appSettingPath == "share")
{
    appSettingPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    appSettingPath = Path.Combine(appSettingPath, "jack.remotelog.webapi");
    if (Directory.Exists(appSettingPath) == false)
    {
        Directory.CreateDirectory(appSettingPath);
    }
    appSettingPath = Path.Combine(appSettingPath, "appsettings.json");
    if (File.Exists(appSettingPath) == false)
    {
        File.Copy("./appsettings.json", appSettingPath);
    }
}


var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.AddJsonFile(appSettingPath,
                       optional: true,
                       reloadOnChange: true);
});

// Add services to the container.
Global.Configuration = builder.Configuration;

builder.Services.AddControllers();

builder.Services.AddSingleton<LogChannelRoute>();
builder.Services.AddSingleton<LogService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("abc", builder =>
    {
        //App:CorsOrigins in appsettings.json can contain more than one address with splitted by comma.
        builder
          .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();

    });
});

var app = builder.Build();

Global.ServiceProvider = app.Services;
// Configure the HTTP request pipeline.


app.UseStaticFiles();
//����index.html
app.UseFileServer();

app.UseAuthorization();
app.UseCors("abc");
app.MapControllers();

var logService = app.Services.GetService<LogService>();
var userInfos = app.Configuration.GetSection("Users").GetNewest<UserInfo[]>();

app.Use((context, next) => {
    bool pass = false;
    bool iswriting = context.Request.Path.ToString().Contains("/WriteLog");
    
    if (userInfos.Current == null || userInfos.Current.Length == 0)
    {
        pass = true;
    }
    else if (context.Request.Headers.TryGetValue("Authorization", out StringValues authorization))
    {
        var base64 = authorization.ToString().Substring(6);
        var name_pwds = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split(':');
        var user = userInfos.Current?.FirstOrDefault(m => string.Equals(name_pwds[0], m.Name, StringComparison.OrdinalIgnoreCase));
        if (user == null || user.Password != name_pwds[1] || (iswriting && user.Writeable == false))
        {
            //��ͨ�������֤
        }
        else
        {
            pass = true;
        }
    }

    if (!pass)
    {
        context.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"Welcome to Jack.RemoteLog\"");
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    }

    if (context.Request.Path.ToString().Contains("/WriteLog"))
    {
        //���ֱ������controller����post�������ᵼ�²�ѯ���������ʵ��߳�Խ���Խ����ԭ����
        return LogController.HandleWriteLog(context, logService);
    }

    return next();
});

ISchedulerFactory sf = new StdSchedulerFactory();
IScheduler scheduler = sf.GetScheduler().ConfigureAwait(false).GetAwaiter().GetResult();

IJobDetail job = JobBuilder.Create<AutoDeleteLogs>().WithIdentity("job1", "mygroup").Build();

var cronExpression = "0 5 1 * * ?";//ÿ��1��05��ִ��
ITrigger trigger = TriggerBuilder.Create().StartAt(DateTime.Now.AddSeconds(50)).WithCronSchedule(cronExpression).Build();

scheduler.ScheduleJob(job, trigger);
scheduler.Start();

var logchannelRoute = app.Services.GetService<LogChannelRoute>();

var logger = app.Services.GetService<ILogger<Program>>();
logger.LogInformation("AppSettings Path: {0}" , appSettingPath);
logger.LogInformation($"Version��{typeof(Global).Assembly.GetName().Version}");

app.Run();
logchannelRoute.Dispose();