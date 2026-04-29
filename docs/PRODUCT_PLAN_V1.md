# Screen Time 产品落地方案 v1

## 产品定位

Screen Time（屏幕时间）是一款 Windows 桌面端屏幕时间记录工具。核心是软件级使用时长统计，提醒只是辅助，让用户在看到真实使用数据的同时，能被温和地拉回休息节奏。

## 核心价值

- 看清今天电脑总共用了多久。
- 看清每个软件分别用了多久。
- 看清连续使用时长，避免长时间不休息。
- 所有数据本地保存，不上传、不截图、不读取内容。

## MVP

- 今日使用概览。
- 每日/每周屏幕时间图表。
- 应用时长列表和分类统计。
- 前台软件识别、空闲检测、持续本地保存。
- 休息提醒、提醒形象、托盘提醒动画。
- 设置页和系统托盘。

## 信息架构

主窗口：
- 今日使用
- 连续使用
- 下次休息
- 运行状态
- 屏幕时间趋势图
- 应用时长列表

设置窗口：
- 提醒规则
- 外观与提醒
- 系统行为
- 隐私与数据

## 数据目录

```text
%AppData%/ScreenTime/
  settings.json
  app_categories.json
  usage/
  sessions/
  assets/
  icon-cache/
```

## 命名约定

- 产品中文名：屏幕时间
- 产品英文名：Screen Time
- 解决方案：ScreenTime.sln
- 主项目：src/ScreenTime/ScreenTime.csproj
- 测试项目：tests/ScreenTime.Tests/ScreenTime.Tests.csproj
- 命名空间：ScreenTime
- 本地数据目录：%AppData%/ScreenTime/

## 验收标准

- 构建通过且测试通过。
- UI 文案以“屏幕时间”为主。
- 项目内容和文件名统一使用 Screen Time / 屏幕时间。
- 托盘提醒命名调整为托盘提醒/提醒形象，不再把提醒作为产品核心。
