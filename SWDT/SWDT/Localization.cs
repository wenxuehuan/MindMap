using System.Windows;

namespace SWDT;

public sealed record SupportedLanguage(string Code, string DisplayName);

public static class Localization
{
    public const string Chinese = "zh-CN";
    public const string English = "en-US";

    public static readonly SupportedLanguage[] SupportedLanguages =
    [
        new(Chinese, "简体中文"),
        new("zh-TW", "繁體中文"),
        new(English, "English"),
        new("ja-JP", "日本語"),
        new("ko-KR", "한국어"),
        new("fr-FR", "Français"),
        new("de-DE", "Deutsch"),
        new("es-ES", "Español"),
        new("it-IT", "Italiano"),
        new("pt-BR", "Português (Brasil)"),
        new("ru-RU", "Русский"),
        new("nl-NL", "Nederlands"),
        new("pl-PL", "Polski"),
        new("tr-TR", "Türkçe"),
        new("ar-SA", "العربية"),
        new("hi-IN", "हिन्दी"),
        new("vi-VN", "Tiếng Việt"),
        new("th-TH", "ไทย"),
        new("id-ID", "Bahasa Indonesia")
    ];

    private static readonly Dictionary<string, string> ChineseStrings = new()
    {
        ["AppTitle"] = "SWDT 思维导图",
        ["MenuFile"] = "文件(_F)",
        ["MenuEdit"] = "编辑(_E)",
        ["MenuView"] = "视图(_V)",
        ["MenuNode"] = "节点(_N)",
        ["New"] = "新建",
        ["Open"] = "打开...",
        ["RecentFiles"] = "最近打开的文件",
        ["Save"] = "保存 Ctrl+S",
        ["SaveAs"] = "另存为...",
        ["ExportImage"] = "导出图片...",
        ["ShowInExplorer"] = "在文件资源管理器中显示",
        ["CloseCurrentTab"] = "关闭当前标签",
        ["Exit"] = "退出",
        ["Undo"] = "撤销 Ctrl+Z",
        ["Redo"] = "重做 Ctrl+Y",
        ["DeleteNode"] = "删除节点 Delete",
        ["AutoLayout"] = "整理布局",
        ["ResetView"] = "重置视图",
        ["Theme"] = "主题",
        ["ThemeSystem"] = "跟随系统",
        ["ThemeLight"] = "亮色",
        ["ThemeDark"] = "暗色",
        ["Language"] = "语言",
        ["LanguageChinese"] = "简体中文",
        ["LanguageEnglish"] = "English",
        ["AddRoot"] = "添加根节点",
        ["AddChild"] = "添加子节点",
        ["AddChildShortcut"] = "添加子节点 Tab",
        ["AddSibling"] = "添加同级节点",
        ["AddSiblingShortcut"] = "添加同级节点 Enter",
        ["AddSummary"] = "添加合并节点",
        ["AddConnection"] = "添加连线",
        ["HideSidebar"] = "隐藏侧栏",
        ["ShowSidebar"] = "显示侧栏",
        ["Ready"] = "就绪",
        ["MindMapStructure"] = "导图结构",
        ["NodeProperties"] = "节点属性",
        ["RootLayout"] = "根节点布局",
        ["ConnectionProperties"] = "连线属性",
        ["CanvasProperties"] = "画板属性",
        ["Color"] = "颜色",
        ["Font"] = "字体",
        ["Appearance"] = "外观",
        ["Connector"] = "连接线",
        ["MindMapInfo"] = "导图信息",
        ["Title"] = "标题",
        ["LayoutDirection"] = "布局方向",
        ["DirectionRight"] = "向右展开",
        ["DirectionDown"] = "向下展开",
        ["DirectionUp"] = "向上展开",
        ["DirectionLeft"] = "向左展开",
        ["DirectionDownLeft"] = "向左下展开",
        ["DirectionDownRight"] = "向右下展开",
        ["DirectionRadial"] = "四周扩散",
        ["HorizontalGap"] = "横向间距",
        ["VerticalGap"] = "纵向间距",
        ["LineColor"] = "线条颜色",
        ["LineThickness"] = "线条粗细",
        ["LineStyle"] = "线型",
        ["LineRouting"] = "走线方式",
        ["Arrow"] = "箭头",
        ["Solid"] = "实线",
        ["Dash"] = "虚线",
        ["Dot"] = "点线",
        ["Curve"] = "曲线",
        ["Straight"] = "直线",
        ["Elbow"] = "折线",
        ["AngleBracket"] = "尖括号",
        ["SquareBracket"] = "中括号",
        ["ExpandNode"] = "展开节点",
        ["CollapseNode"] = "收缩节点",
        ["None"] = "无",
        ["ArrowStart"] = "左箭头",
        ["ArrowEnd"] = "右箭头",
        ["ArrowBoth"] = "双箭头",
        ["BackgroundColor"] = "背景色",
        ["ShowGrid"] = "显示网格",
        ["GridSize"] = "网格大小",
        ["GridColor"] = "网格颜色",
        ["SnapToGrid"] = "拖动时吸附到网格",
        ["FitAll"] = "适应全部",
        ["CenterSelected"] = "居中选中",
        ["FillColor"] = "填充色",
        ["BorderColor"] = "边框色",
        ["TextColor"] = "文字色",
        ["FontFamily"] = "字体",
        ["FontSize"] = "字号",
        ["FontWeight"] = "字重",
        ["FontStyle"] = "样式",
        ["TextAlignment"] = "文字对齐",
        ["Normal"] = "常规",
        ["SemiBold"] = "半粗",
        ["Bold"] = "加粗",
        ["StyleNormal"] = "正常",
        ["Italic"] = "斜体",
        ["AlignLeft"] = "左对齐",
        ["AlignCenter"] = "居中",
        ["AlignRight"] = "右对齐",
        ["Shape"] = "形状",
        ["RoundedRectangle"] = "圆角矩形",
        ["Rectangle"] = "矩形",
        ["Pill"] = "胶囊",
        ["BorderThickness"] = "边框粗细",
        ["CustomColor"] = "自定义颜色",
        ["Ok"] = "确定",
        ["Cancel"] = "取消",
        ["Browse"] = "浏览...",
        ["Export"] = "导出",
        ["OpenFailed"] = "打开失败",
        ["SaveFailed"] = "保存失败",
        ["ExportImageFailed"] = "导出图片失败",
        ["Explorer"] = "文件资源管理器",
        ["ExplorerFailed"] = "打开文件资源管理器失败",
        ["RecentFilesTitle"] = "最近文件",
        ["NoRecentFiles"] = "无最近文件",
        ["MindMapFileFilter"] = "SWDT 思维导图 (*.swdtmap)|*.swdtmap|旧版 SWDT 思维导图 (*.swdtmap.json)|*.swdtmap.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
        ["MindMapSaveFilter"] = "SWDT 思维导图 (*.swdtmap)|*.swdtmap|旧版 SWDT 思维导图 (*.swdtmap.json)|*.swdtmap.json|JSON 文件 (*.json)|*.json",
        ["ImageFilterPng"] = "PNG 图像",
        ["ImageFilterJpg"] = "JPEG 图像",
        ["ImageFilterBmp"] = "BMP 位图",
        ["ImageFilterGif"] = "GIF 图像",
        ["ImageFilterTiff"] = "TIFF 图像",
        ["DefaultRootTitle"] = "中心主题",
        ["DefaultChildResearch"] = "资料收集",
        ["DefaultChildPlan"] = "计划安排",
        ["DefaultChildDeliver"] = "交付成果",
        ["DefaultCanvasTitle"] = "画板",
        ["NewTopic"] = "新主题",
        ["Summary"] = "合并",
        ["Relationship"] = "关系",
        ["Untitled"] = "未命名",
        ["NodeCount"] = "节点数",
        ["SummaryNodeCount"] = "合并节点数",
        ["Depth"] = "层级深度",
        ["CurrentSelection"] = "当前选择",
        ["MultiSelectionCount"] = "多选数量",
        ["SelectionCanvas"] = "画板",
        ["SelectionConnection"] = "连线",
        ["Zoom"] = "缩放：{0:P0}",
        ["UndoDone"] = "已撤销。",
        ["RedoDone"] = "已重做。",
        ["SelectedNodes"] = "已选择 {0} 个节点。",
        ["MovingCanvas"] = "正在拖动画板。",
        ["CanvasPanEnded"] = "画板拖拽结束。",
        ["MovedNodes"] = "已移动 {0} 个节点。",
        ["MovedNode"] = "已移动节点。",
        ["MarqueeSelected"] = "已框选 {0} 个节点。",
        ["ZoomStatus"] = "缩放：{0:P0}",
        ["SelectConnectionStart"] = "请先选择连线的起点节点。",
        ["SelectConnectionTarget"] = "请选择“{0}”要连接到的目标节点。",
        ["SelectDifferentNodes"] = "请选择两个不同的节点。",
        ["ConnectionSourceMissing"] = "连线起点已不存在。",
        ["SelectAnotherTarget"] = "请选择另一个节点作为连线目标。",
        ["AddConnectionCanceled"] = "已取消添加连线。",
        ["DuplicateConnection"] = "这两个节点之间已存在同方向连线。",
        ["ConnectionAdded"] = "已添加“{0}”到“{1}”的连线。",
        ["SelectAtLeastTwoNodes"] = "请至少选择两个节点。",
        ["SummaryAdded"] = "已为 {0} 个节点添加合并节点。",
        ["ConnectionDeleted"] = "连线已删除。",
        ["SelectNodeToDelete"] = "请先选择要删除的节点。",
        ["KeepOneRoot"] = "至少需要保留一个根节点。",
        ["NewMapCreated"] = "已新建思维导图。",
        ["SwitchedToFile"] = "已切换到 {0}。",
        ["OpenedFile"] = "已打开 {0}。",
        ["SavedFile"] = "已保存 {0}。",
        ["ExportedImage"] = "已导出图片：{0}",
        ["NoNodesToExport"] = "当前导图没有可导出的节点。",
        ["CannotCalculateExportBounds"] = "无法计算导图导出范围。",
        ["InvalidMindMapFile"] = "所选文件不是有效的思维导图。",
        ["RecentFileMissing"] = "该文件不存在，已从最近文件中移除。",
        ["CurrentFileNotSaved"] = "当前文件尚未保存。",
        ["SaveChangesTitle"] = "保存更改",
        ["SaveChangesPrompt"] = "是否保存“{0}”的更改？",
        ["LayoutDone"] = "布局已整理。",
        ["ViewReset"] = "视图已重置。",
        ["ThemeLightChanged"] = "已切换到亮色主题。",
        ["ThemeDarkChanged"] = "已切换到暗色主题。",
        ["ThemeSystemChanged"] = "已切换到跟随系统主题。",
        ["FitAllDone"] = "已适应全部节点。",
        ["CenterSelectedDone"] = "已居中当前节点。",
        ["SidebarExpanded"] = "右侧功能栏已展开。",
        ["SidebarCollapsed"] = "右侧功能栏已收起。",
        ["SelectedNode"] = "已选择：{0}",
        ["SelectedCanvas"] = "已选择：画板",
        ["SelectedConnection"] = "已选择连线：{0}",
        ["SummarySuffix"] = "{0}（合并）",
        ["RootLayoutUpdated"] = "根节点布局已更新。",
        ["ExportFile"] = "文件",
        ["ExportFormat"] = "格式",
        ["ExportSize"] = "尺寸",
        ["ExportHint"] = "导出全部可见节点，保持比例。尺寸范围：64 到 16000 像素。",
        ["ExportPathRequired"] = "请选择导出文件路径。",
        ["ExportDirectoryMissing"] = "导出目录不存在。",
        ["ExportInvalidSize"] = "请输入有效的宽度和高度。",
        ["ExportSizeOutOfRange"] = "宽度和高度必须在 {0} 到 {1} 像素之间。",
        ["LanguageChanged"] = "语言已切换为简体中文。"
    };

    private static readonly Dictionary<string, string> EnglishStrings = new()
    {
        ["AppTitle"] = "SWDT Mind Map",
        ["MenuFile"] = "File(_F)",
        ["MenuEdit"] = "Edit(_E)",
        ["MenuView"] = "View(_V)",
        ["MenuNode"] = "Node(_N)",
        ["New"] = "New",
        ["Open"] = "Open...",
        ["RecentFiles"] = "Recent Files",
        ["Save"] = "Save Ctrl+S",
        ["SaveAs"] = "Save As...",
        ["ExportImage"] = "Export Image...",
        ["ShowInExplorer"] = "Show in File Explorer",
        ["CloseCurrentTab"] = "Close Current Tab",
        ["Exit"] = "Exit",
        ["Undo"] = "Undo Ctrl+Z",
        ["Redo"] = "Redo Ctrl+Y",
        ["DeleteNode"] = "Delete Node Delete",
        ["AutoLayout"] = "Auto Layout",
        ["ResetView"] = "Reset View",
        ["Theme"] = "Theme",
        ["ThemeSystem"] = "Use System Setting",
        ["ThemeLight"] = "Light",
        ["ThemeDark"] = "Dark",
        ["Language"] = "Language",
        ["LanguageChinese"] = "简体中文",
        ["LanguageEnglish"] = "English",
        ["AddRoot"] = "Add Root Node",
        ["AddChild"] = "Add Child Node",
        ["AddChildShortcut"] = "Add Child Node Tab",
        ["AddSibling"] = "Add Sibling Node",
        ["AddSiblingShortcut"] = "Add Sibling Node Enter",
        ["AddSummary"] = "Add Summary Node",
        ["AddConnection"] = "Add Connection",
        ["HideSidebar"] = "Hide Sidebar",
        ["ShowSidebar"] = "Show Sidebar",
        ["Ready"] = "Ready",
        ["MindMapStructure"] = "Map Structure",
        ["NodeProperties"] = "Node Properties",
        ["RootLayout"] = "Root Layout",
        ["ConnectionProperties"] = "Connection Properties",
        ["CanvasProperties"] = "Canvas Properties",
        ["Color"] = "Color",
        ["Font"] = "Font",
        ["Appearance"] = "Appearance",
        ["Connector"] = "Connector",
        ["MindMapInfo"] = "Map Info",
        ["Title"] = "Title",
        ["LayoutDirection"] = "Layout Direction",
        ["DirectionRight"] = "Right",
        ["DirectionDown"] = "Down",
        ["DirectionUp"] = "Up",
        ["DirectionLeft"] = "Left",
        ["DirectionDownLeft"] = "Down Left",
        ["DirectionDownRight"] = "Down Right",
        ["DirectionRadial"] = "Radial",
        ["HorizontalGap"] = "Horizontal Gap",
        ["VerticalGap"] = "Vertical Gap",
        ["LineColor"] = "Line Color",
        ["LineThickness"] = "Line Thickness",
        ["LineStyle"] = "Line Style",
        ["LineRouting"] = "Routing",
        ["Arrow"] = "Arrow",
        ["Solid"] = "Solid",
        ["Dash"] = "Dash",
        ["Dot"] = "Dot",
        ["Curve"] = "Curve",
        ["Straight"] = "Straight",
        ["Elbow"] = "Elbow",
        ["AngleBracket"] = "Angle Bracket",
        ["SquareBracket"] = "Square Bracket",
        ["ExpandNode"] = "Expand Node",
        ["CollapseNode"] = "Collapse Node",
        ["None"] = "None",
        ["ArrowStart"] = "Start Arrow",
        ["ArrowEnd"] = "End Arrow",
        ["ArrowBoth"] = "Both Arrows",
        ["BackgroundColor"] = "Background Color",
        ["ShowGrid"] = "Show Grid",
        ["GridSize"] = "Grid Size",
        ["GridColor"] = "Grid Color",
        ["SnapToGrid"] = "Snap to Grid While Dragging",
        ["FitAll"] = "Fit All",
        ["CenterSelected"] = "Center Selected",
        ["FillColor"] = "Fill Color",
        ["BorderColor"] = "Border Color",
        ["TextColor"] = "Text Color",
        ["FontFamily"] = "Font",
        ["FontSize"] = "Font Size",
        ["FontWeight"] = "Weight",
        ["FontStyle"] = "Style",
        ["TextAlignment"] = "Text Alignment",
        ["Normal"] = "Regular",
        ["SemiBold"] = "Semi Bold",
        ["Bold"] = "Bold",
        ["StyleNormal"] = "Normal",
        ["Italic"] = "Italic",
        ["AlignLeft"] = "Left",
        ["AlignCenter"] = "Center",
        ["AlignRight"] = "Right",
        ["Shape"] = "Shape",
        ["RoundedRectangle"] = "Rounded Rectangle",
        ["Rectangle"] = "Rectangle",
        ["Pill"] = "Pill",
        ["BorderThickness"] = "Border Thickness",
        ["CustomColor"] = "Custom Color",
        ["Ok"] = "OK",
        ["Cancel"] = "Cancel",
        ["Browse"] = "Browse...",
        ["Export"] = "Export",
        ["OpenFailed"] = "Open Failed",
        ["SaveFailed"] = "Save Failed",
        ["ExportImageFailed"] = "Export Image Failed",
        ["Explorer"] = "File Explorer",
        ["ExplorerFailed"] = "Failed to Open File Explorer",
        ["RecentFilesTitle"] = "Recent Files",
        ["NoRecentFiles"] = "No Recent Files",
        ["MindMapFileFilter"] = "SWDT Mind Map (*.swdtmap)|*.swdtmap|Legacy SWDT Mind Map (*.swdtmap.json)|*.swdtmap.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
        ["MindMapSaveFilter"] = "SWDT Mind Map (*.swdtmap)|*.swdtmap|Legacy SWDT Mind Map (*.swdtmap.json)|*.swdtmap.json|JSON Files (*.json)|*.json",
        ["ImageFilterPng"] = "PNG Image",
        ["ImageFilterJpg"] = "JPEG Image",
        ["ImageFilterBmp"] = "BMP Bitmap",
        ["ImageFilterGif"] = "GIF Image",
        ["ImageFilterTiff"] = "TIFF Image",
        ["DefaultRootTitle"] = "Central Topic",
        ["DefaultChildResearch"] = "Research",
        ["DefaultChildPlan"] = "Planning",
        ["DefaultChildDeliver"] = "Deliverables",
        ["DefaultCanvasTitle"] = "Canvas",
        ["NewTopic"] = "New Topic",
        ["Summary"] = "Summary",
        ["Relationship"] = "Relationship",
        ["Untitled"] = "Untitled",
        ["NodeCount"] = "Nodes",
        ["SummaryNodeCount"] = "Summary Nodes",
        ["Depth"] = "Depth",
        ["CurrentSelection"] = "Current Selection",
        ["MultiSelectionCount"] = "Multi-selection Count",
        ["SelectionCanvas"] = "Canvas",
        ["SelectionConnection"] = "Connection",
        ["Zoom"] = "Zoom: {0:P0}",
        ["UndoDone"] = "Undo complete.",
        ["RedoDone"] = "Redo complete.",
        ["SelectedNodes"] = "{0} nodes selected.",
        ["MovingCanvas"] = "Panning canvas.",
        ["CanvasPanEnded"] = "Canvas pan ended.",
        ["MovedNodes"] = "Moved {0} nodes.",
        ["MovedNode"] = "Moved node.",
        ["MarqueeSelected"] = "{0} nodes selected by marquee.",
        ["ZoomStatus"] = "Zoom: {0:P0}",
        ["SelectConnectionStart"] = "Select a source node first.",
        ["SelectConnectionTarget"] = "Select the target node to connect from \"{0}\".",
        ["SelectDifferentNodes"] = "Select two different nodes.",
        ["ConnectionSourceMissing"] = "The connection source no longer exists.",
        ["SelectAnotherTarget"] = "Select another node as the connection target.",
        ["AddConnectionCanceled"] = "Add connection canceled.",
        ["DuplicateConnection"] = "A connection already exists in this direction.",
        ["ConnectionAdded"] = "Added a connection from \"{0}\" to \"{1}\".",
        ["SelectAtLeastTwoNodes"] = "Select at least two nodes.",
        ["SummaryAdded"] = "Added a summary node for {0} nodes.",
        ["ConnectionDeleted"] = "Connection deleted.",
        ["SelectNodeToDelete"] = "Select a node to delete first.",
        ["KeepOneRoot"] = "At least one root node must remain.",
        ["NewMapCreated"] = "Created a new mind map.",
        ["SwitchedToFile"] = "Switched to {0}.",
        ["OpenedFile"] = "Opened {0}.",
        ["SavedFile"] = "Saved {0}.",
        ["ExportedImage"] = "Exported image: {0}",
        ["NoNodesToExport"] = "There are no nodes to export.",
        ["CannotCalculateExportBounds"] = "Cannot calculate the export bounds.",
        ["InvalidMindMapFile"] = "The selected file is not a valid mind map.",
        ["RecentFileMissing"] = "The file does not exist and was removed from recent files.",
        ["CurrentFileNotSaved"] = "The current file has not been saved.",
        ["SaveChangesTitle"] = "Save Changes",
        ["SaveChangesPrompt"] = "Save changes to \"{0}\"?",
        ["LayoutDone"] = "Layout updated.",
        ["ViewReset"] = "View reset.",
        ["ThemeLightChanged"] = "Switched to light theme.",
        ["ThemeDarkChanged"] = "Switched to dark theme.",
        ["ThemeSystemChanged"] = "Switched to system theme.",
        ["FitAllDone"] = "Fit all nodes.",
        ["CenterSelectedDone"] = "Centered the selected node.",
        ["SidebarExpanded"] = "Right sidebar expanded.",
        ["SidebarCollapsed"] = "Right sidebar collapsed.",
        ["SelectedNode"] = "Selected: {0}",
        ["SelectedCanvas"] = "Selected: Canvas",
        ["SelectedConnection"] = "Selected connection: {0}",
        ["SummarySuffix"] = "{0} (Summary)",
        ["RootLayoutUpdated"] = "Root layout updated.",
        ["ExportFile"] = "File",
        ["ExportFormat"] = "Format",
        ["ExportSize"] = "Size",
        ["ExportHint"] = "Export all visible nodes and keep the aspect ratio. Size range: 64 to 16000 pixels.",
        ["ExportPathRequired"] = "Choose an export file path.",
        ["ExportDirectoryMissing"] = "The export directory does not exist.",
        ["ExportInvalidSize"] = "Enter a valid width and height.",
        ["ExportSizeOutOfRange"] = "Width and height must be between {0} and {1} pixels.",
        ["LanguageChanged"] = "Language changed to English."
    };

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> AdditionalStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-TW"] = Dict(
            ("AppTitle", "SWDT 心智圖"), ("MenuFile", "檔案(_F)"), ("MenuEdit", "編輯(_E)"), ("MenuView", "檢視(_V)"), ("MenuNode", "節點(_N)"),
            ("New", "新增"), ("Open", "開啟..."), ("RecentFiles", "最近開啟的檔案"), ("Save", "儲存 Ctrl+S"), ("SaveAs", "另存新檔..."), ("ExportImage", "匯出圖片..."), ("ShowInExplorer", "在檔案總管中顯示"), ("CloseCurrentTab", "關閉目前分頁"), ("Exit", "結束"),
            ("Undo", "復原 Ctrl+Z"), ("Redo", "重做 Ctrl+Y"), ("DeleteNode", "刪除節點 Delete"), ("AutoLayout", "整理版面"), ("ResetView", "重設檢視"), ("Theme", "主題"), ("ThemeSystem", "跟隨系統"), ("ThemeLight", "淺色"), ("ThemeDark", "深色"), ("Language", "語言"),
            ("AddRoot", "新增根節點"), ("AddChild", "新增子節點"), ("AddSibling", "新增同層節點"), ("AddSummary", "新增摘要節點"), ("AddConnection", "新增連線"), ("Ready", "就緒"), ("MindMapStructure", "導圖結構"), ("NodeProperties", "節點屬性"), ("CanvasProperties", "畫布屬性"),
            ("Color", "顏色"), ("Font", "字型"), ("Appearance", "外觀"), ("Title", "標題"), ("Ok", "確定"), ("Cancel", "取消"), ("Browse", "瀏覽..."), ("Export", "匯出"), ("DefaultRootTitle", "中心主題"), ("NewTopic", "新主題"), ("Summary", "摘要"), ("Relationship", "關係"), ("Untitled", "未命名"), ("LanguageChanged", "語言已切換為繁體中文。")),

        ["ja-JP"] = Dict(
            ("AppTitle", "SWDT マインドマップ"), ("MenuFile", "ファイル(_F)"), ("MenuEdit", "編集(_E)"), ("MenuView", "表示(_V)"), ("MenuNode", "ノード(_N)"),
            ("New", "新規"), ("Open", "開く..."), ("RecentFiles", "最近使ったファイル"), ("Save", "保存 Ctrl+S"), ("SaveAs", "名前を付けて保存..."), ("ExportImage", "画像を書き出し..."), ("ShowInExplorer", "エクスプローラーで表示"), ("CloseCurrentTab", "現在のタブを閉じる"), ("Exit", "終了"),
            ("Undo", "元に戻す Ctrl+Z"), ("Redo", "やり直し Ctrl+Y"), ("DeleteNode", "ノードを削除 Delete"), ("AutoLayout", "自動レイアウト"), ("ResetView", "表示をリセット"), ("Theme", "テーマ"), ("ThemeSystem", "システム設定に従う"), ("ThemeLight", "ライト"), ("ThemeDark", "ダーク"), ("Language", "言語"),
            ("AddRoot", "ルートノードを追加"), ("AddChild", "子ノードを追加"), ("AddChildShortcut", "子ノードを追加 Tab"), ("AddSibling", "兄弟ノードを追加"), ("AddSiblingShortcut", "兄弟ノードを追加 Enter"), ("AddSummary", "要約ノードを追加"), ("AddConnection", "接続を追加"),
            ("HideSidebar", "サイドバーを隠す"), ("ShowSidebar", "サイドバーを表示"), ("Ready", "準備完了"), ("MindMapStructure", "マップ構造"), ("NodeProperties", "ノードのプロパティ"), ("RootLayout", "ルートレイアウト"), ("ConnectionProperties", "接続のプロパティ"), ("CanvasProperties", "キャンバスのプロパティ"), ("Color", "色"), ("Font", "フォント"), ("Appearance", "外観"), ("Connector", "コネクタ"), ("MindMapInfo", "マップ情報"),
            ("Title", "タイトル"), ("LayoutDirection", "レイアウト方向"), ("DirectionRight", "右"), ("DirectionDown", "下"), ("DirectionUp", "上"), ("DirectionLeft", "左"), ("DirectionDownLeft", "左下"), ("DirectionDownRight", "右下"), ("DirectionRadial", "放射状"),
            ("HorizontalGap", "横間隔"), ("VerticalGap", "縦間隔"), ("LineColor", "線の色"), ("LineThickness", "線の太さ"), ("LineStyle", "線種"), ("LineRouting", "配線"), ("Arrow", "矢印"), ("Solid", "実線"), ("Dash", "破線"), ("Dot", "点線"), ("Curve", "曲線"), ("Straight", "直線"), ("Elbow", "折れ線"), ("None", "なし"),
            ("BackgroundColor", "背景色"), ("ShowGrid", "グリッドを表示"), ("GridSize", "グリッドサイズ"), ("GridColor", "グリッド色"), ("SnapToGrid", "ドラッグ時にグリッドへ吸着"), ("FitAll", "全体表示"), ("CenterSelected", "選択を中央へ"), ("FillColor", "塗りつぶし"), ("BorderColor", "枠線色"), ("TextColor", "文字色"),
            ("FontFamily", "フォント"), ("FontSize", "フォントサイズ"), ("FontWeight", "太さ"), ("FontStyle", "スタイル"), ("TextAlignment", "文字揃え"), ("Normal", "標準"), ("SemiBold", "中太"), ("Bold", "太字"), ("StyleNormal", "標準"), ("Italic", "斜体"), ("AlignLeft", "左揃え"), ("AlignCenter", "中央"), ("AlignRight", "右揃え"),
            ("Shape", "形状"), ("RoundedRectangle", "角丸四角形"), ("Rectangle", "四角形"), ("Pill", "ピル"), ("BorderThickness", "枠線の太さ"), ("CustomColor", "カスタム色"), ("Ok", "OK"), ("Cancel", "キャンセル"), ("Browse", "参照..."), ("Export", "書き出し"),
            ("DefaultRootTitle", "中心トピック"), ("DefaultChildResearch", "情報収集"), ("DefaultChildPlan", "計画"), ("DefaultChildDeliver", "成果物"), ("DefaultCanvasTitle", "キャンバス"), ("NewTopic", "新しいトピック"), ("Summary", "要約"), ("Relationship", "関係"), ("Untitled", "無題"), ("LanguageChanged", "言語を日本語に切り替えました。")),

        ["ko-KR"] = Dict(
            ("AppTitle", "SWDT 마인드맵"), ("MenuFile", "파일(_F)"), ("MenuEdit", "편집(_E)"), ("MenuView", "보기(_V)"), ("MenuNode", "노드(_N)"),
            ("New", "새로 만들기"), ("Open", "열기..."), ("RecentFiles", "최근 파일"), ("Save", "저장 Ctrl+S"), ("SaveAs", "다른 이름으로 저장..."), ("ExportImage", "이미지 내보내기..."), ("ShowInExplorer", "파일 탐색기에서 표시"), ("CloseCurrentTab", "현재 탭 닫기"), ("Exit", "종료"),
            ("Undo", "실행 취소 Ctrl+Z"), ("Redo", "다시 실행 Ctrl+Y"), ("DeleteNode", "노드 삭제 Delete"), ("AutoLayout", "자동 정렬"), ("ResetView", "보기 초기화"), ("Theme", "테마"), ("ThemeSystem", "시스템 설정 사용"), ("ThemeLight", "밝게"), ("ThemeDark", "어둡게"), ("Language", "언어"),
            ("AddRoot", "루트 노드 추가"), ("AddChild", "자식 노드 추가"), ("AddChildShortcut", "자식 노드 추가 Tab"), ("AddSibling", "형제 노드 추가"), ("AddSiblingShortcut", "형제 노드 추가 Enter"), ("AddSummary", "요약 노드 추가"), ("AddConnection", "연결 추가"),
            ("HideSidebar", "사이드바 숨기기"), ("ShowSidebar", "사이드바 표시"), ("Ready", "준비됨"), ("MindMapStructure", "맵 구조"), ("NodeProperties", "노드 속성"), ("RootLayout", "루트 레이아웃"), ("ConnectionProperties", "연결 속성"), ("CanvasProperties", "캔버스 속성"), ("Color", "색상"), ("Font", "글꼴"), ("Appearance", "모양"), ("Connector", "커넥터"), ("MindMapInfo", "맵 정보"),
            ("Title", "제목"), ("LayoutDirection", "레이아웃 방향"), ("DirectionRight", "오른쪽"), ("DirectionDown", "아래"), ("DirectionUp", "위"), ("DirectionLeft", "왼쪽"), ("DirectionRadial", "방사형"), ("HorizontalGap", "가로 간격"), ("VerticalGap", "세로 간격"),
            ("LineColor", "선 색상"), ("LineThickness", "선 두께"), ("LineStyle", "선 스타일"), ("LineRouting", "경로"), ("Arrow", "화살표"), ("Solid", "실선"), ("Dash", "파선"), ("Dot", "점선"), ("Curve", "곡선"), ("Straight", "직선"), ("Elbow", "꺾은선"), ("None", "없음"),
            ("BackgroundColor", "배경색"), ("ShowGrid", "격자 표시"), ("GridSize", "격자 크기"), ("GridColor", "격자 색상"), ("SnapToGrid", "드래그 시 격자에 맞춤"), ("FitAll", "전체 맞춤"), ("CenterSelected", "선택 항목 가운데"), ("FillColor", "채우기 색"), ("BorderColor", "테두리 색"), ("TextColor", "텍스트 색"),
            ("FontFamily", "글꼴"), ("FontSize", "글꼴 크기"), ("FontWeight", "굵기"), ("FontStyle", "스타일"), ("TextAlignment", "텍스트 정렬"), ("Normal", "보통"), ("SemiBold", "중간 굵게"), ("Bold", "굵게"), ("Italic", "기울임"), ("AlignLeft", "왼쪽"), ("AlignCenter", "가운데"), ("AlignRight", "오른쪽"),
            ("Shape", "모양"), ("RoundedRectangle", "둥근 사각형"), ("Rectangle", "사각형"), ("Pill", "알약형"), ("BorderThickness", "테두리 두께"), ("CustomColor", "사용자 색상"), ("Ok", "확인"), ("Cancel", "취소"), ("Browse", "찾아보기..."), ("Export", "내보내기"),
            ("DefaultRootTitle", "중심 주제"), ("DefaultChildResearch", "자료 수집"), ("DefaultChildPlan", "계획"), ("DefaultChildDeliver", "결과물"), ("DefaultCanvasTitle", "캔버스"), ("NewTopic", "새 주제"), ("Summary", "요약"), ("Relationship", "관계"), ("Untitled", "제목 없음"), ("LanguageChanged", "언어가 한국어로 변경되었습니다.")),

        ["fr-FR"] = Dict(("AppTitle", "Carte mentale SWDT"), ("MenuFile", "Fichier(_F)"), ("MenuEdit", "Modifier(_E)"), ("MenuView", "Affichage(_V)"), ("MenuNode", "Noeud(_N)"), ("New", "Nouveau"), ("Open", "Ouvrir..."), ("RecentFiles", "Fichiers recents"), ("Save", "Enregistrer Ctrl+S"), ("SaveAs", "Enregistrer sous..."), ("ExportImage", "Exporter l'image..."), ("ShowInExplorer", "Afficher dans l'Explorateur"), ("CloseCurrentTab", "Fermer l'onglet"), ("Exit", "Quitter"), ("Undo", "Annuler Ctrl+Z"), ("Redo", "Retablir Ctrl+Y"), ("DeleteNode", "Supprimer le noeud Delete"), ("AutoLayout", "Reorganiser"), ("ResetView", "Reinitialiser la vue"), ("Theme", "Theme"), ("ThemeSystem", "Systeme"), ("ThemeLight", "Clair"), ("ThemeDark", "Sombre"), ("Language", "Langue"), ("AddRoot", "Ajouter un noeud racine"), ("AddChild", "Ajouter un enfant"), ("AddSibling", "Ajouter un noeud voisin"), ("AddSummary", "Ajouter un resume"), ("AddConnection", "Ajouter une connexion"), ("HideSidebar", "Masquer le panneau"), ("ShowSidebar", "Afficher le panneau"), ("Ready", "Pret"), ("MindMapStructure", "Structure"), ("NodeProperties", "Proprietes du noeud"), ("RootLayout", "Disposition racine"), ("ConnectionProperties", "Proprietes de connexion"), ("CanvasProperties", "Proprietes du canevas"), ("Color", "Couleur"), ("Font", "Police"), ("Appearance", "Apparence"), ("Connector", "Connecteur"), ("MindMapInfo", "Infos"), ("Title", "Titre"), ("LayoutDirection", "Direction"), ("LineColor", "Couleur de ligne"), ("LineThickness", "Epaisseur"), ("LineStyle", "Style de ligne"), ("Arrow", "Fleche"), ("BackgroundColor", "Couleur d'arriere-plan"), ("ShowGrid", "Afficher la grille"), ("GridSize", "Taille de grille"), ("GridColor", "Couleur de grille"), ("SnapToGrid", "Aligner sur la grille"), ("FitAll", "Tout ajuster"), ("CenterSelected", "Centrer la selection"), ("FillColor", "Remplissage"), ("BorderColor", "Bordure"), ("TextColor", "Texte"), ("CustomColor", "Couleur personnalisee"), ("Ok", "OK"), ("Cancel", "Annuler"), ("Browse", "Parcourir..."), ("Export", "Exporter"), ("DefaultRootTitle", "Sujet central"), ("NewTopic", "Nouveau sujet"), ("Summary", "Resume"), ("Relationship", "Relation"), ("Untitled", "Sans titre"), ("LanguageChanged", "Langue changee en francais.")),

        ["de-DE"] = Dict(("AppTitle", "SWDT Mindmap"), ("MenuFile", "Datei(_F)"), ("MenuEdit", "Bearbeiten(_E)"), ("MenuView", "Ansicht(_V)"), ("MenuNode", "Knoten(_N)"), ("New", "Neu"), ("Open", "Offnen..."), ("RecentFiles", "Zuletzt verwendete Dateien"), ("Save", "Speichern Ctrl+S"), ("SaveAs", "Speichern unter..."), ("ExportImage", "Bild exportieren..."), ("ShowInExplorer", "Im Explorer anzeigen"), ("CloseCurrentTab", "Aktuellen Tab schliessen"), ("Exit", "Beenden"), ("Undo", "Ruckgangig Ctrl+Z"), ("Redo", "Wiederholen Ctrl+Y"), ("DeleteNode", "Knoten loschen Delete"), ("AutoLayout", "Layout anordnen"), ("ResetView", "Ansicht zurucksetzen"), ("Theme", "Design"), ("ThemeSystem", "System"), ("ThemeLight", "Hell"), ("ThemeDark", "Dunkel"), ("Language", "Sprache"), ("AddRoot", "Wurzelknoten hinzufugen"), ("AddChild", "Kindknoten hinzufugen"), ("AddSibling", "Geschwisterknoten hinzufugen"), ("AddSummary", "Zusammenfassung hinzufugen"), ("AddConnection", "Verbindung hinzufugen"), ("HideSidebar", "Seitenleiste ausblenden"), ("ShowSidebar", "Seitenleiste anzeigen"), ("Ready", "Bereit"), ("MindMapStructure", "Struktur"), ("NodeProperties", "Knoteneigenschaften"), ("RootLayout", "Wurzellayout"), ("ConnectionProperties", "Verbindungseigenschaften"), ("CanvasProperties", "Canvas-Eigenschaften"), ("Color", "Farbe"), ("Font", "Schrift"), ("Appearance", "Darstellung"), ("Connector", "Verbinder"), ("MindMapInfo", "Info"), ("Title", "Titel"), ("LineColor", "Linienfarbe"), ("LineThickness", "Linienstarke"), ("LineStyle", "Linienstil"), ("BackgroundColor", "Hintergrundfarbe"), ("ShowGrid", "Raster anzeigen"), ("FitAll", "Alles einpassen"), ("CenterSelected", "Auswahl zentrieren"), ("Ok", "OK"), ("Cancel", "Abbrechen"), ("Browse", "Durchsuchen..."), ("Export", "Exportieren"), ("DefaultRootTitle", "Zentrales Thema"), ("NewTopic", "Neues Thema"), ("Summary", "Zusammenfassung"), ("Relationship", "Beziehung"), ("Untitled", "Unbenannt"), ("LanguageChanged", "Sprache auf Deutsch umgestellt.")),

        ["es-ES"] = Dict(("AppTitle", "Mapa mental SWDT"), ("MenuFile", "Archivo(_F)"), ("MenuEdit", "Editar(_E)"), ("MenuView", "Vista(_V)"), ("MenuNode", "Nodo(_N)"), ("New", "Nuevo"), ("Open", "Abrir..."), ("RecentFiles", "Archivos recientes"), ("Save", "Guardar Ctrl+S"), ("SaveAs", "Guardar como..."), ("ExportImage", "Exportar imagen..."), ("ShowInExplorer", "Mostrar en el Explorador"), ("CloseCurrentTab", "Cerrar pestana actual"), ("Exit", "Salir"), ("Undo", "Deshacer Ctrl+Z"), ("Redo", "Rehacer Ctrl+Y"), ("DeleteNode", "Eliminar nodo Delete"), ("AutoLayout", "Ordenar diseno"), ("ResetView", "Restablecer vista"), ("Theme", "Tema"), ("ThemeSystem", "Sistema"), ("ThemeLight", "Claro"), ("ThemeDark", "Oscuro"), ("Language", "Idioma"), ("AddRoot", "Agregar nodo raiz"), ("AddChild", "Agregar nodo hijo"), ("AddSibling", "Agregar nodo hermano"), ("AddSummary", "Agregar resumen"), ("AddConnection", "Agregar conexion"), ("HideSidebar", "Ocultar barra lateral"), ("ShowSidebar", "Mostrar barra lateral"), ("Ready", "Listo"), ("MindMapStructure", "Estructura"), ("NodeProperties", "Propiedades del nodo"), ("RootLayout", "Diseno raiz"), ("ConnectionProperties", "Propiedades de conexion"), ("CanvasProperties", "Propiedades del lienzo"), ("Color", "Color"), ("Font", "Fuente"), ("Appearance", "Apariencia"), ("Connector", "Conector"), ("MindMapInfo", "Informacion"), ("Title", "Titulo"), ("LineColor", "Color de linea"), ("LineThickness", "Grosor"), ("LineStyle", "Estilo de linea"), ("BackgroundColor", "Color de fondo"), ("ShowGrid", "Mostrar cuadricula"), ("FitAll", "Ajustar todo"), ("CenterSelected", "Centrar seleccion"), ("Ok", "Aceptar"), ("Cancel", "Cancelar"), ("Browse", "Examinar..."), ("Export", "Exportar"), ("DefaultRootTitle", "Tema central"), ("NewTopic", "Nuevo tema"), ("Summary", "Resumen"), ("Relationship", "Relacion"), ("Untitled", "Sin titulo"), ("LanguageChanged", "Idioma cambiado a espanol.")),

        ["it-IT"] = Dict(("AppTitle", "Mappa mentale SWDT"), ("MenuFile", "File(_F)"), ("MenuEdit", "Modifica(_E)"), ("MenuView", "Visualizza(_V)"), ("MenuNode", "Nodo(_N)"), ("New", "Nuovo"), ("Open", "Apri..."), ("RecentFiles", "File recenti"), ("Save", "Salva Ctrl+S"), ("SaveAs", "Salva con nome..."), ("ExportImage", "Esporta immagine..."), ("ShowInExplorer", "Mostra in Esplora file"), ("CloseCurrentTab", "Chiudi scheda corrente"), ("Exit", "Esci"), ("Undo", "Annulla Ctrl+Z"), ("Redo", "Ripeti Ctrl+Y"), ("DeleteNode", "Elimina nodo Delete"), ("AutoLayout", "Disponi layout"), ("ResetView", "Reimposta vista"), ("Theme", "Tema"), ("ThemeSystem", "Sistema"), ("ThemeLight", "Chiaro"), ("ThemeDark", "Scuro"), ("Language", "Lingua"), ("AddRoot", "Aggiungi nodo radice"), ("AddChild", "Aggiungi nodo figlio"), ("AddSibling", "Aggiungi nodo fratello"), ("AddSummary", "Aggiungi riepilogo"), ("AddConnection", "Aggiungi connessione"), ("Ready", "Pronto"), ("Color", "Colore"), ("Font", "Carattere"), ("Appearance", "Aspetto"), ("Title", "Titolo"), ("Ok", "OK"), ("Cancel", "Annulla"), ("Browse", "Sfoglia..."), ("Export", "Esporta"), ("NewTopic", "Nuovo argomento"), ("Summary", "Riepilogo"), ("Relationship", "Relazione"), ("Untitled", "Senza titolo"), ("LanguageChanged", "Lingua cambiata in italiano.")),

        ["pt-BR"] = Dict(("AppTitle", "Mapa mental SWDT"), ("MenuFile", "Arquivo(_F)"), ("MenuEdit", "Editar(_E)"), ("MenuView", "Exibir(_V)"), ("MenuNode", "No(_N)"), ("New", "Novo"), ("Open", "Abrir..."), ("RecentFiles", "Arquivos recentes"), ("Save", "Salvar Ctrl+S"), ("SaveAs", "Salvar como..."), ("ExportImage", "Exportar imagem..."), ("ShowInExplorer", "Mostrar no Explorador"), ("CloseCurrentTab", "Fechar aba atual"), ("Exit", "Sair"), ("Undo", "Desfazer Ctrl+Z"), ("Redo", "Refazer Ctrl+Y"), ("DeleteNode", "Excluir no Delete"), ("AutoLayout", "Organizar layout"), ("ResetView", "Redefinir visualizacao"), ("Theme", "Tema"), ("ThemeSystem", "Sistema"), ("ThemeLight", "Claro"), ("ThemeDark", "Escuro"), ("Language", "Idioma"), ("AddRoot", "Adicionar no raiz"), ("AddChild", "Adicionar filho"), ("AddSibling", "Adicionar irmao"), ("AddSummary", "Adicionar resumo"), ("AddConnection", "Adicionar conexao"), ("Ready", "Pronto"), ("Color", "Cor"), ("Font", "Fonte"), ("Appearance", "Aparencia"), ("Title", "Titulo"), ("Ok", "OK"), ("Cancel", "Cancelar"), ("Browse", "Procurar..."), ("Export", "Exportar"), ("NewTopic", "Novo topico"), ("Summary", "Resumo"), ("Relationship", "Relacao"), ("Untitled", "Sem titulo"), ("LanguageChanged", "Idioma alterado para portugues.")),

        ["ru-RU"] = Dict(("AppTitle", "SWDT интеллект-карта"), ("MenuFile", "Файл(_F)"), ("MenuEdit", "Правка(_E)"), ("MenuView", "Вид(_V)"), ("MenuNode", "Узел(_N)"), ("New", "Создать"), ("Open", "Открыть..."), ("RecentFiles", "Недавние файлы"), ("Save", "Сохранить Ctrl+S"), ("SaveAs", "Сохранить как..."), ("ExportImage", "Экспорт изображения..."), ("ShowInExplorer", "Показать в проводнике"), ("CloseCurrentTab", "Закрыть текущую вкладку"), ("Exit", "Выход"), ("Undo", "Отменить Ctrl+Z"), ("Redo", "Повторить Ctrl+Y"), ("DeleteNode", "Удалить узел Delete"), ("AutoLayout", "Упорядочить"), ("ResetView", "Сбросить вид"), ("Theme", "Тема"), ("ThemeSystem", "Системная"), ("ThemeLight", "Светлая"), ("ThemeDark", "Темная"), ("Language", "Язык"), ("AddRoot", "Добавить корневой узел"), ("AddChild", "Добавить дочерний узел"), ("AddSibling", "Добавить соседний узел"), ("AddSummary", "Добавить сводный узел"), ("AddConnection", "Добавить связь"), ("Ready", "Готово"), ("Color", "Цвет"), ("Font", "Шрифт"), ("Appearance", "Вид"), ("Title", "Заголовок"), ("Ok", "OK"), ("Cancel", "Отмена"), ("Browse", "Обзор..."), ("Export", "Экспорт"), ("NewTopic", "Новая тема"), ("Summary", "Сводка"), ("Relationship", "Связь"), ("Untitled", "Без имени"), ("LanguageChanged", "Язык изменен на русский.")),

        ["nl-NL"] = Dict(("AppTitle", "SWDT mindmap"), ("MenuFile", "Bestand(_F)"), ("MenuEdit", "Bewerken(_E)"), ("MenuView", "Beeld(_V)"), ("MenuNode", "Knooppunt(_N)"), ("New", "Nieuw"), ("Open", "Openen..."), ("Save", "Opslaan Ctrl+S"), ("SaveAs", "Opslaan als..."), ("ExportImage", "Afbeelding exporteren..."), ("Exit", "Afsluiten"), ("Undo", "Ongedaan maken Ctrl+Z"), ("Redo", "Opnieuw Ctrl+Y"), ("Theme", "Thema"), ("Language", "Taal"), ("Ready", "Gereed"), ("Color", "Kleur"), ("Font", "Lettertype"), ("Title", "Titel"), ("Ok", "OK"), ("Cancel", "Annuleren"), ("NewTopic", "Nieuw onderwerp"), ("Summary", "Samenvatting"), ("Relationship", "Relatie"), ("Untitled", "Naamloos"), ("LanguageChanged", "Taal gewijzigd naar Nederlands.")),
        ["pl-PL"] = Dict(("AppTitle", "Mapa mysli SWDT"), ("MenuFile", "Plik(_F)"), ("MenuEdit", "Edycja(_E)"), ("MenuView", "Widok(_V)"), ("MenuNode", "Wezel(_N)"), ("New", "Nowy"), ("Open", "Otworz..."), ("Save", "Zapisz Ctrl+S"), ("SaveAs", "Zapisz jako..."), ("ExportImage", "Eksportuj obraz..."), ("Exit", "Zakoncz"), ("Undo", "Cofnij Ctrl+Z"), ("Redo", "Ponow Ctrl+Y"), ("Theme", "Motyw"), ("Language", "Jezyk"), ("Ready", "Gotowe"), ("Color", "Kolor"), ("Font", "Czcionka"), ("Title", "Tytul"), ("Ok", "OK"), ("Cancel", "Anuluj"), ("NewTopic", "Nowy temat"), ("Summary", "Podsumowanie"), ("Relationship", "Relacja"), ("Untitled", "Bez tytulu"), ("LanguageChanged", "Zmieniono jezyk na polski.")),
        ["tr-TR"] = Dict(("AppTitle", "SWDT zihin haritasi"), ("MenuFile", "Dosya(_F)"), ("MenuEdit", "Duzenle(_E)"), ("MenuView", "Gorunum(_V)"), ("MenuNode", "Dugum(_N)"), ("New", "Yeni"), ("Open", "Ac..."), ("Save", "Kaydet Ctrl+S"), ("SaveAs", "Farkli kaydet..."), ("ExportImage", "Gorsel disa aktar..."), ("Exit", "Cikis"), ("Undo", "Geri al Ctrl+Z"), ("Redo", "Yinele Ctrl+Y"), ("Theme", "Tema"), ("Language", "Dil"), ("Ready", "Hazir"), ("Color", "Renk"), ("Font", "Yazi tipi"), ("Title", "Baslik"), ("Ok", "Tamam"), ("Cancel", "Iptal"), ("NewTopic", "Yeni konu"), ("Summary", "Ozet"), ("Relationship", "Iliski"), ("Untitled", "Adsiz"), ("LanguageChanged", "Dil Turkce olarak degistirildi.")),
        ["ar-SA"] = Dict(("AppTitle", "خريطة ذهنية SWDT"), ("MenuFile", "ملف(_F)"), ("MenuEdit", "تحرير(_E)"), ("MenuView", "عرض(_V)"), ("MenuNode", "عقدة(_N)"), ("New", "جديد"), ("Open", "فتح..."), ("Save", "حفظ Ctrl+S"), ("SaveAs", "حفظ باسم..."), ("ExportImage", "تصدير صورة..."), ("Exit", "خروج"), ("Undo", "تراجع Ctrl+Z"), ("Redo", "إعادة Ctrl+Y"), ("Theme", "السمة"), ("Language", "اللغة"), ("Ready", "جاهز"), ("Color", "اللون"), ("Font", "الخط"), ("Title", "العنوان"), ("Ok", "موافق"), ("Cancel", "إلغاء"), ("NewTopic", "موضوع جديد"), ("Summary", "ملخص"), ("Relationship", "علاقة"), ("Untitled", "بلا عنوان"), ("LanguageChanged", "تم تغيير اللغة إلى العربية.")),
        ["hi-IN"] = Dict(("AppTitle", "SWDT माइंड मैप"), ("MenuFile", "फ़ाइल(_F)"), ("MenuEdit", "संपादित करें(_E)"), ("MenuView", "दृश्य(_V)"), ("MenuNode", "नोड(_N)"), ("New", "नया"), ("Open", "खोलें..."), ("Save", "सहेजें Ctrl+S"), ("SaveAs", "इस रूप में सहेजें..."), ("ExportImage", "छवि निर्यात करें..."), ("Exit", "बाहर निकलें"), ("Undo", "पूर्ववत Ctrl+Z"), ("Redo", "दोहराएँ Ctrl+Y"), ("Theme", "थीम"), ("Language", "भाषा"), ("Ready", "तैयार"), ("Color", "रंग"), ("Font", "फ़ॉन्ट"), ("Title", "शीर्षक"), ("Ok", "ठीक"), ("Cancel", "रद्द करें"), ("NewTopic", "नया विषय"), ("Summary", "सारांश"), ("Relationship", "संबंध"), ("Untitled", "शीर्षकहीन"), ("LanguageChanged", "भाषा हिन्दी में बदल दी गई है।")),
        ["vi-VN"] = Dict(("AppTitle", "So do tu duy SWDT"), ("MenuFile", "Tep(_F)"), ("MenuEdit", "Chinh sua(_E)"), ("MenuView", "Xem(_V)"), ("MenuNode", "Nut(_N)"), ("New", "Moi"), ("Open", "Mo..."), ("Save", "Luu Ctrl+S"), ("SaveAs", "Luu thanh..."), ("ExportImage", "Xuat anh..."), ("Exit", "Thoat"), ("Undo", "Hoan tac Ctrl+Z"), ("Redo", "Lam lai Ctrl+Y"), ("Theme", "Giao dien"), ("Language", "Ngon ngu"), ("Ready", "San sang"), ("Color", "Mau"), ("Font", "Phong chu"), ("Title", "Tieu de"), ("Ok", "OK"), ("Cancel", "Huy"), ("NewTopic", "Chu de moi"), ("Summary", "Tom tat"), ("Relationship", "Quan he"), ("Untitled", "Chua dat ten"), ("LanguageChanged", "Da chuyen ngon ngu sang tieng Viet.")),
        ["th-TH"] = Dict(("AppTitle", "แผนที่ความคิด SWDT"), ("MenuFile", "ไฟล์(_F)"), ("MenuEdit", "แก้ไข(_E)"), ("MenuView", "มุมมอง(_V)"), ("MenuNode", "โหนด(_N)"), ("New", "ใหม่"), ("Open", "เปิด..."), ("Save", "บันทึก Ctrl+S"), ("SaveAs", "บันทึกเป็น..."), ("ExportImage", "ส่งออกรูปภาพ..."), ("Exit", "ออก"), ("Undo", "เลิกทำ Ctrl+Z"), ("Redo", "ทำซ้ำ Ctrl+Y"), ("Theme", "ธีม"), ("Language", "ภาษา"), ("Ready", "พร้อม"), ("Color", "สี"), ("Font", "แบบอักษร"), ("Title", "ชื่อ"), ("Ok", "ตกลง"), ("Cancel", "ยกเลิก"), ("NewTopic", "หัวข้อใหม่"), ("Summary", "สรุป"), ("Relationship", "ความสัมพันธ์"), ("Untitled", "ไม่มีชื่อ"), ("LanguageChanged", "เปลี่ยนภาษาเป็นไทยแล้ว")),
        ["id-ID"] = Dict(("AppTitle", "Peta pikiran SWDT"), ("MenuFile", "File(_F)"), ("MenuEdit", "Edit(_E)"), ("MenuView", "Tampilan(_V)"), ("MenuNode", "Node(_N)"), ("New", "Baru"), ("Open", "Buka..."), ("Save", "Simpan Ctrl+S"), ("SaveAs", "Simpan sebagai..."), ("ExportImage", "Ekspor gambar..."), ("Exit", "Keluar"), ("Undo", "Urungkan Ctrl+Z"), ("Redo", "Ulangi Ctrl+Y"), ("Theme", "Tema"), ("Language", "Bahasa"), ("Ready", "Siap"), ("Color", "Warna"), ("Font", "Font"), ("Title", "Judul"), ("Ok", "OK"), ("Cancel", "Batal"), ("NewTopic", "Topik baru"), ("Summary", "Ringkasan"), ("Relationship", "Relasi"), ("Untitled", "Tanpa judul"), ("LanguageChanged", "Bahasa diubah ke Bahasa Indonesia."))
    };

    public static string NormalizeLanguage(string? language)
    {
        foreach (SupportedLanguage supportedLanguage in SupportedLanguages)
        {
            if (string.Equals(supportedLanguage.Code, language, StringComparison.OrdinalIgnoreCase))
            {
                return supportedLanguage.Code;
            }
        }

        return Chinese;
    }

    public static void Apply(string? language)
    {
        string normalized = NormalizeLanguage(language);
        Application.Current.Resources["CurrentLanguage"] = normalized;

        foreach ((string key, string value) in EnglishStrings)
        {
            Application.Current.Resources[key] = value;
        }

        foreach ((string key, string value) in GetLanguageStrings(normalized))
        {
            Application.Current.Resources[key] = value;
        }
    }

    public static string T(string key)
    {
        string language = NormalizeLanguage(Application.Current.Resources["CurrentLanguage"] as string);
        IReadOnlyDictionary<string, string> strings = GetLanguageStrings(language);
        if (strings.TryGetValue(key, out string? value))
        {
            return value;
        }

        return EnglishStrings.TryGetValue(key, out string? englishValue) ? englishValue : key;
    }

    private static IReadOnlyDictionary<string, string> GetLanguageStrings(string? language)
    {
        string normalized = NormalizeLanguage(language);
        if (normalized == Chinese)
        {
            return ChineseStrings;
        }

        if (normalized == English)
        {
            return EnglishStrings;
        }

        return AdditionalStrings.TryGetValue(normalized, out IReadOnlyDictionary<string, string>? strings)
            ? strings
            : EnglishStrings;
    }

    private static IReadOnlyDictionary<string, string> Dict(params (string Key, string Value)[] items)
    {
        Dictionary<string, string> dictionary = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string value) in items)
        {
            dictionary[key] = value;
        }

        return dictionary;
    }
}
