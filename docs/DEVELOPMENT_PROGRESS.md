# Screen Time 开发进度

## 已完成

- 项目统一改名为 Screen Time / 屏幕时间。
- 解决方案、项目文件、目录和命名空间已改为 ScreenTime。
- 本地数据目录改为 `%AppData%/ScreenTime/`。
- 托盘提醒相关命名调整为提醒形象/托盘提醒。
- 主界面、设置页、提醒页关键中文文案已改为正常中文。
- 旧构建产物和旧日志已清理。

## 验证

```powershell
dotnet build ScreenTime.sln
dotnet run --project tests\ScreenTime.Tests\ScreenTime.Tests.csproj
```

当前结果：构建通过，6 个核心测试通过。

## 后续

- 准备正式提醒形象 `.ico` 序列帧。
- 增加更完整的 UI 自动化检查。
- 发布前做多显示器提醒实机测试。
