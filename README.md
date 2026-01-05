# G PRO Audio Switcher

一个轻量级的 Windows 系统托盘应用，用于自动切换罗技 G PRO 2 耳机和扬声器之间的音频设备。

## 功能特点

- 🎧 **自动切换**：当 G PRO 2 耳机开启/关闭时，自动切换播放和录音设备
- 📡 **G HUB 集成**：通过监听 Logitech G HUB 的本地 WebSocket API 检测耳机状态
- 🔊 **手动切换**：通过系统托盘菜单手动选择音频设备
- ⚙️ **可配置**：支持自定义设备关键字匹配
- 🚀 **零依赖**：纯 C# 实现，不依赖任何第三方 NuGet 包
- 💾 **开机自启**：可选开机自动运行

## 系统要求

- Windows 10/11
- .NET 8.0 运行时
- Logitech G HUB（用于自动检测耳机状态）

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
   - 进入 `Settings` → `Configure Speakers Keyword...`
   - 输入你的扬声器设备名称中的关键字（如 "Realtek"）
3. 默认配置会自动识别包含 "PRO X 2" 的设备作为耳机

### 托盘菜单功能

- **Playback Devices**：查看和切换播放设备
- **Recording Devices**：查看和切换录音设备
- **Auto Switch**：启用/禁用自动切换
- **Start with Windows**：开机自启动
- **Settings**：
  - Show Notifications：显示/隐藏气泡通知
  - Debug Mode：开启调试日志
  - Configure Headset Keyword：设置耳机设备关键字
  - Configure Speakers Keyword：设置扬声器设备关键字
- **Reconnect G HUB**：重新连接 G HUB
- **Exit**：退出程序

## 配置文件

配置文件位于：`%APPDATA%\GProAudioSwitcher\config.json`

```json
{
  "HeadsetKeyword": "PRO X 2",
  "SpeakersKeyword": "Realtek",
  "HeadsetMicKeyword": "PRO X 2",
  "DefaultMicKeyword": "",
  "GHubWebSocketUrl": "ws://localhost:9010",
  "AutoSwitchOnConnect": true,
  "AutoSwitchOnDisconnect": true,
  "ShowNotifications": true,
  "DebugMode": false,
  "StartWithWindows": false
}
```

## 工作原理

1. **G HUB WebSocket 监听**：程序连接到 Logitech G HUB 本地运行的 WebSocket 服务 (`ws://localhost:9010`)
2. **设备状态检测**：解析 G HUB 发送的 JSON 消息，检测耳机的连接/断开状态
3. **音频切换**：使用 Windows Core Audio API (COM Interop) 切换默认音频设备

## 故障排除

### G HUB 显示 "Disconnected"

- 确保 Logitech G HUB 正在运行
- 检查 G HUB 是否正常工作
- 尝试点击 "Reconnect G HUB"

### 自动切换不工作

1. 开启 Debug Mode 查看日志
2. 检查 `%APPDATA%\GProAudioSwitcher\debug.log`
3. 确认 G HUB WebSocket 消息格式是否包含设备连接状态

### 找不到设备

- 检查设备关键字是否正确
- 在托盘菜单中查看 "Playback Devices" / "Recording Devices" 列表
- 确保设备在 Windows 声音设置中已启用

## 技术细节

- **语言**：C# (.NET 8.0)
- **UI**：Windows Forms (NotifyIcon)
- **音频 API**：Windows Core Audio API (COM Interop)
  - `IMMDeviceEnumerator`：枚举音频设备
  - `IPolicyConfig`：设置默认音频设备（未公开接口，但广泛使用）
- **网络**：`System.Net.WebSockets.ClientWebSocket`（.NET 内置）

## 许可证

MIT License

## 致谢

- 感谢社区对 G HUB WebSocket API 的逆向工程
- 参考了 [SoundSwitch](https://github.com/Belphemur/SoundSwitch) 的 IPolicyConfig 实现
