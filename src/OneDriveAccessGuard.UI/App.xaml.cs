using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Infrastructure.Data;
using OneDriveAccessGuard.Infrastructure.Graph;
using OneDriveAccessGuard.Infrastructure.Settings;
using OneDriveAccessGuard.UI.ViewModels;
using OneDriveAccessGuard.UI.Views;
using Serilog;
using System.IO;
using System.Windows;

namespace OneDriveAccessGuard.UI;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Serilog 設定
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                path: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OneDriveAccessGuard", "logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        // DBマイグレーション
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessGuardDbContext>();
        await db.Database.EnsureCreatedAsync();

        // メインウィンドウ表示
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // 設定サービス（レジストリ管理）
        services.AddSingleton<ISettingsService, RegistrySettingsService>();

        // Graph サービス（設定が揃ったときに初めて接続）
        services.AddSingleton<IGraphService, GraphService>();

        // SQLite DB
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OneDriveAccessGuard", "data.db");
        /*
        services.AddDbContext<AccessGuardDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}"));
        */
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!); // DBフォルダを事前作成
        services.AddDbContext<AccessGuardDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}"),
            ServiceLifetime.Transient);

        // ★ リポジトリの登録
        services.AddTransient<ISharedItemRepository, SharedItemRepository>();
        services.AddTransient<IAuditLogRepository, AuditLogRepository>();
        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ScanViewModel>();
        services.AddTransient<SharedItemsViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        //services.AddTransient<LoginWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
