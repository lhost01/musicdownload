# MusicBox - 网易云音乐工具

## 📋 更新日志

### 2026.04.19 更新

#### 歌单识别逻辑升级

- 新增更严格的歌单识别机制，避免把无关目录误识别为歌单
- 仅识别程序歌单标记，并结合目录结构判断是否为真实歌单
- 旧数据只会在明显像真实歌单时迁移，减少历史脏路径干扰

#### 界面与交互优化

- 更新了导航栏 UI，整体层次和视觉风格更统一
- 修复了播放进度条不同步、无法拖动的问题

#### 更新日志提示

- 新增"下次不再提示"选项，勾选后本次更新日志将不再自动弹出
- 后续如果有新的更新日志版本，仍会继续提醒

***

<p align="center">
  <img src="favicon.ico" width="128" height="128" alt="MusicBox Logo">
</p>

<p align="center">
  <a href="#"><img src="https://img.shields.io/badge/.NET-9.0-blue?style=flat-square&logo=.net" alt=".NET 9.0"></a>
  <a href="#"><img src="https://img.shields.io/badge/WPF-Application-orange?style=flat-square&logo=windows" alt="WPF"></a>
  <a href="#"><img src="https://img.shields.io/badge/MVVM-Architecture-green?style=flat-square" alt="MVVM"></a>
  <a href="#"><img src="https://img.shields.io/badge/License-MIT-yellow?style=flat-square" alt="License"></a>
</p>

<p align="center">
  <b>一款精美的网易云音乐 NCM 解密、下载和播放工具</b><br>
  <i>采用 macOS 风格设计，支持歌单管理和音乐播放</i>
</p>

***

## ✨ 功能特性

### 🎵 音乐播放

- **内置音乐播放器**：支持 MP3、FLAC、WAV 格式播放
- **播放控制**：播放/暂停、上一首、下一首
- **进度条拖动**：可自由拖动调整播放位置
- **自动播放**：歌曲播放完成后自动播放下一首
- **实时进度**：200ms 刷新频率，流畅跟随播放进度

### 📁 歌单管理

- **创建歌单**：支持自定义名称和封面
- **歌单详情页**：在程序内打开歌单，显示完整歌曲列表
- **歌曲信息显示**：显示歌曲名、艺术家、专辑、时长、文件大小
- **更改存储位置**：歌单可存储在任意盘的任意文件夹
- **导入歌曲**：支持添加本地音乐文件到歌单
- **删除歌曲**：支持从歌单中删除歌曲文件

### 🔄 NCM 文件解密转换

- **单文件模式**：选择单个 NCM 文件进行解密
- **批量文件夹模式**：扫描整个文件夹中的所有 NCM 文件
- **多文件选择模式**：同时选择多个 NCM 文件
- **支持输出格式**：MP3、FLAC、WAV
- **实时进度显示**：带进度条和百分比显示
- **导入歌单**：转换完成后可直接导入到现有或新建歌单

### ⬇️ 音乐下载功能

- **ID 下载**：通过网易云音乐 ID 下载歌曲
- **批量下载**：支持多任务同时下载
- **实时进度**：显示下载进度和状态

### 🎨 精美 UI 设计

- **macOS 风格**：无边框窗口、红黄绿控制按钮
- **圆角设计**：现代化的圆角卡片和按钮
- **毛玻璃效果**：半透明背景和柔和阴影
- **动画效果**：
  - 按钮点击动画
  - 列表项悬停效果
  - 进度条平滑动画
  - 页面切换效果
- **响应式布局**：自适应窗口大小

***

## 📸 界面预览

### 主界面

- 左侧导航栏：NCM 转换、ID 下载、我的歌单
- macOS 风格标题栏：红黄绿三色控制按钮
- 卡片式内容区域：清晰的功能分区

### 歌单详情页

- 顶部导航：返回按钮、歌单标题、操作按钮
- 歌曲列表：显示完整歌曲信息
- 播放按钮：每首歌旁都有播放按钮

### 音乐播放器

- 固定在底部
- 显示当前播放歌曲信息
- 播放控制按钮和进度条

***

## 🛠️ 技术架构

### 项目结构

```
网易云音乐下载/
├── Commands/
│   └── RelayCommand.cs              # MVVM 命令实现
├── Converters/
│   ├── BoolToBackgroundConverter.cs # 布尔值转背景色
│   ├── BoolToPlayPauseConverter.cs  # 播放/暂停图标转换
│   ├── InputModeConverter.cs        # 输入模式转换
│   ├── IntToBoolConverter.cs        # 整数转布尔值
│   ├── IntToVisibilityConverter.cs  # 整数转可见性
│   ├── InverseBoolConverter.cs      # 布尔值反转
│   ├── InverseBoolToVisibilityConverter.cs # 布尔值反转可见性
│   └── TimeSpanToSecondsConverter.cs # 时间转秒数
├── Models/
│   ├── DownloadTaskInfo.cs          # 下载任务模型
│   ├── InputMode.cs                 # 输入模式枚举
│   ├── NcmFileInfo.cs               # NCM 文件信息模型
│   ├── PlaylistInfo.cs              # 歌单信息模型
│   └── PlaylistSongInfo.cs          # 歌单歌曲信息模型
├── Services/
│   ├── AudioConverterService.cs     # NCM 解密核心服务
│   ├── MusicPlayerService.cs        # 音乐播放服务
│   └── NeteaseDownloadService.cs    # 音乐下载服务
├── ViewModels/
│   ├── MainViewModel.cs             # 主视图模型
│   └── ViewModelBase.cs             # 视图模型基类
├── MainWindow.xaml                  # 主窗口 UI
├── MainWindow.xaml.cs
├── App.xaml                         # 应用程序资源
└── App.xaml.cs
```

### 核心技术

- **框架**：.NET Framework 4.7.2
- **UI 框架**：WPF (Windows Presentation Foundation)
- **设计模式**：MVVM (Model-View-ViewModel)
- **加密算法**：
  - AES-128-ECB
  - 变种 RC4
- **音频播放**：WPF MediaPlayer
- **元数据读取**：Windows Shell32 API
- **异步编程**：async/await + CancellationToken

### NCM 解密原理

1. **文件头验证**：检查 "CTENFDAM" 魔数
2. **密钥解密**：
   - XOR 0x64 预处理
   - AES-128-ECB 解密（密钥：hzHRAmso5kInbaxW）
   - 去除 "neteasecloudmusic" 前缀
3. **音频解密**：使用网易云变种 RC4 算法
4. **元数据提取**：解析 JSON 格式的歌曲信息

***

## 📋 系统要求

- Windows 7 SP1 或更高版本
- .NET Framework 4.7.2 或更高版本
- Visual Studio 2019 或更高版本（用于开发）

***

## 🚀 使用方法

### 1. NCM 文件转换

1. 启动应用程序
2. 选择输入模式：
   - **单文件**：选择单个 .ncm 文件
   - **批量文件夹**：扫描文件夹中的所有 NCM 文件
   - **多文件**：同时选择多个 NCM 文件
3. 选择输出格式（MP3/FLAC/WAV）
4. 选择输出文件夹
5. 点击"开始转换"
6. 转换完成后可选择导入歌单

### 2. 音乐播放

1. 切换到"我的歌单"标签页
2. 创建或选择一个歌单
3. 点击歌单旁的 **▶** 按钮进入详情页
4. 点击歌曲旁的播放按钮开始播放
5. 使用底部播放器控制播放

### 3. 歌单管理

1. 创建新歌单：输入名称，选择封面
2. 添加歌曲：点击"添加歌曲"按钮选择音乐文件
3. 更改目录：点击 **📂** 按钮选择新的存储位置
4. 删除歌曲：点击歌曲旁的 **🗑** 按钮

### 4. 音乐下载

1. 切换到"ID 下载"标签页
2. 输入网易云音乐歌曲 ID
3. 点击"开始下载"

***

## 🔧 编译说明

### 使用 Visual Studio

1. 克隆仓库

```bash
git clone https://github.com/lhost01/musicdownload.git
```

1. 使用 Visual Studio 2019+ 打开 `网易云音乐下载.sln`
2. 按 `F5` 或点击"开始调试"

### 使用命令行

```bash
# 进入项目目录
cd MusicBox

# 编译项目
msbuild 网易云音乐下载.sln /p:Configuration=Release /p:Platform="Any CPU"
```

编译完成后，可执行文件位于：

```
bin\Release\网易云音乐下载.exe
```

***

## 📁 文件格式说明

### NCM 文件结构

```
[8 字节] 魔数 "CTENFDAM"
[2 字节] 版本号
[4 字节] 加密密钥长度
[N 字节] 加密密钥数据
[4 字节] 元数据长度
[M 字节] 元数据（JSON 格式）
[5 字节] CRC32
[剩余] 加密音频数据
```

***

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建您的特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交您的更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开一个 Pull Request

***

## ⚠️ 免责声明

本项目仅供学习研究使用，请勿用于商业用途。使用本项目产生的任何后果由使用者自行承担。

本项目与网易云音乐官方无关，仅为第三方工具。

***

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE) 开源。

```
MIT License

Copyright (c) 2024 MusicBox

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

***

## 🙏 致谢

- 感谢网易云音乐提供的音乐服务
- 感谢所有贡献者和用户的支持

***

<p align="center">
  <b>Made with ❤️ by MusicBox Team</b>
</p>
