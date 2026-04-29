# Screen Time 发布计划

## 构建

```powershell
dotnet build ScreenTime.sln
```

## 测试

```powershell
dotnet run --project tests\ScreenTime.Tests\ScreenTime.Tests.csproj
```

## 发布

框架依赖发布：

```powershell
dotnet publish src\ScreenTime\ScreenTime.csproj -c Release -r win-x64 --self-contained false -o publish\win-x64
```

自包含单文件候选包：

```powershell
dotnet publish src\ScreenTime\ScreenTime.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\win-x64-self-contained
```

## 发布前检查

- 主窗口“屏幕时间”看板能实时刷新。
- 应用时长列表、分类、每日/每周图表正常。
- 设置保存后立即生效。
- 休息提醒遵守“允许提前关闭提醒”设置。
- 托盘菜单可打开看板、设置并退出。
- 数据只写入 `%AppData%/ScreenTime/`。
- 项目产物命名为 ScreenTime.exe / ScreenTime.dll。
- 运行核心测试，全部通过。

## 资源约定

提醒形象托盘帧放在：

```text
%AppData%/ScreenTime/assets/
```

支持命名：

```text
cat_tray_low_0.ico
cat_tray_normal_0.ico
cat_tray_high_0.ico
cat_tray_veryhigh_0.ico
dog_tray_low_0.ico
dog_tray_normal_0.ico
dog_tray_high_0.ico
dog_tray_veryhigh_0.ico
```

缺少资源时，应用使用内置生成图标。