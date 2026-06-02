# Windows Power Monitor / 主机用电监控

Windows 10+ desktop app for real-time PC power monitoring, host hardware wattage estimation, and daily energy usage tracking.

Windows 10+ 主机硬件功耗监控软件。后台自动识别电脑内部配件，能实时读取的硬件优先实时读取，读取不到的内部硬件按配件功耗范围估算。只统计主机内部硬件，不统计显示器、音箱、充电器等外接设备。

[Download installer / 下载安装包](https://github.com/Source-blip/Windows-Power-Monitor/raw/main/release/HostPowerMonitor_Setup.exe)

![Floating wattage bubble](HostPowerMonitor_Bubble_Current.png)

![Main window preview](HostPowerMonitor_UI_Reference.png)

## Why Use It / 适合谁用

- Want a small Windows power monitor instead of a heavy hardware dashboard
- Want a floating real-time wattage bubble on the desktop
- Want to estimate daily PC electricity usage without buying a wall power meter
- Want a Windows 10+ tool focused on host PC internal hardware power

## Features / 功能

- Real-time whole-PC wattage display
- Draggable floating wattage bubble
- Bubble can be disabled in settings
- Starts with Windows
- Daily and monthly energy usage tracking
- Automatic internal hardware detection
- Direct readings where the hardware exposes usable power data
- Estimated power for internal parts that cannot expose direct readings
- Windows 10 and newer support
- Simple installer included

## Accuracy Note / 精度说明

This app estimates host PC hardware power. It is useful for trend tracking, rough electricity usage, and comparing idle/load power. It is not a replacement for a wall power meter.

这个软件算的是主机内部硬件的功耗估算值，适合看趋势、看大概用电量、对比待机和高负载变化。它不是插座电表，不能保证和墙插功率计完全一致。

## Download / 下载

For normal users:

1. Download [HostPowerMonitor_Setup.exe](https://github.com/Source-blip/Windows-Power-Monitor/raw/main/release/HostPowerMonitor_Setup.exe)
2. Double-click the installer
3. Open Windows Power Monitor from the desktop or Start Menu

普通用户：

1. 下载 [HostPowerMonitor_Setup.exe](https://github.com/Source-blip/Windows-Power-Monitor/raw/main/release/HostPowerMonitor_Setup.exe)
2. 双击安装
3. 从桌面或开始菜单打开软件

## Files / 文件说明

- `release/HostPowerMonitor_Setup.exe`: installer / 安装包
- `release/HostPowerMonitor.exe`: app executable / 主程序
- `src/`: app source code / 主程序源码
- `installer/`: installer source code / 安装器源码
- `tests/`: test utilities / 测试工具源码

## Search Keywords / 搜索关键词

Windows power monitor, PC wattage monitor, hardware power monitor, energy usage tracker, electricity usage monitor, real-time wattage bubble, Windows 10 power monitor, desktop power meter.

Windows 功耗监控，电脑功耗监控，主机用电统计，硬件功耗估算，实时功耗悬浮窗，电脑电量统计，Windows 用电监控。

## License / 开源协议

MIT License.
