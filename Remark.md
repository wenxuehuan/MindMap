# SWDT 项目备注

## 项目概览

SWDT 是一个基于 Windows WPF 的思维导图编辑器，当前代码集中在 `SWDT/` 目录下。项目目标是提供一个轻量、可本地运行的桌面端思维导图工具，支持多文档、画布操作、节点编辑、连线样式、主题切换以及 JSON 文件保存与读取。

## 当前能力

- 多文档编辑：支持多个文档同时打开，并通过标签页切换、重排或分离。
- 思维导图编辑：支持节点新增、删除、选择、拖拽、自动布局和摘要节点。
- 画布交互：支持平移、缩放、适配全部内容、居中选中内容和框选。
- 样式配置：支持节点、连接线、颜色、主题等可视化配置。
- 文件持久化：使用 JSON 保存和加载导图数据，并保留对旧格式的兼容处理。
- 使用体验：包含撤销/重做、最近文件、亮色/暗色/系统主题等桌面应用常用功能。

## 代码结构

- `SWDT.slnx`：解决方案入口。
- `SWDT/SWDT.csproj`：WPF 应用项目文件，目标框架为 `net10.0-windows`。
- `SWDT/App.xaml` 与 `SWDT/App.xaml.cs`：应用启动和全局资源。
- `SWDT/MainWindow.xaml`：主窗口 UI、菜单、工具栏、标签页、画布和检查器布局。
- `SWDT/MainWindow.xaml.cs`：主交互逻辑，包括文档生命周期、渲染、输入、布局、序列化、撤销/重做和主题应用。
- `SWDT/MindMapNode.cs`、`MindMapConnection.cs`、`MindMapDocument.cs` 等：导图领域模型和运行时状态。
- `installer/`：安装与打包脚本。

## 开发备注

- 大部分运行时行为目前集中在 `MainWindow.xaml.cs`，新增较复杂功能时可以优先考虑提取到独立的普通 C# 类中，便于维护和测试。
- 保存/加载逻辑需要注意兼容旧版文件；新增持久化字段时，应考虑默认值、反序列化后的归一化以及父子关系重建。
- 用户可见的编辑操作通常需要遵循现有流程：推入撤销快照、标记文档为已修改、刷新画布、刷新检查器和更新命令状态。
- `SWDT/bin/`、`SWDT/obj/`、`.vs/`、`*.csproj.user` 和打包产物属于生成或本地环境文件，不应提交到版本库。

## 常用命令

```powershell
dotnet restore .\SWDT.slnx
dotnet build .\SWDT.slnx
dotnet run --project .\SWDT\SWDT.csproj
dotnet test .\SWDT.slnx
```

## 后续建议

- 增加 `.gitignore`，避免构建产物和本地 IDE 文件进入版本库。
- 为 JSON 兼容、导图布局、撤销/重做和数据归一化补充测试项目。
- 逐步拆分 `MainWindow.xaml.cs` 中的纯逻辑，降低 UI 代码和业务逻辑耦合。
- 在发布流程中明确 Debug、Release、安装包产物的生成位置和清理规则。
