# Windows Power Monitor / 主机用电监控

Windows 10+ PC hardware power monitor. It shows real-time estimated whole-PC wattage, tracks daily/monthly electricity usage, and includes a draggable floating wattage bubble.

Windows 10+ 主机硬件功耗监控软件。能读取的硬件优先实时读取，读取不到的内部硬件按配件估算，只统计主机内部硬件，不统计外接设备。

![Floating bubble](HostPowerMonitor_Bubble_Current.png)

## Download / 下载

Download `release/HostPowerMonitor_Setup.exe` and double-click to install.

普通用户直接下载 `release/HostPowerMonitor_Setup.exe`，双击安装。

## Features / 功能

- Starts with Windows
- Real-time PC wattage display
- Daily and monthly energy usage tracking
- Draggable floating bubble
- Bubble can be disabled in settings
- Windows 10+ support
- Detects internal hardware automatically
- Uses direct readings where available, estimates unavailable internal components
- Counts host PC internal hardware only, not external devices

## Files / 文件说明

- `release/HostPowerMonitor_Setup.exe`: installer / 安装包
- `release/HostPowerMonitor.exe`: app executable / 主程序
- `src/`: app source code / 主程序源码
- `installer/`: installer source code / 安装器源码
- `tests/`: test utilities / 测试工具源码
