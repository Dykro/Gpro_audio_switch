# G PRO Audio Switcher

一个轻量级的 Windows 系统托盘应用，用于自动切换罗技 G PRO X 2 无线耳机和扬声器之间的音频设备。

## 功能特点

- 🎧 **自动切换**：当 G PRO X 2 耳机开启/关闭时，自动切换播放和录音设备
- 📡 **HID++ 协议**：直接监控耳机接收器的 HID++ 协议，精确检测耳机开关状态
- 🔊 **快速切换**：一级菜单直接点击切换播放设备
- ⭐ **默认设备**：可设置耳机断开时自动切换回的默认设备
- 🌐 **多语言**：支持中文/英文界面，自动跟随系统语言
- 🚀 **零依赖**：纯 C# 实现，不依赖任何第三方 NuGet 包
- 💾 **开机自启**：可选开机自动运行
- 🔔 **通知提示**：可选气泡通知显示切换状态

## 系统要求

- Windows 10/11
- .NET 8.0 运行时
- Logitech G PRO X 2 无线耳机（使用 USB 接收器）
- （可选）Logitech G HUB（程序可独立工作，无需 G HUB）

## 安装

### 方式一：直接运行

1. 下载最新的 Release
2. 解压后运行 `GProAudioSwitcher.exe`

### 方式二：从源码编译

```bash
# 克隆仓库
git clone https://github.com/yourusername/Gpro_audio_switch.git
cd Gpro_audio_switch

# 编译
dotnet build -c Release

# 运行
dotnet run
```

### 发布单文件版本

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

## 使用说明

1. 运行程序后，会在系统托盘（通知区域）显示图标
2. **首次使用**：
   - 右键点击托盘图标
   - 点击 `⭐ 设置默认设备`，选择耳机断开时要切换到的设备（如扬声器）
3. 程序会自动检测 G PRO X 2 耳机的开关状态

### 托盘菜单功能

```
状态: HID++ 监控中
─────────────────
设备1                    ← 点击直接切换
✓ 设备2 ★                ← 当前播放设备 + 默认设备
设备3
─────────────────
⭐ 设置默认设备 >         ← 选择耳机断开时切换到的设备
🎤 录音设备 >             ← 切换录音设备
─────────────────
自动切换                  ← 开启/关闭自动切换功能
开机自启动
─────────────────
⚙️ 设置 >
    显示通知              ← 开启/关闭气泡通知
    🌐 语言 >            ← 选择界面语言
─────────────────
退出
```

- **播放设备列表**：直接点击即可切换到该设备
- **★ 标记**：表示该设备已被设为"默认设备"（耳机断开时会自动切换到此设备）
- **✓ 标记**：表示当前正在使用的播放设备

## 配置文件

配置文件位于：`%APPDATA%\GProAudioSwitcher\config.json`

```json
{
  "HeadsetKeyword": "PRO X 2",
  "SpeakersKeyword": "Realtek",
  "HeadsetMicKeyword": "PRO X 2",
  "DefaultPlaybackDeviceId": "...",
  "DefaultPlaybackDeviceName": "扬声器 (Realtek Audio)",
  "AutoSwitchOnConnect": true,
  "AutoSwitchOnDisconnect": true,
  "ShowNotifications": true,
  "StartWithWindows": false,
  "Language": "Auto"
}
```

### 配置说明

| 配置项 | 说明 |
|--------|------|
| `HeadsetKeyword` | 耳机设备名称关键字 |
| `SpeakersKeyword` | 扬声器设备名称关键字（向后兼容） |
| `DefaultPlaybackDeviceId` | 默认播放设备 ID（耳机断开时切换到） |
| `DefaultPlaybackDeviceName` | 默认播放设备名称 |
| `AutoSwitchOnConnect` | 耳机开启时自动切换 |
| `AutoSwitchOnDisconnect` | 耳机关闭时自动切换 |
| `ShowNotifications` | 显示气泡通知 |
| `StartWithWindows` | 开机自启动 |
| `Language` | 界面语言（Auto/English/Chinese） |

## 工作原理

### HID++ 协议监控

程序通过监控 G PRO X 2 USB 接收器的 HID++ 协议消息来检测耳机状态：

1. **设备识别**：查找 VID=0x046D, PID=0x0AF7 的 HID 设备（G PRO X 2 接收器）
2. **协议监听**：读取 Report ID 0x51 的 HID++ 消息
3. **状态解析**：解析消息中的连接状态字节
   - `51 05 00 03 00 00 01` = 耳机已连接
   - `51 05 00 03 00 00 00` = 耳机已断开
4. **音频切换**：使用 Windows Core Audio API 切换默认音频设备

### 音频设备切换

使用 Windows Core Audio API (COM Interop)：
- `IMMDeviceEnumerator`：枚举音频设备
- `IPolicyConfig`：设置默认音频设备（未公开接口）

## 故障排除

### 状态显示 "连接中..." 或 "Windows 设备监控"

- 确保 G PRO X 2 接收器已插入 USB 端口
- 如果 G HUB 正在运行，可能会独占 HID 设备访问权限
- 程序会自动回退到 Windows 设备监控模式

### 自动切换不工作

1. 检查 "自动切换" 选项是否已开启
2. 确认已设置 "默认设备"（⭐ 设置默认设备）
3. 检查耳机是否在播放设备列表中显示

### 找不到设备

- 确保设备在 Windows 声音设置中已启用
- 在托盘菜单中查看播放设备列表
- 确认设备未被其他程序独占

### 耳机断开后没有自动切换

- 点击 `⭐ 设置默认设备`，选择要切换到的目标设备
- 确保 "自动切换" 选项已开启

## 技术细节

- **语言**：C# (.NET 8.0)
- **UI**：Windows Forms (NotifyIcon)
- **音频 API**：Windows Core Audio API (COM Interop)
  - `IMMDeviceEnumerator`：枚举音频设备
  - `IPolicyConfig`：设置默认音频设备
- **HID 通信**：Windows HID API
  - `hid.dll`：HID 设备枚举和通信
  - `SetupAPI`：设备信息查询

## 许可证

AGPL-3.0 许可

## 更新日志

### v2.0 (2026-01)
- 🆕 使用 HID++ 协议直接监控耳机状态，不再依赖 G HUB
- 🆕 一级菜单快速切换播放设备
- 🆕 独立的"设置默认设备"子菜单
- 🆕 多语言支持（中文/英文）
- 🔧 简化菜单结构，移除调试选项
- 🔧 优化设备检测可靠性

### v1.0
- 初始版本，使用 G HUB WebSocket API

## 致谢

- Logitech HID++ 协议文档
- 参考了 [SoundSwitch](https://github.com/Belphemur/SoundSwitch) 的 IPolicyConfig 实现
