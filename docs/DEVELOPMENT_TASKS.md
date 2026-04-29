# Screen Time 开发任务拆解

## T1 统计核心

- ForegroundAppTracker：获取前台软件。
- IdleDetector：判断用户空闲。
- UsageTimer：按活跃状态累计时长。
- ReminderScheduler：触发休息提醒。

## T2 存储

- SettingsStore：设置读写和默认值。
- UsageStore：每日统计读写。
- AtomicFileWriter：原子写入。
- AppDataPaths：统一使用 `%AppData%/ScreenTime/`。

## T3 UI

- MainWindow：屏幕时间看板。
- SettingsWindow：设置。
- ReminderOverlayWindow：休息提醒。
- TrayService：托盘入口。

## T4 命名整理

- 解决方案改为 ScreenTime.sln。
- 项目目录改为 src/ScreenTime。
- 测试目录改为 tests/ScreenTime.Tests。
- 命名空间改为 ScreenTime。
- 设置字段使用 `ReminderCharacter`。
- 托盘动画类使用 `TrayReminder...`。

## T5 验证

- `dotnet build ScreenTime.sln`
- `dotnet run --project tests\ScreenTime.Tests\ScreenTime.Tests.csproj`
