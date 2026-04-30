# 屏幕时间

一款轻量的 Windows 屏幕时间工具，专注统计本机软件使用时长，并在连续使用过久时用可爱的提醒动画提示你休息。

## 预览

![主界面预览](src/ScreenTime/Assets/preview_0.png)

![设置界面预览](src/ScreenTime/Assets/preview_1.png)

## 功能

- 统计今日和每周的软件使用时长
- 按工作、社交、娱乐、学习、系统、其他分类查看占比
- 支持连续使用提醒、休息倒计时和全屏提醒遮罩
- 支持小猫提醒形象和托盘动画
- 支持浅色、深色、跟随系统主题
- 支持最小化到托盘和开机自启动
- 数据保存在本机 AppData，不上传云端

## 隐私

屏幕时间只记录软件名、进程名、可执行路径、活跃秒数、分类和提醒记录。不会记录键盘输入、截图录屏、聊天文档内容、浏览器 URL 或账号数据。

## 开发

```powershell
dotnet build ScreenTime.sln
dotnet run --project src/ScreenTime/ScreenTime.csproj
```

## 测试

```powershell
dotnet test ScreenTime.sln
```
