# Macro Redirection

作者：youshu

一个整合了鼠标宏重定向与技能图标显示功能的卫月插件，支持 IPC 通信接口。

## 功能特性

### 🎯 鼠标宏重定向
- 无需宏即可对鼠标悬停目标施放技能
- 支持多种目标类型优先级设置
- 支持按键修饰符（Shift/Ctrl/Alt等）
- 支持职业特定的技能重定向配置

### 🖼️ 技能图标显示
- 直观的技能图标显示界面
- 可自定义图标大小和显示样式
- 按职业分类显示技能

### 🔌 IPC 通信支持
- 提供 IPC 接口供其他插件（如 YouShu ACR）调用
- 可查询重定向的技能列表
- 可获取技能的目标优先级设置

## 支持的目标类型

- **UI Mouseover** - 列表鼠标悬停
- **Model Mouseover** - 场景模型鼠标悬停
- **Cursor** - 光标位置（仅地面技能）
- **Target** - 当前目标
- **Focus** - 焦点目标
- **Target of Target** - 目标的目标
- **Self** - 自己
- **Soft Target** - 软目标
- **<2> ~ <8>** - 队伍成员占位符

## 使用方法

### 基本设置

1. 使用命令 `/macroredirect` 或 `/mr` 打开配置窗口
2. 在左侧选择职业或职能技能
3. 为需要重定向的技能添加目标优先级
4. 插件会按照优先级顺序尝试施放技能

### 高级选项

#### 重定向选项
- **忽略范围和目标类型错误** - 不显示错误提示
- **启用范围检查** - 检查技能施放距离

#### 默认鼠标悬停行为
- **友方技能默认悬停** - 所有友方技能默认使用鼠标悬停
- **敌方技能默认悬停** - 所有敌方技能默认使用鼠标悬停
- **地面技能默认悬停** - 地面技能默认在鼠标位置释放
- **地面目标默认在光标位置** - 地面技能默认在光标位置

#### 队列选项
- **允许地面技能排队** - 地面技能可以进入技能队列
- **允许宏技能排队** - 从宏触发的技能可以排队

## IPC 接口

### 获取所有重定向的技能ID
```csharp
var ipc = pluginInterface.GetIpcSubscriber<uint[]>("MacroRedirection.RetargetedActions");
uint[] actionIds = ipc.InvokeFunc();
```

### 检查技能是否被重定向
```csharp
var ipc = pluginInterface.GetIpcSubscriber<uint, bool>("MacroRedirection.IsActionRetargeted");
bool isRetargeted = ipc.InvokeFunc(actionId);
```

### 获取技能的目标优先级列表
```csharp
var ipc = pluginInterface.GetIpcSubscriber<uint, string[]>("MacroRedirection.GetActionTargets");
string[] targets = ipc.InvokeFunc(actionId);
```

## 与 YouShu ACR 联动

本插件提供 IPC 接口，可以让 YouShu 的 ACR 获取重定向配置信息，实现更智能的技能施放逻辑。

### 在 YouShu ACR 中使用

```csharp
// 检查技能是否被重定向
var ipc = Services.Interface.GetIpcSubscriber<uint, bool>("MacroRedirection.IsActionRetargeted");
try
{
    if (ipc.InvokeFunc(actionId))
    {
        // 技能已被重定向，可以使用对应的目标逻辑
    }
}
catch
{
    // Macro Redirection 插件未安装或未启用
}
```

## 技术说明

### 项目结构
```
MacroRedirection/
├── Plugin.cs                    # 插件主入口
├── Configuration.cs             # 配置管理
├── Services.cs                  # 服务容器
├── Actions.cs                   # 技能数据管理
├── MacroRedirectionCore.cs      # 核心重定向逻辑
├── PluginUI.cs                  # 用户界面
├── IPCProvider.cs               # IPC 接口提供者
├── MacroRedirection.csproj      # 项目文件
└── MacroRedirection.json        # 插件清单
```

### 核心功能实现

#### 1. 技能重定向
通过 Hook 游戏的 `UseAction` 函数，在技能施放时拦截并重定向目标：
- 根据配置的优先级列表依次尝试目标
- 验证目标的合法性（范围、视线、类型）
- 支持地面技能的光标/目标位置施放

#### 2. 目标解析
使用游戏的 `PronounModule` 和 `TargetManager` 解析各类目标：
- UI 悬停目标：通过 `UiMouseOverTarget` 获取
- 模型悬停目标：通过 `MouseOverTarget` 获取
- 占位符目标：通过 `ResolvePlaceholder` 解析

#### 3. 配置持久化
使用 Dalamud 的配置系统保存设置：
- 每个技能可以有独立的重定向配置
- 支持职业特定配置
- 支持按键修饰符配置

## 编译说明

1. 确保安装了 .NET 10.0 SDK
2. 设置 `DALAMUD_HOME` 环境变量指向 Dalamud 开发文件夹
3. 使用以下命令编译：

```bash
dotnet build MacroRedirection.csproj -c Release
```

编译后的插件将输出到 `bin/Release` 目录。

## 依赖项

- Dalamud.CN.NET.Sdk (14.0.0)
- Newtonsoft.Json (13.0.4)
- .NET 10.0 Runtime

## 兼容性

- 游戏版本：适用于当前版本的最终幻想XIV（国服/国际服）
- Dalamud API Level: 13

## 更新日志

### v1.0.0 (2025-12-19)
- 初始版本发布
- 整合 MOActionPlugin 的重定向功能
- 整合 Redirect 的图标显示功能
- 添加 IPC 接口支持 YouShu ACR 联动

## 致谢

本插件参考了以下开源项目：
- [MOActionPlugin](https://github.com/kaedys/MOActionPlugin) - 鼠标悬停技能施放
- [Redirect](https://github.com/cairthenn/Redirect) - 技能目标重定向

特别感谢原作者的贡献！

## 许可证

本项目基于原项目的许可证进行分发。

## 联系方式

如有问题或建议，请通过以下方式联系：
- 作者：youshu
- 项目地址：D:\ACR\自创插件\Macro Redirection

---

**注意**: 使用本插件时请遵守游戏服务条款。
