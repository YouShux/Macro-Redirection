# Macro Redirection - 项目概览

## 项目信息

- **项目名称**: Macro Redirection
- **作者**: youshu
- **版本**: 1.0.0
- **创建日期**: 2025-12-19
- **框架**: Dalamud Plugin (.NET 10.0)

## 项目结构

```
Macro Redirection/
│
├── MacroRedirection/                 # 主项目目录
│   ├── Plugin.cs                     # 插件主入口 (57 行)
│   ├── Configuration.cs              # 配置类定义 (44 行)
│   ├── Services.cs                   # 服务容器 (19 行)
│   ├── Actions.cs                    # 技能数据管理 (66 行)
│   ├── MacroRedirectionCore.cs       # 核心重定向逻辑 (197 行)
│   ├── PluginUI.cs                   # 用户界面 (267 行)
│   ├── IPCProvider.cs                # IPC 接口提供者 (64 行)
│   ├── MacroRedirection.csproj       # 项目文件
│   └── MacroRedirection.json         # 插件清单
│
├── MacroRedirection.sln              # Visual Studio 解决方案文件
├── README.md                         # 项目说明文档
├── USAGE.md                          # 使用说明文档
├── LICENSE                           # MIT 许可证
└── .gitignore                        # Git 忽略文件配置

总计: 8 个 C# 源文件，约 714 行代码
```

## 核心功能模块

### 1. Plugin.cs
- **职责**: 插件生命周期管理
- **功能**:
  - 插件初始化和清理
  - 命令注册 (`/macroredirect`, `/mr`)
  - 服务依赖注入
  - IPC 接口注册

### 2. Configuration.cs
- **职责**: 配置数据结构和持久化
- **功能**:
  - 重定向条目定义 (`RedirectionEntry`)
  - 全局设置存储
  - 配置保存和加载

### 3. Services.cs
- **职责**: Dalamud 服务容器
- **功能**:
  - 依赖注入服务注册
  - 提供全局服务访问点
  - 包含 11 个核心服务

### 4. Actions.cs
- **职责**: 技能数据管理
- **功能**:
  - 从游戏数据表加载技能信息
  - 按职业分类技能
  - 职能技能管理
  - 异步初始化

### 5. MacroRedirectionCore.cs
- **职责**: 核心重定向逻辑
- **功能**:
  - Hook 游戏的 `UseAction` 函数
  - 目标解析和验证
  - 重定向规则匹配
  - 地面技能处理
  - 修饰键支持

### 6. PluginUI.cs
- **职责**: 用户界面
- **功能**:
  - ImGui 界面绘制
  - 职业/技能选择
  - 重定向配置编辑
  - 选项菜单
  - 技能图标显示

### 7. IPCProvider.cs
- **职责**: IPC 接口提供
- **功能**:
  - 注册 IPC 调用网关
  - 提供 3 个公共接口：
    - `MacroRedirection.RetargetedActions`
    - `MacroRedirection.IsActionRetargeted`
    - `MacroRedirection.GetActionTargets`

## 技术特性

### 安全特性
- ✅ Unsafe 代码块用于底层 Hook
- ✅ 异常处理和错误日志
- ✅ 配置验证和默认值
- ✅ Hook 失败的优雅降级

### 性能优化
- ✅ 异步技能数据加载
- ✅ 缓存技能查询结果
- ✅ 最小化 Hook 开销
- ✅ 延迟配置保存

### 用户体验
- ✅ 中文界面
- ✅ 直观的技能图标
- ✅ 丰富的配置选项
- ✅ 详细的错误提示
- ✅ 热键快捷访问

## IPC 接口规范

### 接口 1: 获取重定向技能列表
```csharp
IPC Name: "MacroRedirection.RetargetedActions"
Return Type: uint[]
Description: 返回所有配置了重定向的技能 ID 数组
```

### 接口 2: 检查技能是否重定向
```csharp
IPC Name: "MacroRedirection.IsActionRetargeted"
Parameter: uint actionId
Return Type: bool
Description: 检查指定技能是否配置了重定向
```

### 接口 3: 获取技能目标列表
```csharp
IPC Name: "MacroRedirection.GetActionTargets"
Parameter: uint actionId
Return Type: string[]
Description: 获取指定技能的目标优先级列表
```

## 依赖关系

```
Plugin
  ├── Services (静态服务容器)
  ├── Configuration (配置管理)
  ├── Actions (技能数据)
  ├── MacroRedirectionCore (核心功能)
  │     ├── Actions
  │     ├── Configuration
  │     └── Services
  ├── PluginUI (用户界面)
  │     ├── Actions
  │     ├── Configuration
  │     └── Services
  └── IPCProvider (IPC 接口)
        ├── Plugin
        └── Configuration
```

## 编译和部署

### 开发环境要求
- Visual Studio 2022 或 JetBrains Rider
- .NET 10.0 SDK
- Dalamud CN SDK 14.0.0

### 编译步骤
```bash
# 1. 克隆/下载项目
cd "D:\ACR\自创插件\Macro Redirection"

# 2. 还原 NuGet 包
dotnet restore MacroRedirection/MacroRedirection.csproj

# 3. 编译项目
dotnet build MacroRedirection/MacroRedirection.csproj -c Release

# 4. 输出位置
# bin/Release/net10.0-windows/MacroRedirection.dll
```

### 部署步骤
```bash
# 复制到 Dalamud 插件目录
copy "bin\Release\net10.0-windows\*" "%APPDATA%\XIVLauncher\devPlugins\MacroRedirection\"
```

## 测试清单

### 基础功能测试
- [ ] 插件正常加载和卸载
- [ ] 配置窗口正常打开
- [ ] 命令 `/macroredirect` 和 `/mr` 工作
- [ ] 配置保存和加载

### 重定向功能测试
- [ ] UI 悬停目标重定向
- [ ] 模型悬停目标重定向
- [ ] 地面技能光标位置
- [ ] 修饰键功能
- [ ] 目标优先级顺序
- [ ] 范围检查
- [ ] 错误提示

### UI 功能测试
- [ ] 职业选择
- [ ] 技能搜索
- [ ] 图标显示
- [ ] 配置编辑
- [ ] 选项菜单

### IPC 功能测试
- [ ] IPC 接口注册
- [ ] YouShu ACR 调用测试
- [ ] 接口返回值正确性

## 已知限制

1. **PvP 技能不支持**: 当前版本不支持 PvP 技能重定向
2. **某些特殊技能**: 部分游戏内置的特殊机制技能可能无法重定向
3. **性能考虑**: 每个技能建议不超过 4 个目标优先级
4. **插件冲突**: 与其他鼠标悬停插件可能存在冲突

## 未来改进计划

- [ ] 添加配置导入/导出功能
- [ ] 支持配置模板和预设
- [ ] 添加技能冷却时间显示
- [ ] 优化大型配置的性能
- [ ] 添加调试模式和详细日志
- [ ] 支持更多的目标类型
- [ ] 添加配置备份和恢复

## 参考资源

### 原始项目
- [MOActionPlugin](https://github.com/kaedys/MOActionPlugin)
- [Redirect](https://github.com/cairthenn/Redirect)

### 开发文档
- [Dalamud Plugin Development Guide](https://dalamud.dev/)
- [FFXIVClientStructs Documentation](https://github.com/aers/FFXIVClientStructs)
- [Lumina Documentation](https://github.com/NotAdam/Lumina)

### 社区资源
- [Dalamud Discord](https://discord.gg/3NMcUV5)
- [FFXIV 卫月中文社区](https://github.com/ottercorp/Dalamud.CN)

## 维护日志

### v1.0.0 (2025-12-19)
- ✅ 初始版本创建
- ✅ 实现核心重定向功能
- ✅ 实现 UI 配置界面
- ✅ 添加 IPC 接口支持
- ✅ 完成文档编写

---

**项目状态**: ✅ 开发完成，待测试

**下一步**: 编译测试 → 游戏内验证 → 与 YouShu ACR 联动测试
