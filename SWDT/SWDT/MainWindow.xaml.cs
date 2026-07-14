using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace SWDT;

public partial class MainWindow : Window
{
    private const double MinNodeWidth = 96;
    private const double MaxNodeWidth = 280;
    private const double MinNodeHeight = 42;
    private const double HorizontalGap = 110;
    private const double ParentChildGap = 70;
    private const double VerticalGap = 12;
    private const double NodeHorizontalPadding = 22;
    private const double NodeVerticalPadding = 18;
    private const double NodeFontSize = 14;
    private const double NodeVisibilityMargin = 24;
    private const string DocumentDragFormat = "SWDT.MindMapDocument";
    private const int MaxRecentFiles = 10;
    private const int MaxHistoryEntries = 100;
    private const double ExportPadding = 48;
    private const int MinExportPixelSize = 64;
    private const int MaxExportPixelSize = 16000;
    private const string ThemeSystem = "System";
    private const string ThemeLight = "Light";
    private const string ThemeDark = "Dark";

    private static readonly List<MainWindow> OpenWindows = [];
    private static readonly string SettingsDirectory = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SWDT");
    private static readonly string SettingsPath = IOPath.Combine(SettingsDirectory, "settings.json");
    private static AppSettings AppSettings = LoadAppSettings();
    private static int UntitledCounter;
    private static bool IsMiniMapVisible = true;

    private readonly ObservableCollection<MindMapDocument> _documents = [];
    private readonly Dictionary<Guid, Border> _nodeControls = [];
    private readonly Dictionary<Guid, Button> _collapseToggleControls = [];
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private Point _tabDragStart;
    private MindMapDocument? _draggedDocument;
    private bool _skipClosePrompt;
    private bool _isMovingDocument;

    private MindMapNode? _draggedNode;
    private readonly List<MindMapNode> _draggedNodes = [];
    private Point _dragStart;
    private Point _panStart;
    private bool _isPanning;
    private bool _isUpdatingInspector;
    private bool _isUpdatingInlineEditor;
    private bool _isComposingText;
    private bool _isUpdatingTree;
    private bool _isUpdatingNodeProperties;
    private bool _isUpdatingColorPicker;
    private TextBox? _inlineTitleBox;
    private Guid? _editingNodeId;
    private GridLength _previousRightSidebarWidth = new(300);
    private bool _isRightSidebarCollapsed;
    private bool _hasTextEditUndoSnapshot;
    private Guid? _textEditLayoutNodeId;
    private Size _textEditLayoutBaseSize;
    private readonly Dictionary<Guid, Point> _textEditLayoutBasePositions = [];
    private bool _hasNodeDragUndoSnapshot;
    private bool _isSelectingWithMarquee;
    private bool _isMarqueeCtrlMode;
    private Point _marqueeStart;
    private string? _activeColorProperty;
    private Color _pendingColor = Colors.Black;
    private Guid? _pendingConnectionSourceNodeId;
    private Rect _miniMapWorldBounds = Rect.Empty;
    private double _miniMapScale = 1;
    private double _miniMapOffsetX;
    private double _miniMapOffsetY;
    private bool _isDraggingMiniMapViewport;
    private Vector _miniMapDragOffset;

    private readonly record struct LayoutBounds(double Top, double Bottom);

    private sealed record ExportImageFormat(string Key, string LabelKey, string DefaultExtension, string FilterPattern, string[] Extensions)
    {
        public string Label => Localization.T(LabelKey);
    }

    private sealed record ExportImageOptions(string FilePath, string FormatKey, int PixelWidth, int PixelHeight);

    private static readonly ExportImageFormat[] ExportImageFormats =
    [
        new("png", "ImageFilterPng", ".png", "*.png", [".png"]),
        new("jpg", "ImageFilterJpg", ".jpg", "*.jpg;*.jpeg", [".jpg", ".jpeg"]),
        new("bmp", "ImageFilterBmp", ".bmp", "*.bmp", [".bmp"]),
        new("gif", "ImageFilterGif", ".gif", "*.gif", [".gif"]),
        new("tiff", "ImageFilterTiff", ".tif", "*.tif;*.tiff", [".tif", ".tiff"])
    ];

    private MindMapDocument CurrentDocument
    {
        get
        {
            if (DocumentsTabControl.SelectedItem is MindMapDocument document)
            {
                return document;
            }

            return _documents[0];
        }
    }

    private MindMapNode _root
    {
        get => CurrentDocument.Root;
        set => CurrentDocument.Root = value;
    }

    private MindMapNode? _selectedNode
    {
        get => CurrentDocument.SelectedNode;
        set => CurrentDocument.SelectedNode = value;
    }

    private HashSet<Guid> _selectedNodeIds => CurrentDocument.SelectedNodeIds;

    private List<MindMapNode> VisibleRoots => _root.IsCanvasRoot ? _root.Children : [_root];

    private MindMapConnection? SelectedConnection => CurrentDocument.SelectedConnectionId is Guid id
        ? CurrentDocument.Connections.FirstOrDefault(connection => connection.Id == id)
        : null;

    public MainWindow()
    {
        ApplyLocalization();
        InitializeComponent();
        ApplyCurrentTheme();
        UpdateLanguageMenuChecks();
        DocumentsTabControl.ItemsSource = _documents;
        OpenWindows.Add(this);
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        AddNewDocument(selectTitle: false);
        UpdateRecentFilesMenu();
        ApplyMiniMapVisibility();
    }

    public MainWindow(MindMapDocument document)
    {
        ApplyLocalization();
        InitializeComponent();
        ApplyCurrentTheme();
        UpdateLanguageMenuChecks();
        DocumentsTabControl.ItemsSource = _documents;
        OpenWindows.Add(this);
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        AddDocument(document);
        UpdateRecentFilesMenu();
        ApplyMiniMapVisibility();
    }

    private static AppSettings LoadAppSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(SettingsPath);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            settings.ThemeMode = NormalizeThemeMode(settings.ThemeMode);
            settings.Language = Localization.NormalizeLanguage(settings.Language);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static void SaveAppSettings()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(AppSettings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Settings are a convenience feature; failing to save them should not block editing.
        }
    }

    private static string NormalizeThemeMode(string? themeMode)
    {
        return themeMode is ThemeLight or ThemeDark or ThemeSystem ? themeMode : ThemeSystem;
    }

    private static void ApplyLocalization()
    {
        AppSettings.Language = Localization.NormalizeLanguage(AppSettings.Language);
        Localization.Apply(AppSettings.Language);
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            object? value = key?.GetValue("AppsUseLightTheme");
            return value is not int intValue || intValue != 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsDarkThemeActive()
    {
        return NormalizeThemeMode(AppSettings.ThemeMode) == ThemeDark ||
            (NormalizeThemeMode(AppSettings.ThemeMode) == ThemeSystem && !IsSystemLightTheme());
    }

    private static SolidColorBrush ThemeBrush(string colorText)
    {
        Color color = (Color)ColorConverter.ConvertFromString(colorText)!;
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private void ApplyCurrentTheme()
    {
        bool dark = IsDarkThemeActive();
        SetThemeBrush("AppBackgroundBrush", dark ? "#111827" : "#F3F4F6");
        SetThemeBrush("TopBarBackgroundBrush", dark ? "#1F2937" : "#F8FAFC");
        SetThemeBrush("ToolbarBackgroundBrush", dark ? "#182232" : "#F3F6FA");
        SetThemeBrush("PanelBackgroundBrush", dark ? "#172033" : "#F8FAFC");
        SetThemeBrush("PanelBorderBrush", dark ? "#334155" : "#D5DCE6");
        SetThemeBrush("TextPrimaryBrush", dark ? "#E5E7EB" : "#1F2937");
        SetThemeBrush("TextMutedBrush", dark ? "#CBD5E1" : "#475569");
        SetThemeBrush("ControlBackgroundBrush", dark ? "#0F172A" : "#FFFFFF");
        SetThemeBrush("ControlBorderBrush", dark ? "#475569" : "#C9D2DE");
        SetThemeBrush("ControlHoverBrush", dark ? "#263449" : "#E5EEF8");
        SetThemeBrush("ControlPressedBrush", dark ? "#31445F" : "#D6E7F8");
        SetThemeBrush("TabBackgroundBrush", dark ? "#243044" : "#E8EDF3");
        SetThemeBrush("TabHoverBackgroundBrush", dark ? "#2B3A52" : "#F6F8FA");
        SetThemeBrush("TabSelectedBackgroundBrush", dark ? "#0F172A" : "#FFFFFF");
        SetThemeBrush("AccentBrush", "#007ACC");
        SetThemeBrush(SystemColors.MenuBrushKey, dark ? "#0F172A" : "#FFFFFF");
        SetThemeBrush(SystemColors.MenuTextBrushKey, dark ? "#E5E7EB" : "#1F2937");
        SetThemeBrush(SystemColors.MenuHighlightBrushKey, dark ? "#263449" : "#E5EEF8");
        SetThemeBrush(SystemColors.HighlightBrushKey, dark ? "#263449" : "#E5EEF8");
        SetThemeBrush(SystemColors.HighlightTextBrushKey, dark ? "#E5E7EB" : "#1F2937");
        SetThemeBrush(SystemColors.ControlBrushKey, dark ? "#0F172A" : "#FFFFFF");
        SetThemeBrush(SystemColors.ControlTextBrushKey, dark ? "#E5E7EB" : "#1F2937");
        SetThemeBrush(SystemColors.WindowBrushKey, dark ? "#0F172A" : "#FFFFFF");
        SetThemeBrush(SystemColors.WindowTextBrushKey, dark ? "#E5E7EB" : "#1F2937");
        SetThemeBrush(SystemColors.GrayTextBrushKey, dark ? "#64748B" : "#6B7280");
        SetThemeBrush(SystemColors.ActiveBorderBrushKey, dark ? "#334155" : "#D5DCE6");
        UpdateThemeMenuChecks();

        if (_documents.Count > 0)
        {
            ApplyCanvasAppearance();
            UpdateTree();
            UpdateMiniMapContent();
        }
    }

    private void SetThemeBrush(string key, string colorText)
    {
        SolidColorBrush brush = ThemeBrush(colorText);
        Resources[key] = brush;
        Application.Current.Resources[key] = brush;
    }

    private void SetThemeBrush(ResourceKey key, string colorText)
    {
        SolidColorBrush brush = ThemeBrush(colorText);
        Resources[key] = brush;
        Application.Current.Resources[key] = brush;
    }

    private static void ApplyThemeToAllWindows()
    {
        foreach (MainWindow window in OpenWindows)
        {
            window.ApplyCurrentTheme();
        }
    }

    private void UpdateThemeMenuChecks()
    {
        string themeMode = NormalizeThemeMode(AppSettings.ThemeMode);
        SystemThemeMenuItem.IsChecked = themeMode == ThemeSystem;
        LightThemeMenuItem.IsChecked = themeMode == ThemeLight;
        DarkThemeMenuItem.IsChecked = themeMode == ThemeDark;
    }

    private static void ApplyLocalizationToAllWindows()
    {
        ApplyLocalization();
        foreach (MainWindow window in OpenWindows)
        {
            window.UpdateLanguageMenuChecks();
            window.UpdateTree();
            window.UpdateInspector();
            window.UpdateStats();
            window.UpdateRecentFilesMenu();
        }
    }

    private void UpdateLanguageMenuChecks()
    {
        if (LanguageMenuItem is null)
        {
            return;
        }

        string language = Localization.NormalizeLanguage(AppSettings.Language);
        LanguageMenuItem.Items.Clear();
        foreach (SupportedLanguage supportedLanguage in Localization.SupportedLanguages)
        {
            MenuItem item = new()
            {
                Header = supportedLanguage.DisplayName,
                Tag = supportedLanguage.Code,
                IsCheckable = true,
                IsChecked = language == supportedLanguage.Code
            };
            item.Click += LanguageMenuItem_Click;
            LanguageMenuItem.Items.Add(item);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TextCompositionManager.AddPreviewTextInputStartHandler(NodeTitleBox, TextComposition_Started);
        TextCompositionManager.AddPreviewTextInputHandler(NodeTitleBox, TextComposition_Completed);
        RenderCanvas();
    }

    private MindMapDocument AddNewDocument(bool selectTitle)
    {
        MindMapNode visibleRoot = CreateStarterMap();
        MindMapNode root = CreateCanvasRoot(visibleRoot, new CanvasSettings());
        LinkParents(root);
        MindMapDocument document = new(root, $"{Localization.T("Untitled")} {++UntitledCounter}");
        AddDocument(document);
        AutoLayoutMap();
        SelectNode(visibleRoot, selectTitle);
        document.MarkDirty();
        return document;
    }

    private void AddDocument(MindMapDocument document)
    {
        NormalizeCanvasSettings(document.CanvasSettings);
        document.Root = EnsureCanvasRoot(document.Root, document.CanvasSettings);
        LinkParents(document.Root);
        document.Connections ??= [];
        NormalizeConnections(document);
        document.SelectedNode = document.SelectedNode is null || document.SelectedNode.IsCanvasRoot
            ? GetFirstVisibleRoot(document.Root)
            : document.SelectedNode;

        if (document.SelectedNodeIds.Count == 0 && document.SelectedNode is not null)
        {
            document.SelectedNodeIds.Add(document.SelectedNode.Id);
        }

        _documents.Add(document);
        DocumentsTabControl.SelectedItem = document;
        ApplyDocumentView(document);
        RenderCanvas();
    }

    private void RemoveDocument(MindMapDocument document)
    {
        _documents.Remove(document);

        if (_documents.Count == 0)
        {
            _skipClosePrompt = _isMovingDocument;
            Close();
            return;
        }

        DocumentsTabControl.SelectedItem = _documents[^1];
    }

    private void DocumentsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentsTabControl.SelectedItem is not MindMapDocument document)
        {
            return;
        }

        ApplyDocumentView(document);
        RenderCanvas();
    }

    private void ApplyDocumentView(MindMapDocument document)
    {
        CanvasScale.ScaleX = document.CanvasScale;
        CanvasScale.ScaleY = document.CanvasScale;
        CanvasTranslate.X = document.CanvasOffsetX;
        CanvasTranslate.Y = document.CanvasOffsetY;
        ApplyCanvasAppearance();
    }

    private void CaptureDocumentView()
    {
        if (_documents.Count == 0)
        {
            return;
        }

        CurrentDocument.CanvasScale = CanvasScale.ScaleX;
        CurrentDocument.CanvasOffsetX = CanvasTranslate.X;
        CurrentDocument.CanvasOffsetY = CanvasTranslate.Y;
    }

    private void CanvasViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_documents.Count == 0)
        {
            return;
        }

        ApplyCanvasAppearance();
    }

    private void MarkCurrentDocumentDirty()
    {
        CurrentDocument.MarkDirty();
        UpdateCommandState();
    }

    private DocumentHistoryEntry CreateHistoryEntry(MindMapDocument document)
    {
        CaptureDocumentView();
        return new DocumentHistoryEntry(
            JsonSerializer.Serialize(document.Root, _jsonOptions),
            JsonSerializer.Serialize(document.CanvasSettings, _jsonOptions),
            JsonSerializer.Serialize(document.Connections, _jsonOptions),
            document.SelectedNodeIds.ToList(),
            document.SelectedNode?.Id,
            document.SelectedConnectionId,
            document.CanvasScale,
            document.CanvasOffsetX,
            document.CanvasOffsetY,
            document.IsDirty);
    }

    private void PushUndoSnapshot()
    {
        if (_documents.Count == 0)
        {
            return;
        }

        MindMapDocument document = CurrentDocument;
        document.UndoStack.Push(CreateHistoryEntry(document));
        if (document.UndoStack.Count > MaxHistoryEntries)
        {
            List<DocumentHistoryEntry> newestFirst = document.UndoStack.Take(MaxHistoryEntries).ToList();
            document.UndoStack.Clear();
            for (int index = newestFirst.Count - 1; index >= 0; index--)
            {
                document.UndoStack.Push(newestFirst[index]);
            }
        }

        document.RedoStack.Clear();
        UpdateCommandState();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        UndoCurrentDocument();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        RedoCurrentDocument();
    }

    private void UndoCurrentDocument()
    {
        MindMapDocument document = CurrentDocument;
        if (document.UndoStack.Count == 0)
        {
            return;
        }

        document.RedoStack.Push(CreateHistoryEntry(document));
        RestoreHistoryEntry(document, document.UndoStack.Pop());
        StatusText.Text = Localization.T("UndoDone");
    }

    private void RedoCurrentDocument()
    {
        MindMapDocument document = CurrentDocument;
        if (document.RedoStack.Count == 0)
        {
            return;
        }

        document.UndoStack.Push(CreateHistoryEntry(document));
        RestoreHistoryEntry(document, document.RedoStack.Pop());
        StatusText.Text = Localization.T("RedoDone");
    }

    private void RestoreHistoryEntry(MindMapDocument document, DocumentHistoryEntry entry)
    {
        MindMapNode? root = JsonSerializer.Deserialize<MindMapNode>(entry.RootJson, _jsonOptions);
        if (root is null)
        {
            return;
        }

        document.Root = root;
        document.CanvasSettings = JsonSerializer.Deserialize<CanvasSettings>(entry.CanvasSettingsJson, _jsonOptions) ?? new CanvasSettings();
        document.Connections = JsonSerializer.Deserialize<List<MindMapConnection>>(entry.ConnectionsJson, _jsonOptions) ?? [];
        NormalizeCanvasSettings(document.CanvasSettings);
        LinkParents(document.Root);
        NormalizeConnections(document);
        DocumentsTabControl.SelectedItem = document;
        document.SelectedNodeIds.Clear();

        HashSet<Guid> existingIds = TraverseVisibleNodes(document.Root).Select(node => node.Id).ToHashSet();
        foreach (Guid id in entry.SelectedNodeIds.Where(existingIds.Contains))
        {
            document.SelectedNodeIds.Add(id);
        }

        document.SelectedNode = entry.SelectedNodeId is Guid selectedId
            ? TraverseVisibleNodes(document.Root).FirstOrDefault(node => node.Id == selectedId)
            : null;
        if (document.SelectedNode is null && document.SelectedNodeIds.Count > 0)
        {
            document.SelectedNode = TraverseVisibleNodes(document.Root).FirstOrDefault(node => document.SelectedNodeIds.Contains(node.Id));
        }

        if (document.SelectedNode is null && entry.SelectedNodeId is not null)
        {
            document.SelectedNode = GetFirstVisibleRoot(document.Root);
        }

        if (document.SelectedNodeIds.Count == 0 && document.SelectedNode is not null)
        {
            document.SelectedNodeIds.Add(document.SelectedNode.Id);
        }

        document.SelectedConnectionId = document.Connections.Any(connection => connection.Id == entry.SelectedConnectionId)
            ? entry.SelectedConnectionId
            : null;

        document.CanvasScale = entry.CanvasScale;
        document.CanvasOffsetX = entry.CanvasOffsetX;
        document.CanvasOffsetY = entry.CanvasOffsetY;
        document.IsDirty = entry.IsDirty;
        _hasTextEditUndoSnapshot = false;
        EndTextEditLayoutSession();
        _hasNodeDragUndoSnapshot = false;
        _isSelectingWithMarquee = false;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        NormalizeSummaryReferences();
        ApplyDocumentView(document);
        RenderCanvas();
        UpdateCommandState();
    }

    private void TabHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _tabDragStart = e.GetPosition(this);
        _draggedDocument = (sender as FrameworkElement)?.DataContext as MindMapDocument;
    }

    private void TabHeader_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedDocument is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point current = e.GetPosition(this);
        if (Math.Abs(current.X - _tabDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _tabDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        MindMapDocument draggedDocument = _draggedDocument;
        _draggedDocument = null;
        DragDropEffects effect = DragDrop.DoDragDrop(
            DocumentsTabControl,
            new DataObject(DocumentDragFormat, draggedDocument),
            DragDropEffects.Move);

        if (effect == DragDropEffects.None && _documents.Contains(draggedDocument))
        {
            DetachDocumentToNewWindow(draggedDocument);
        }
    }

    private void DocumentsTabControl_Drop(object sender, DragEventArgs e)
    {
        AcceptDroppedDocument(e);
    }

    private void TabHeader_Drop(object sender, DragEventArgs e)
    {
        AcceptDroppedDocument(e);
    }

    private void AcceptDroppedDocument(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DocumentDragFormat) ||
            e.Data.GetData(DocumentDragFormat) is not MindMapDocument document)
        {
            return;
        }

        MainWindow? sourceWindow = FindDocumentOwner(document);
        if (sourceWindow == this)
        {
            DocumentsTabControl.SelectedItem = document;
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        sourceWindow?.MoveDocumentOut(document);
        AddDocument(document);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void DetachDocumentToNewWindow(MindMapDocument document)
    {
        MoveDocumentOut(document);
        MainWindow window = new(document)
        {
            Left = Math.Max(0, Left + 40),
            Top = Math.Max(0, Top + 40),
            Width = Width,
            Height = Height
        };
        window.Show();
        window.Activate();
    }

    private void MoveDocumentOut(MindMapDocument document)
    {
        _isMovingDocument = true;
        try
        {
            RemoveDocument(document);
        }
        finally
        {
            _isMovingDocument = false;
        }
    }

    private static MainWindow? FindDocumentOwner(MindMapDocument document)
    {
        return OpenWindows.FirstOrDefault(window => window._documents.Contains(document));
    }

    private static MindMapNode CreateStarterMap()
    {
        return new MindMapNode
        {
            Title = Localization.T("DefaultRootTitle"),
            Children =
            [
                new() { Title = Localization.T("DefaultChildResearch") },
                new() { Title = Localization.T("DefaultChildPlan") },
                new() { Title = Localization.T("DefaultChildDeliver") }
            ]
        };
    }

    private static MindMapNode CreateCanvasRoot(MindMapNode visibleRoot, CanvasSettings canvasSettings)
    {
        NormalizeCanvasSettings(canvasSettings);
        ApplyLegacyLayoutSettings(visibleRoot, canvasSettings);
        return new MindMapNode
        {
            Title = Localization.T("DefaultCanvasTitle"),
            IsCanvasRoot = true,
            Children = [visibleRoot]
        };
    }

    private static MindMapNode EnsureCanvasRoot(MindMapNode root, CanvasSettings canvasSettings)
    {
        if (root.IsCanvasRoot)
        {
            foreach (MindMapNode child in root.Children)
            {
                ApplyLegacyLayoutSettings(child, canvasSettings);
            }

            return root;
        }

        return CreateCanvasRoot(root, canvasSettings);
    }

    private static MindMapNode? GetFirstVisibleRoot(MindMapNode root)
    {
        return root.IsCanvasRoot ? root.Children.FirstOrDefault() : root;
    }

    private static void ApplyLegacyLayoutSettings(MindMapNode node, CanvasSettings canvasSettings)
    {
        node.LayoutDirection = string.IsNullOrWhiteSpace(node.LayoutDirection)
            ? canvasSettings.LayoutDirection
            : node.LayoutDirection;
        node.HorizontalGap = node.HorizontalGap <= 0 ? canvasSettings.HorizontalGap : node.HorizontalGap;
        node.VerticalGap = node.VerticalGap <= 0 ? canvasSettings.VerticalGap : node.VerticalGap;
    }

    private static void LinkParents(MindMapNode node, MindMapNode? parent = null)
    {
        node.Children ??= [];
        node.SummarySourceIds ??= [];
        NormalizeNodeStyle(node);
        node.Parent = parent;

        foreach (MindMapNode child in node.Children)
        {
            LinkParents(child, node);
        }
    }

    private static void NormalizeNodeStyle(MindMapNode node)
    {
        node.FillColor = string.IsNullOrWhiteSpace(node.FillColor) ? "#FFFFFF" : node.FillColor;
        node.BorderColor = string.IsNullOrWhiteSpace(node.BorderColor) ? "#CBD5E1" : node.BorderColor;
        node.TextColor = string.IsNullOrWhiteSpace(node.TextColor) ? "#0F172A" : node.TextColor;
        node.FontFamily = string.IsNullOrWhiteSpace(node.FontFamily) ? "Microsoft YaHei UI" : node.FontFamily;
        node.FontSize = node.FontSize <= 0 ? NodeFontSize : node.FontSize;
        node.FontWeight = string.IsNullOrWhiteSpace(node.FontWeight) ? "SemiBold" : node.FontWeight;
        node.FontStyle = string.IsNullOrWhiteSpace(node.FontStyle) ? "Normal" : node.FontStyle;
        node.TextAlignment = string.IsNullOrWhiteSpace(node.TextAlignment) ? "Center" : node.TextAlignment;
        node.Shape = string.IsNullOrWhiteSpace(node.Shape) ? "RoundedRectangle" : node.Shape;
        node.BorderThickness = node.BorderThickness <= 0 ? 1 : node.BorderThickness;
        node.ConnectorColor = string.IsNullOrWhiteSpace(node.ConnectorColor)
            ? (node.IsSummary ? "#16A34A" : "#94A3B8")
            : node.ConnectorColor;
        node.ConnectorThickness = node.ConnectorThickness <= 0
            ? (node.IsSummary ? 2.5 : 2)
            : node.ConnectorThickness;
        node.ConnectorDashStyle = string.IsNullOrWhiteSpace(node.ConnectorDashStyle)
            ? (node.IsSummary ? "Dash" : "Solid")
            : node.ConnectorDashStyle;
        node.ConnectorShape = string.IsNullOrWhiteSpace(node.ConnectorShape) ? "Curve" : node.ConnectorShape;
        node.ConnectorArrowStyle = NormalizeArrowStyle(node.ConnectorArrowStyle);
        node.LayoutDirection = NormalizeLayoutDirection(node.LayoutDirection);
        node.HorizontalGap = node.HorizontalGap <= 0 ? ParentChildGap : node.HorizontalGap;
        node.VerticalGap = node.VerticalGap <= 0 ? VerticalGap : node.VerticalGap;
    }

    private static void NormalizeCanvasSettings(CanvasSettings settings)
    {
        settings.CanvasBackgroundColor = string.IsNullOrWhiteSpace(settings.CanvasBackgroundColor) ? "#FFFFFF" : settings.CanvasBackgroundColor;
        settings.GridColor = string.IsNullOrWhiteSpace(settings.GridColor) ? "#E5E7EB" : settings.GridColor;
        settings.GridSize = settings.GridSize <= 0 ? 24 : settings.GridSize;
        settings.GridSize = Math.Clamp(settings.GridSize, 8, 96);
        settings.LayoutDirection = NormalizeLayoutDirection(settings.LayoutDirection);
        settings.HorizontalGap = settings.HorizontalGap <= 0 ? ParentChildGap : settings.HorizontalGap;
        settings.VerticalGap = settings.VerticalGap <= 0 ? VerticalGap : settings.VerticalGap;
    }

    private static string NormalizeLayoutDirection(string? layoutDirection)
    {
        return layoutDirection is "Left" or "Up" or "Down" or "DownLeft" or "DownRight" or "Radial"
            ? layoutDirection
            : "Right";
    }

    private static string NormalizeArrowStyle(string? arrowStyle)
    {
        return arrowStyle is "Start" or "End" or "Both" ? arrowStyle : "None";
    }

    private void RenderCanvas()
    {
        ApplyCanvasAppearance();
        MindMapCanvas.Children.Clear();
        _nodeControls.Clear();
        _collapseToggleControls.Clear();
        _inlineTitleBox = null;

        foreach (MindMapNode root in VisibleRoots)
        {
            DrawConnections(root);
        }

        DrawSummaryConnections();
        DrawCustomConnections();

        foreach (MindMapNode node in TraverseDisplayedNodes())
        {
            DrawNode(node);
        }

        UpdateTree();
        UpdateInspector();
        UpdateStats();
        UpdateMiniMapContent();
    }

    private void ApplyCanvasAppearance()
    {
        CanvasSettings settings = CurrentDocument.CanvasSettings;
        NormalizeCanvasSettings(settings);
        Brush background = CreateBrush(GetEffectiveCanvasBackgroundColor(settings), IsDarkThemeActive() ? Color.FromRgb(11, 18, 32) : Colors.White);
        CanvasViewport.Background = background;
        if (!settings.ShowGrid)
        {
            CanvasBackground.Fill = background;
            UpdateMiniMapViewport();
            return;
        }

        double scale = Math.Max(0.01, CanvasScale.ScaleX);
        double gridSize = settings.GridSize * scale;
        double offsetX = PositiveModulo(CanvasTranslate.X, gridSize);
        double offsetY = PositiveModulo(CanvasTranslate.Y, gridSize);
        Pen gridPen = new(CreateBrush(GetEffectiveGridColor(settings), IsDarkThemeActive() ? Color.FromRgb(36, 48, 68) : Color.FromRgb(229, 231, 235)), 1);
        DrawingGroup drawingGroup = new();
        drawingGroup.Children.Add(new GeometryDrawing(null, gridPen, new LineGeometry(new Point(0, 0), new Point(gridSize, 0))));
        drawingGroup.Children.Add(new GeometryDrawing(null, gridPen, new LineGeometry(new Point(0, 0), new Point(0, gridSize))));

        CanvasBackground.Fill = new DrawingBrush(drawingGroup)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(offsetX, offsetY, gridSize, gridSize),
            ViewportUnits = BrushMappingMode.Absolute
        };

        UpdateMiniMapViewport();
    }

    private static double PositiveModulo(double value, double divisor)
    {
        if (divisor <= 0)
        {
            return 0;
        }

        double result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private void UpdateMiniMapContent()
    {
        if (!IsMiniMapVisible || MiniMapBorder.Visibility != Visibility.Visible || MiniMapCanvas is null || MiniMapViewportRectangle is null)
        {
            return;
        }

        List<MindMapNode> nodes = TraverseDisplayedNodes().ToList();
        MiniMapCanvas.Children.Clear();
        if (nodes.Count == 0)
        {
            _miniMapWorldBounds = Rect.Empty;
            MiniMapViewportRectangle.Visibility = Visibility.Collapsed;
            MiniMapCanvas.Children.Add(MiniMapViewportRectangle);
            return;
        }

        Rect bounds = GetNodeBounds(nodes);
        double worldPadding = Math.Max(30, Math.Max(bounds.Width, bounds.Height) * 0.04);
        bounds.Inflate(worldPadding, worldPadding);
        _miniMapWorldBounds = bounds;

        double mapWidth = MiniMapCanvas.ActualWidth > 1 ? MiniMapCanvas.ActualWidth : 218;
        double mapHeight = MiniMapCanvas.ActualHeight > 1 ? MiniMapCanvas.ActualHeight : 138;
        const double mapPadding = 8;
        _miniMapScale = Math.Max(0.0001, Math.Min(
            (mapWidth - mapPadding * 2) / Math.Max(1, bounds.Width),
            (mapHeight - mapPadding * 2) / Math.Max(1, bounds.Height)));
        _miniMapOffsetX = (mapWidth - bounds.Width * _miniMapScale) / 2 - bounds.Left * _miniMapScale;
        _miniMapOffsetY = (mapHeight - bounds.Height * _miniMapScale) / 2 - bounds.Top * _miniMapScale;

        Dictionary<Guid, MindMapNode> nodeById = nodes.ToDictionary(node => node.Id);
        Brush accentBrush = TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue;

        foreach (MindMapNode child in nodes.Where(node => !node.IsSummary && node.Parent is not null && nodeById.ContainsKey(node.Parent.Id)))
        {
            MindMapNode parent = child.Parent!;
            (Point start, Point end) = GetParentChildConnectionPoints(parent, MeasureNodeSize(parent), child, MeasureNodeSize(child));
            MindMapNode connectorStyle = NodeLayoutHierarchy.GetStructuralConnectorStyle(parent, child);
            AddMiniMapLine(start, end, CreateBrush(connectorStyle.ConnectorColor, Color.FromRgb(148, 163, 184)), 1);
        }

        foreach (MindMapNode summary in nodes.Where(node => node.IsSummary))
        {
            foreach (Guid sourceId in summary.SummarySourceIds)
            {
                if (nodeById.TryGetValue(sourceId, out MindMapNode? source))
                {
                    AddMiniMapLine(GetNodeCenter(source), GetNodeCenter(summary), CreateBrush(summary.ConnectorColor, Color.FromRgb(22, 163, 74)), 1);
                }
            }
        }

        foreach (MindMapConnection connection in CurrentDocument.Connections)
        {
            if (nodeById.TryGetValue(connection.SourceNodeId, out MindMapNode? source) &&
                nodeById.TryGetValue(connection.TargetNodeId, out MindMapNode? target))
            {
                AddMiniMapLine(GetNodeCenter(source), GetNodeCenter(target), CreateBrush(connection.Color, Color.FromRgb(37, 99, 235)), 1);
            }
        }

        foreach (MindMapNode node in nodes)
        {
            Size size = MeasureNodeSize(node);
            Point topLeft = WorldToMiniMap(new Point(node.X, node.Y));
            Rectangle previewNode = new()
            {
                Width = Math.Max(3, size.Width * _miniMapScale),
                Height = Math.Max(2, size.Height * _miniMapScale),
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = CreateBrush(node.FillColor, Colors.White),
                Stroke = node == _selectedNode ? accentBrush : CreateBrush(node.BorderColor, Color.FromRgb(148, 163, 184)),
                StrokeThickness = node == _selectedNode ? 1.5 : 0.75,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(previewNode, topLeft.X);
            Canvas.SetTop(previewNode, topLeft.Y);
            MiniMapCanvas.Children.Add(previewNode);
        }

        MiniMapViewportRectangle.Visibility = Visibility.Visible;
        MiniMapCanvas.Children.Add(MiniMapViewportRectangle);
        UpdateMiniMapViewport();
    }

    private void AddMiniMapLine(Point worldStart, Point worldEnd, Brush brush, double thickness)
    {
        Point start = WorldToMiniMap(worldStart);
        Point end = WorldToMiniMap(worldEnd);
        System.Windows.Shapes.Line line = new()
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = brush,
            StrokeThickness = thickness,
            Opacity = 0.8,
            IsHitTestVisible = false
        };
        MiniMapCanvas.Children.Add(line);
    }

    private Point WorldToMiniMap(Point point)
    {
        return new Point(point.X * _miniMapScale + _miniMapOffsetX, point.Y * _miniMapScale + _miniMapOffsetY);
    }

    private Point MiniMapToWorld(Point point)
    {
        double scale = Math.Max(0.0001, _miniMapScale);
        return new Point((point.X - _miniMapOffsetX) / scale, (point.Y - _miniMapOffsetY) / scale);
    }

    private void UpdateMiniMapViewport()
    {
        if (!IsMiniMapVisible || MiniMapBorder.Visibility != Visibility.Visible || MiniMapViewportRectangle is null || _miniMapWorldBounds.IsEmpty || CanvasViewport.ActualWidth <= 0 || CanvasViewport.ActualHeight <= 0)
        {
            return;
        }

        double canvasScale = Math.Max(0.01, CanvasScale.ScaleX);
        Rect visibleWorld = new(
            -CanvasTranslate.X / canvasScale,
            -CanvasTranslate.Y / canvasScale,
            CanvasViewport.ActualWidth / canvasScale,
            CanvasViewport.ActualHeight / canvasScale);
        Point topLeft = WorldToMiniMap(visibleWorld.TopLeft);
        MiniMapViewportRectangle.Width = Math.Max(2, visibleWorld.Width * _miniMapScale);
        MiniMapViewportRectangle.Height = Math.Max(2, visibleWorld.Height * _miniMapScale);
        Canvas.SetLeft(MiniMapViewportRectangle, topLeft.X);
        Canvas.SetTop(MiniMapViewportRectangle, topLeft.Y);
    }

    private void MiniMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_miniMapWorldBounds.IsEmpty)
        {
            return;
        }

        Point point = e.GetPosition(MiniMapCanvas);
        Rect viewportRect = new(
            Canvas.GetLeft(MiniMapViewportRectangle),
            Canvas.GetTop(MiniMapViewportRectangle),
            MiniMapViewportRectangle.Width,
            MiniMapViewportRectangle.Height);
        Point viewportCenter = new(viewportRect.Left + viewportRect.Width / 2, viewportRect.Top + viewportRect.Height / 2);
        _miniMapDragOffset = viewportRect.Contains(point) ? point - viewportCenter : new Vector(0, 0);
        _isDraggingMiniMapViewport = true;
        MiniMapCanvas.CaptureMouse();

        if (!viewportRect.Contains(point))
        {
            CenterMainViewportAtMiniMapPoint(point);
        }

        e.Handled = true;
    }

    private void MiniMap_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingMiniMapViewport || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point point = e.GetPosition(MiniMapCanvas) - _miniMapDragOffset;
        CenterMainViewportAtMiniMapPoint(point);
        e.Handled = true;
    }

    private void MiniMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingMiniMapViewport)
        {
            _isDraggingMiniMapViewport = false;
            MiniMapCanvas.ReleaseMouseCapture();
            CaptureDocumentView();
        }

        e.Handled = true;
    }

    private void CenterMainViewportAtMiniMapPoint(Point miniMapPoint)
    {
        Point worldPoint = MiniMapToWorld(miniMapPoint);
        double scale = Math.Max(0.01, CanvasScale.ScaleX);
        CanvasTranslate.X = CanvasViewport.ActualWidth / 2 - worldPoint.X * scale;
        CanvasTranslate.Y = CanvasViewport.ActualHeight / 2 - worldPoint.Y * scale;
        ApplyCanvasAppearance();
        CaptureDocumentView();
    }

    private void ShowMiniMapMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiniMapVisibility(ShowMiniMapMenuItem.IsChecked);
    }

    private void HideMiniMapButton_Click(object sender, RoutedEventArgs e)
    {
        SetMiniMapVisibility(false);
        e.Handled = true;
    }

    private static void SetMiniMapVisibility(bool isVisible)
    {
        IsMiniMapVisible = isVisible;
        foreach (MainWindow window in OpenWindows)
        {
            window.ApplyMiniMapVisibility();
        }
    }

    private void ApplyMiniMapVisibility()
    {
        ShowMiniMapMenuItem.IsChecked = IsMiniMapVisible;
        MiniMapBorder.Visibility = IsMiniMapVisible ? Visibility.Visible : Visibility.Collapsed;

        if (!IsMiniMapVisible)
        {
            _isDraggingMiniMapViewport = false;
            if (MiniMapCanvas.IsMouseCaptured)
            {
                MiniMapCanvas.ReleaseMouseCapture();
            }

            return;
        }

        if (_documents.Count > 0)
        {
            UpdateMiniMapContent();
        }
    }

    private Point GetViewportPosition(MouseEventArgs e)
    {
        return e.GetPosition(CanvasViewport);
    }

    private Point GetWorldPosition(MouseEventArgs e)
    {
        return ViewportToWorld(GetViewportPosition(e));
    }

    private Point ViewportToWorld(Point point)
    {
        double scale = Math.Max(0.01, CanvasScale.ScaleX);
        return new Point((point.X - CanvasTranslate.X) / scale, (point.Y - CanvasTranslate.Y) / scale);
    }

    private string GetEffectiveCanvasBackgroundColor(CanvasSettings settings)
    {
        if (IsDarkThemeActive() && IsDefaultCanvasColor(settings.CanvasBackgroundColor))
        {
            return "#0B1220";
        }

        return settings.CanvasBackgroundColor;
    }

    private string GetEffectiveGridColor(CanvasSettings settings)
    {
        if (IsDarkThemeActive() && IsDefaultCanvasColor(settings.CanvasBackgroundColor) && IsDefaultGridColor(settings.GridColor))
        {
            return "#243044";
        }

        return settings.GridColor;
    }

    private static bool IsDefaultCanvasColor(string? colorText)
    {
        return string.IsNullOrWhiteSpace(colorText) ||
            string.Equals(colorText, "#FFFFFF", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(colorText, "White", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDefaultGridColor(string? colorText)
    {
        return string.IsNullOrWhiteSpace(colorText) ||
            string.Equals(colorText, "#E5E7EB", StringComparison.OrdinalIgnoreCase);
    }

    private void DrawConnections(MindMapNode parent)
    {
        if (parent.IsCollapsed)
        {
            return;
        }

        Size parentSize = MeasureNodeSize(parent);

        foreach (MindMapNode child in parent.Children)
        {
            if (child.IsSummary && child.SummarySourceIds.Count > 0)
            {
                DrawConnections(child);
                continue;
            }

            Size childSize = MeasureNodeSize(child);
            (Point start, Point end) = GetParentChildConnectionPoints(parent, parentSize, child, childSize);
            if (parent.Children.Count > 0)
            {
                start = GetCollapseToggleCenter(parent, parentSize);
            }

            MindMapNode connectorStyle = NodeLayoutHierarchy.GetStructuralConnectorStyle(parent, child);
            Brush connectorBrush = CreateBrush(connectorStyle.ConnectorColor, Color.FromRgb(148, 163, 184));

            System.Windows.Shapes.Path connector = new()
            {
                Stroke = connectorBrush,
                StrokeThickness = connectorStyle.ConnectorThickness,
                StrokeDashArray = GetConnectorDashArray(connectorStyle.ConnectorDashStyle),
                Data = CreateConnectorGeometry(start, end, connectorStyle.ConnectorShape),
                IsHitTestVisible = false
            };

            MindMapCanvas.Children.Insert(0, connector);
            DrawConnectorArrowheads(start, end, connectorStyle.ConnectorArrowStyle, connectorBrush, connectorStyle.ConnectorThickness, insertBehindNodes: true);
            DrawConnections(child);
        }
    }

    private void DrawSummaryConnections()
    {
        Dictionary<Guid, MindMapNode> nodeById = TraverseDisplayedNodes().ToDictionary(node => node.Id);

        foreach (MindMapNode summaryNode in nodeById.Values.Where(node => node.IsSummary && node.SummarySourceIds.Count > 0))
        {
            foreach (Guid sourceId in summaryNode.SummarySourceIds)
            {
                if (!nodeById.TryGetValue(sourceId, out MindMapNode? sourceNode))
                {
                    continue;
                }

                DrawSummaryConnection(sourceNode, summaryNode);
            }
        }
    }

    private void DrawSummaryConnection(MindMapNode sourceNode, MindMapNode summaryNode)
    {
        Size sourceSize = MeasureNodeSize(sourceNode);
        Size summarySize = MeasureNodeSize(summaryNode);
        string direction = NormalizeLayoutDirection(GetVisibleRoot(sourceNode).LayoutDirection);
        (Point start, Point end) = direction == "Radial"
            ? GetNearestConnectionPoints(sourceNode, sourceSize, summaryNode, summarySize)
            : GetDirectionalConnectionPoints(sourceNode, sourceSize, summaryNode, summarySize, direction);
        Brush connectorBrush = CreateBrush(summaryNode.ConnectorColor, Color.FromRgb(22, 163, 74));

        System.Windows.Shapes.Path connector = new()
        {
            Stroke = connectorBrush,
            StrokeThickness = summaryNode.ConnectorThickness,
            StrokeDashArray = GetConnectorDashArray(summaryNode.ConnectorDashStyle),
            Data = CreateConnectorGeometry(start, end, summaryNode.ConnectorShape),
            IsHitTestVisible = false
        };

        MindMapCanvas.Children.Insert(0, connector);
        DrawConnectorArrowheads(start, end, summaryNode.ConnectorArrowStyle, connectorBrush, summaryNode.ConnectorThickness, insertBehindNodes: true);
    }

    private void DrawCustomConnections()
    {
        Dictionary<Guid, MindMapNode> nodeById = TraverseDisplayedNodes().ToDictionary(node => node.Id);
        foreach (MindMapConnection connection in CurrentDocument.Connections)
        {
            if (!nodeById.TryGetValue(connection.SourceNodeId, out MindMapNode? sourceNode) ||
                !nodeById.TryGetValue(connection.TargetNodeId, out MindMapNode? targetNode))
            {
                continue;
            }

            DrawCustomConnection(connection, sourceNode, targetNode);
        }
    }

    private void DrawCustomConnection(MindMapConnection connection, MindMapNode sourceNode, MindMapNode targetNode)
    {
        NormalizeConnectionStyle(connection);
        Size sourceSize = MeasureNodeSize(sourceNode);
        Size targetSize = MeasureNodeSize(targetNode);
        (Point start, Point end) = GetNearestConnectionPoints(sourceNode, sourceSize, targetNode, targetSize);
        bool isSelected = CurrentDocument.SelectedConnectionId == connection.Id;
        Brush connectorBrush = CreateBrush(connection.Color, Color.FromRgb(37, 99, 235));
        double connectorThickness = isSelected ? Math.Max(3, connection.Thickness + 1) : connection.Thickness;

        System.Windows.Shapes.Path connector = new()
        {
            Stroke = connectorBrush,
            StrokeThickness = connectorThickness,
            StrokeDashArray = GetConnectorDashArray(connection.DashStyle),
            Data = CreateConnectorGeometry(start, end, connection.Shape),
            IsHitTestVisible = true,
            Cursor = Cursors.Hand,
            Tag = connection.Id
        };
        connector.MouseLeftButtonDown += Connection_MouseLeftButtonDown;
        MindMapCanvas.Children.Add(connector);
        DrawConnectorArrowheads(start, end, connection.ArrowStyle, connectorBrush, connectorThickness, insertBehindNodes: false);

        if (!string.IsNullOrWhiteSpace(connection.Title))
        {
            TextBlock label = new()
            {
                Text = connection.Title,
                FontSize = 12,
                Padding = new Thickness(6, 2, 6, 2),
                Background = CreateBrush(CurrentDocument.CanvasSettings.CanvasBackgroundColor, Colors.White),
                Foreground = CreateBrush(connection.Color, Color.FromRgb(37, 99, 235)),
                IsHitTestVisible = true,
                Cursor = Cursors.Hand,
                Tag = connection.Id
            };
            label.MouseLeftButtonDown += Connection_MouseLeftButtonDown;
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Point mid = new((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            Canvas.SetLeft(label, mid.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, mid.Y - label.DesiredSize.Height / 2);
            MindMapCanvas.Children.Add(label);
        }
    }

    private (Point Start, Point End) GetParentChildConnectionPoints(MindMapNode parent, Size parentSize, MindMapNode child, Size childSize)
    {
        MindMapNode root = GetVisibleRoot(parent);
        int childIndex = GetLayoutChildren(parent).IndexOf(child);
        string direction = GetEffectiveChildDirection(root, parent, Math.Max(0, childIndex));
        return GetDirectionalConnectionPoints(parent, parentSize, child, childSize, direction);
    }

    private static (Point Start, Point End) GetDirectionalConnectionPoints(
        MindMapNode source,
        Size sourceSize,
        MindMapNode target,
        Size targetSize,
        string direction)
    {
        Point sourceCenter = new(source.X + sourceSize.Width / 2, source.Y + sourceSize.Height / 2);
        Point targetCenter = new(target.X + targetSize.Width / 2, target.Y + targetSize.Height / 2);
        return direction switch
        {
            "Left" or "DownLeft" =>
                (new Point(source.X, sourceCenter.Y), new Point(target.X + targetSize.Width, targetCenter.Y)),
            "Down" =>
                (new Point(sourceCenter.X, source.Y + sourceSize.Height), new Point(targetCenter.X, target.Y)),
            "Up" =>
                (new Point(sourceCenter.X, source.Y), new Point(targetCenter.X, target.Y + targetSize.Height)),
            _ =>
                (new Point(source.X + sourceSize.Width, sourceCenter.Y), new Point(target.X, targetCenter.Y))
        };
    }

    private static (Point Start, Point End) GetNearestConnectionPoints(MindMapNode source, Size sourceSize, MindMapNode target, Size targetSize)
    {
        Point sourceCenter = new(source.X + sourceSize.Width / 2, source.Y + sourceSize.Height / 2);
        Point targetCenter = new(target.X + targetSize.Width / 2, target.Y + targetSize.Height / 2);
        double deltaX = targetCenter.X - sourceCenter.X;
        double deltaY = targetCenter.Y - sourceCenter.Y;

        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            return deltaX >= 0
                ? (new Point(source.X + sourceSize.Width, sourceCenter.Y), new Point(target.X, targetCenter.Y))
                : (new Point(source.X, sourceCenter.Y), new Point(target.X + targetSize.Width, targetCenter.Y));
        }

        return deltaY >= 0
            ? (new Point(sourceCenter.X, source.Y + sourceSize.Height), new Point(targetCenter.X, target.Y))
            : (new Point(sourceCenter.X, source.Y), new Point(targetCenter.X, target.Y + targetSize.Height));
    }

    private void DrawNode(MindMapNode node)
    {
        bool isSelected = node == _selectedNode;
        bool isMultiSelected = _selectedNodeIds.Contains(node.Id);
        Size nodeSize = MeasureNodeSize(node);
        bool canInlineEdit = isSelected && _selectedNodeIds.Count <= 1 && _editingNodeId == node.Id;
        FrameworkElement titleControl = canInlineEdit ? CreateInlineTitleBox(node, nodeSize) : CreateNodeTitleBlock(node, nodeSize);
        Border border = new()
        {
            Width = nodeSize.Width,
            Height = nodeSize.Height,
            CornerRadius = GetNodeCornerRadius(node, nodeSize),
            Background = GetNodeBackground(node, isSelected),
            BorderBrush = GetNodeBorderBrush(isSelected, isMultiSelected, node),
            BorderThickness = new Thickness(isSelected || isMultiSelected ? Math.Max(2, node.BorderThickness) : node.BorderThickness),
            Cursor = Cursors.Hand,
            Child = titleControl
        };

        border.PreviewMouseLeftButtonDown += Node_MouseLeftButtonDown;
        border.Tag = node.Id;

        Canvas.SetLeft(border, node.X);
        Canvas.SetTop(border, node.Y);

        if (node.Children.Count > 0)
        {
            DrawCollapseToggleTrunk(node, nodeSize);
        }

        MindMapCanvas.Children.Add(border);
        _nodeControls[node.Id] = border;

        if (node.Children.Count > 0)
        {
            DrawCollapseToggle(node, nodeSize);
        }
    }

    private void DrawCollapseToggle(MindMapNode node, Size nodeSize)
    {
        const double toggleSize = 20;
        Button toggle = new()
        {
            Content = node.IsCollapsed ? "+" : "−",
            Tag = node.Id,
            ToolTip = Localization.T(node.IsCollapsed ? "ExpandNode" : "CollapseNode")
        };
        toggle.SetResourceReference(FrameworkElement.StyleProperty, "NodeCollapseToggleButtonStyle");
        toggle.Click += CollapseToggle_Click;

        Point center = GetCollapseToggleCenter(node, nodeSize);
        double left = center.X - toggleSize / 2;
        double top = center.Y - toggleSize / 2;

        Canvas.SetLeft(toggle, left);
        Canvas.SetTop(toggle, top);
        MindMapCanvas.Children.Add(toggle);
        _collapseToggleControls[node.Id] = toggle;
    }

    private Point GetCollapseToggleCenter(MindMapNode node, Size nodeSize)
    {
        const double toggleRadius = 10;
        const double toggleGap = 8;
        double offset = toggleGap + toggleRadius;
        MindMapNode root = GetVisibleRoot(node);
        string direction = NormalizeLayoutDirection(root.LayoutDirection);
        return direction switch
        {
            "Left" or "DownLeft" => new Point(node.X - offset, node.Y + nodeSize.Height / 2),
            "Down" => new Point(node.X + nodeSize.Width / 2, node.Y + nodeSize.Height + offset),
            "Up" => new Point(node.X + nodeSize.Width / 2, node.Y - offset),
            _ => new Point(node.X + nodeSize.Width + offset, node.Y + nodeSize.Height / 2)
        };
    }

    private void DrawCollapseToggleTrunk(MindMapNode node, Size nodeSize)
    {
        Point toggleCenter = GetCollapseToggleCenter(node, nodeSize);
        string direction = NormalizeLayoutDirection(GetVisibleRoot(node).LayoutDirection);
        MindMapNode connectorStyle = node.IsSummary
            ? GetLayoutChildren(node).FirstOrDefault() ?? node
            : node;
        Point nodeAnchor = direction switch
        {
            "Left" or "DownLeft" => new Point(node.X, node.Y + nodeSize.Height / 2),
            "Down" => new Point(node.X + nodeSize.Width / 2, node.Y + nodeSize.Height),
            "Up" => new Point(node.X + nodeSize.Width / 2, node.Y),
            _ => new Point(node.X + nodeSize.Width, node.Y + nodeSize.Height / 2)
        };

        System.Windows.Shapes.Path trunk = new()
        {
            Stroke = CreateBrush(connectorStyle.ConnectorColor, Color.FromRgb(148, 163, 184)),
            StrokeThickness = connectorStyle.ConnectorThickness,
            StrokeDashArray = GetConnectorDashArray(connectorStyle.ConnectorDashStyle),
            Data = new LineGeometry(nodeAnchor, toggleCenter),
            IsHitTestVisible = false
        };
        MindMapCanvas.Children.Insert(0, trunk);
    }

    private void CollapseToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid nodeId } ||
            TraverseVisibleNodes().FirstOrDefault(node => node.Id == nodeId) is not MindMapNode node)
        {
            return;
        }

        PushUndoSnapshot();
        node.IsCollapsed = !node.IsCollapsed;
        _editingNodeId = null;
        MarkCurrentDocumentDirty();
        AutoLayoutMap();
        SelectNode(node);
        e.Handled = true;
    }

    private static Brush GetNodeBackground(MindMapNode node, bool isSelected)
    {
        return CreateBrush(node.FillColor, Color.FromRgb(255, 255, 255));
    }

    private static Brush GetNodeBorderBrush(bool isSelected, bool isMultiSelected, MindMapNode? node = null)
    {
        if (isSelected)
        {
            return new SolidColorBrush(Color.FromRgb(37, 99, 235));
        }

        if (isMultiSelected)
        {
            return new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }

        return node is null
            ? new SolidColorBrush(Color.FromRgb(203, 213, 225))
            : CreateBrush(node.BorderColor, Color.FromRgb(203, 213, 225));
    }

    private static CornerRadius GetNodeCornerRadius(MindMapNode node, Size nodeSize)
    {
        return node.Shape switch
        {
            "Rectangle" => new CornerRadius(0),
            "Pill" => new CornerRadius(nodeSize.Height / 2),
            _ => new CornerRadius(7)
        };
    }

    private static Brush CreateBrush(string colorText, Color fallback)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(colorText)!;
        }
        catch
        {
            return new SolidColorBrush(fallback);
        }
    }

    private TextBlock CreateNodeTitleBlock(MindMapNode node, Size nodeSize)
    {
        double contentWidth = GetNodeContentWidth(nodeSize);
        return new TextBlock
        {
            Text = node.Title,
            Width = contentWidth,
            MaxWidth = contentWidth,
            FontFamily = GetNodeFontFamily(node),
            FontSize = node.FontSize,
            FontWeight = GetNodeFontWeight(node),
            FontStyle = GetNodeFontStyle(node),
            Foreground = CreateBrush(node.TextColor, Color.FromRgb(15, 23, 42)),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = GetNodeTextAlignment(node),
            Margin = new Thickness(NodeHorizontalPadding / 2, 0, NodeHorizontalPadding / 2, 0)
        };
    }

    private TextBox CreateInlineTitleBox(MindMapNode node, Size nodeSize)
    {
        double contentWidth = GetNodeContentWidth(nodeSize);
        TextBox editor = new()
        {
            Text = node.Title,
            Width = contentWidth,
            MaxWidth = contentWidth,
            FontFamily = GetNodeFontFamily(node),
            FontSize = node.FontSize,
            FontWeight = GetNodeFontWeight(node),
            FontStyle = GetNodeFontStyle(node),
            Foreground = CreateBrush(node.TextColor, Color.FromRgb(15, 23, 42)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Margin = new Thickness(NodeHorizontalPadding / 2, 0, NodeHorizontalPadding / 2, 0),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            TextAlignment = GetNodeTextAlignment(node),
            Tag = node.Id
        };

        editor.TextChanged += InlineTitleBox_TextChanged;
        editor.LostFocus += InlineTitleBox_LostFocus;
        TextCompositionManager.AddPreviewTextInputStartHandler(editor, TextComposition_Started);
        TextCompositionManager.AddPreviewTextInputHandler(editor, TextComposition_Completed);
        _inlineTitleBox = editor;
        return editor;
    }

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not Guid nodeId)
        {
            return;
        }

        MindMapNode? node = TraverseVisibleNodes().FirstOrDefault(item => item.Id == nodeId);
        if (node is null)
        {
            return;
        }

        if (_selectedNodeIds.Count <= 1 &&
            ReferenceEquals(node, _selectedNode) &&
            FindVisualAncestor<TextBox>(e.OriginalSource as DependencyObject) is TextBox editor &&
            e.ClickCount < 2)
        {
            editor.Focus();
            return;
        }

        if (_pendingConnectionSourceNodeId is Guid sourceNodeId)
        {
            CompletePendingConnection(sourceNodeId, node);
            e.Handled = true;
            return;
        }

        bool isCtrlClick = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (isCtrlClick)
        {
            ToggleNodeSelection(node);
        }
        else if (_selectedNodeIds.Contains(node.Id) && _selectedNodeIds.Count > 1)
        {
            CurrentDocument.SelectedConnectionId = null;
            _selectedNode = node;
            RenderCanvas();
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("SelectedNodes"), _selectedNodeIds.Count);
        }
        else
        {
            SelectNode(node, focusTitle: e.ClickCount >= 2);
        }

        _draggedNode = IsVisibleRoot(node) ? node : null;
        _draggedNodes.Clear();
        if (_draggedNode is not null)
        {
            _draggedNodes.AddRange(Traverse(_draggedNode));
        }

        _dragStart = GetWorldPosition(e);
        _hasNodeDragUndoSnapshot = false;
        if (_draggedNode is not null)
        {
            CanvasViewport.CaptureMouse();
        }

        e.Handled = true;
    }

    private void ToggleNodeSelection(MindMapNode node)
    {
        _editingNodeId = null;
        EndTextEditLayoutSession();
        CurrentDocument.SelectedConnectionId = null;
        if (_selectedNodeIds.Contains(node.Id) && _selectedNodeIds.Count > 1)
        {
            _selectedNodeIds.Remove(node.Id);
            _selectedNode = TraverseVisibleNodes().FirstOrDefault(item => _selectedNodeIds.Contains(item.Id)) ?? node;
        }
        else
        {
            _selectedNodeIds.Add(node.Id);
            _selectedNode = node;
        }

        RenderCanvas();
        StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("SelectedNodes"), _selectedNodeIds.Count);
    }

    private void Connection_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid connectionId })
        {
            return;
        }

        MindMapConnection? connection = CurrentDocument.Connections.FirstOrDefault(item => item.Id == connectionId);
        if (connection is null)
        {
            return;
        }

        SelectConnection(connection);
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool isControlDown = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool isShiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        if (isControlDown && e.Key == Key.Z)
        {
            if (isShiftDown)
            {
                RedoCurrentDocument();
            }
            else
            {
                UndoCurrentDocument();
            }

            e.Handled = true;
            return;
        }

        if (isControlDown && e.Key == Key.Y)
        {
            RedoCurrentDocument();
            e.Handled = true;
            return;
        }

        if (isControlDown && e.Key == Key.S)
        {
            SaveDocument(CurrentDocument, forceSaveAs: false);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape && _pendingConnectionSourceNodeId is not null)
        {
            CancelPendingConnection();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape && _editingNodeId is not null)
        {
            MindMapNode? node = _selectedNode;
            _editingNodeId = null;
            _hasTextEditUndoSnapshot = false;
            if (node is not null)
            {
                SelectNode(node);
            }

            e.Handled = true;
            return;
        }

        bool isEditingText = Keyboard.FocusedElement is TextBox;
        if (Keyboard.Modifiers == ModifierKeys.None && !isEditingText && (e.Key == Key.Delete || e.Key == Key.Back))
        {
            DeleteSelectedItem();
            e.Handled = true;
            return;
        }

        if (_selectedNode is null || Keyboard.Modifiers != ModifierKeys.None || Keyboard.FocusedElement is Button)
        {
            return;
        }

        if (!isEditingText && e.Key == Key.F2)
        {
            SelectNode(_selectedNode, focusTitle: true);
            e.Handled = true;
            return;
        }

        if (!isEditingText && e.Key == Key.Space)
        {
            SelectNode(_selectedNode, focusTitle: true);
            e.Handled = true;
            return;
        }

        if (!isEditingText && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            MoveNodeSelection(e.Key);
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is TextBox && _isComposingText)
        {
            return;
        }

        if (e.Key == Key.Enter &&
            Keyboard.FocusedElement is TextBox { Tag: Guid editorNodeId } &&
            _editingNodeId == editorNodeId &&
            _selectedNode?.Id == editorNodeId)
        {
            MindMapNode node = _selectedNode;
            _editingNodeId = null;
            _hasTextEditUndoSnapshot = false;
            SelectNode(node);
            e.Handled = true;
            return;
        }

        if (!isEditingText && e.Key == Key.Enter)
        {
            AddSiblingNode(focusTitle: true);
            e.Handled = true;
            return;
        }

        if (!isEditingText && e.Key == Key.Tab)
        {
            if (_selectedNodeIds.Count >= 2)
            {
                AddSummaryNode();
            }
            else
            {
                AddChildNode(focusTitle: true);
            }

            e.Handled = true;
        }
    }

    private void MoveNodeSelection(Key direction)
    {
        if (_selectedNode is null)
        {
            return;
        }

        List<MindMapNode> displayedNodes = TraverseDisplayedNodes().ToList();
        HashSet<Guid> displayedNodeIds = displayedNodes.Select(node => node.Id).ToHashSet();
        MindMapNode? targetNode = direction switch
        {
            Key.Left or Key.Right => FindHorizontalNavigationNode(
                _selectedNode,
                direction,
                displayedNodes,
                displayedNodeIds),
            Key.Up or Key.Down => FindSiblingNavigationNode(
                _selectedNode,
                direction,
                displayedNodeIds),
            _ => null
        };

        if (targetNode is not null)
        {
            SelectNode(targetNode);
        }
    }

    private MindMapNode? FindHorizontalNavigationNode(
        MindMapNode selectedNode,
        Key direction,
        IReadOnlyList<MindMapNode> displayedNodes,
        IReadOnlySet<Guid> displayedNodeIds)
    {
        IEnumerable<MindMapNode> relatedNodes;
        if (selectedNode.IsSummary)
        {
            if (direction == Key.Right)
            {
                return null;
            }

            HashSet<Guid> sourceIds = selectedNode.SummarySourceIds.ToHashSet();
            relatedNodes = displayedNodes.Where(node => sourceIds.Contains(node.Id));
        }
        else if (direction == Key.Left)
        {
            relatedNodes = selectedNode.Parent is not null &&
                !selectedNode.Parent.IsCanvasRoot &&
                displayedNodeIds.Contains(selectedNode.Parent.Id)
                    ? [selectedNode.Parent]
                    : [];
        }
        else
        {
            List<MindMapNode> candidates = selectedNode.Children
                .Where(node => !node.IsSummary && displayedNodeIds.Contains(node.Id))
                .ToList();

            candidates.AddRange(displayedNodes.Where(node =>
                node.IsSummary && node.SummarySourceIds.Contains(selectedNode.Id)));
            relatedNodes = candidates;
        }

        Point origin = GetNodeCenter(selectedNode);
        return relatedNodes
            .Where(candidate => candidate.Id != selectedNode.Id)
            .Select(candidate => new
            {
                Node = candidate,
                Center = GetNodeCenter(candidate)
            })
            .OrderBy(item => Math.Abs(item.Center.X - origin.X) + Math.Abs(item.Center.Y - origin.Y))
            .Select(item => item.Node)
            .FirstOrDefault();
    }

    private static MindMapNode? FindSiblingNavigationNode(
        MindMapNode selectedNode,
        Key direction,
        IReadOnlySet<Guid> displayedNodeIds)
    {
        if (selectedNode.Parent is null)
        {
            return null;
        }

        List<MindMapNode> siblings = selectedNode.Parent.Children
            .Where(node =>
                node.IsSummary == selectedNode.IsSummary &&
                displayedNodeIds.Contains(node.Id))
            .ToList();
        int currentIndex = siblings.FindIndex(node => node.Id == selectedNode.Id);
        int targetIndex = direction == Key.Up ? currentIndex - 1 : currentIndex + 1;
        return currentIndex >= 0 && targetIndex >= 0 && targetIndex < siblings.Count
            ? siblings[targetIndex]
            : null;
    }

    private void MindMapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CanvasViewport.Focus();
        if (!ReferenceEquals(e.OriginalSource, CanvasViewport))
        {
            return;
        }

        if (_pendingConnectionSourceNodeId is not null)
        {
            CancelPendingConnection();
            e.Handled = true;
            return;
        }

        _isSelectingWithMarquee = true;
        _isMarqueeCtrlMode = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        _marqueeStart = GetViewportPosition(e);
        UpdateSelectionRectangle(_marqueeStart, _marqueeStart);
        SelectionRectangle.Visibility = Visibility.Visible;
        CanvasViewport.CaptureMouse();
        e.Handled = true;
    }

    private void MindMapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelectingWithMarquee)
        {
            FinishMarqueeSelection(GetViewportPosition(e));
            CanvasViewport.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_draggedNode is not null && _hasNodeDragUndoSnapshot)
        {
            SnapDraggedNodesToGrid();
            RenderCanvas();
        }

        _draggedNode = null;
        _draggedNodes.Clear();
        if (!_isPanning)
        {
            CanvasViewport.ReleaseMouseCapture();
        }
    }

    private void MindMapCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, CanvasViewport))
        {
            return;
        }

        _isPanning = true;
        _panStart = GetViewportPosition(e);
        CanvasViewport.Cursor = Cursors.SizeAll;
        CanvasViewport.CaptureMouse();
        StatusText.Text = Localization.T("MovingCanvas");
        e.Handled = true;
    }

    private void MindMapCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndCanvasPan();
        e.Handled = true;
    }

    private void MindMapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isSelectingWithMarquee && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateSelectionRectangle(_marqueeStart, GetViewportPosition(e));
            return;
        }

        if (_draggedNode is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            if (!_hasNodeDragUndoSnapshot)
            {
                PushUndoSnapshot();
                _hasNodeDragUndoSnapshot = true;
            }

            Point current = GetWorldPosition(e);
            double offsetX = current.X - _dragStart.X;
            double offsetY = current.Y - _dragStart.Y;

            foreach (MindMapNode node in _draggedNodes)
            {
                node.X += offsetX;
                node.Y += offsetY;
            }

            _dragStart = current;

            MarkCurrentDocumentDirty();
            RenderCanvas();
            StatusText.Text = _draggedNodes.Count > 1
                ? string.Format(CultureInfo.CurrentCulture, Localization.T("MovedNodes"), _draggedNodes.Count)
                : Localization.T("MovedNode");
            return;
        }

        if (_isPanning && e.RightButton == MouseButtonState.Pressed)
        {
            Point current = GetViewportPosition(e);
            CanvasTranslate.X += current.X - _panStart.X;
            CanvasTranslate.Y += current.Y - _panStart.Y;
            _panStart = current;
            ApplyCanvasAppearance();
            CaptureDocumentView();
            return;
        }

        if (_isPanning && e.RightButton == MouseButtonState.Released)
        {
            EndCanvasPan();
        }
    }

    private void EndCanvasPan()
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        CanvasViewport.Cursor = Cursors.Arrow;
        CanvasViewport.ReleaseMouseCapture();
        StatusText.Text = Localization.T("CanvasPanEnded");
    }

    private void UpdateSelectionRectangle(Point start, Point end)
    {
        double left = Math.Min(start.X, end.X);
        double top = Math.Min(start.Y, end.Y);
        double width = Math.Abs(start.X - end.X);
        double height = Math.Abs(start.Y - end.Y);
        Canvas.SetLeft(SelectionRectangle, left);
        Canvas.SetTop(SelectionRectangle, top);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private void FinishMarqueeSelection(Point end)
    {
        _isSelectingWithMarquee = false;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        Rect marqueeBounds = new(
            new Point(Math.Min(_marqueeStart.X, end.X), Math.Min(_marqueeStart.Y, end.Y)),
            new Point(Math.Max(_marqueeStart.X, end.X), Math.Max(_marqueeStart.Y, end.Y)));
        Point startWorld = ViewportToWorld(_marqueeStart);
        Point endWorld = ViewportToWorld(end);
        Rect selectionBounds = new(
            new Point(Math.Min(startWorld.X, endWorld.X), Math.Min(startWorld.Y, endWorld.Y)),
            new Point(Math.Max(startWorld.X, endWorld.X), Math.Max(startWorld.Y, endWorld.Y)));

        if (marqueeBounds.Width < 4 || marqueeBounds.Height < 4)
        {
            if (!_isMarqueeCtrlMode)
            {
                SelectCanvas();
            }

            return;
        }

        List<MindMapNode> hitNodes = TraverseDisplayedNodes()
            .Where(node => selectionBounds.IntersectsWith(new Rect(new Point(node.X, node.Y), MeasureNodeSize(node))))
            .ToList();

        if (!_isMarqueeCtrlMode)
        {
            _selectedNodeIds.Clear();
        }

        if (hitNodes.Count == 0)
        {
            if (!_isMarqueeCtrlMode)
            {
                SelectCanvas();
            }

            return;
        }

        foreach (MindMapNode node in hitNodes)
        {
            if (_isMarqueeCtrlMode && _selectedNodeIds.Contains(node.Id))
            {
                _selectedNodeIds.Remove(node.Id);
            }
            else
            {
                _selectedNodeIds.Add(node.Id);
            }
        }

        _selectedNode = hitNodes.LastOrDefault(node => _selectedNodeIds.Contains(node.Id))
            ?? TraverseDisplayedNodes().FirstOrDefault(node => _selectedNodeIds.Contains(node.Id));

        RenderCanvas();
        StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("MarqueeSelected"), _selectedNodeIds.Count);
    }

    private void MindMapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 1.08 : 0.92;
        double oldScale = Math.Max(0.35, CanvasScale.ScaleX);
        double nextScale = Math.Clamp(CanvasScale.ScaleX * factor, 0.35, 2.4);
        if (Math.Abs(nextScale - oldScale) < 0.001)
        {
            return;
        }

        Point mouseOnViewport = GetViewportPosition(e);
        Point worldUnderMouse = ViewportToWorld(mouseOnViewport);

        CanvasScale.ScaleX = nextScale;
        CanvasScale.ScaleY = nextScale;
        CanvasTranslate.X = mouseOnViewport.X - worldUnderMouse.X * nextScale;
        CanvasTranslate.Y = mouseOnViewport.Y - worldUnderMouse.Y * nextScale;
        ApplyCanvasAppearance();
        CaptureDocumentView();
        StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("ZoomStatus"), nextScale);
        UpdateStats();
        e.Handled = true;
    }

    private void NodeTitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInspector || _selectedNode is null)
        {
            return;
        }

        UpdateSelectedTitle(NodeTitleBox.Text);

        if (_inlineTitleBox is not null && _inlineTitleBox.Text != _selectedNode.Title)
        {
            _isUpdatingInlineEditor = true;
            _inlineTitleBox.Text = _selectedNode.Title;
            _inlineTitleBox.CaretIndex = _inlineTitleBox.Text.Length;
            _isUpdatingInlineEditor = false;
        }
    }

    private void AddChild_Click(object sender, RoutedEventArgs e)
    {
        AddChildNode(focusTitle: true);
    }

    private void AddRoot_Click(object sender, RoutedEventArgs e)
    {
        AddRootNode(focusTitle: true);
    }

    private void AddRootNode(bool focusTitle)
    {
        MindMapNode root = new()
        {
            Title = Localization.T("NewTopic"),
            Parent = _root
        };
        NormalizeNodeStyle(root);

        if (VisibleRoots.LastOrDefault() is MindMapNode lastRoot)
        {
            CopyVisualStyle(lastRoot, root);
            root.X = lastRoot.X;
            root.Y = GetNodeBounds(Traverse(lastRoot).ToList()).Bottom + Math.Max(80, lastRoot.VerticalGap * 2);
        }
        else
        {
            root.X = 110;
            root.Y = 110;
        }

        PushUndoSnapshot();
        _root.Children.Add(root);
        MarkCurrentDocumentDirty();
        AutoLayoutMap();
        SelectNode(root, focusTitle);
    }

    private void AddChildNode(bool focusTitle)
    {
        if (_selectedNode is null)
        {
            AddRootNode(focusTitle);
            return;
        }

        MindMapNode parent = _selectedNode;
        MindMapNode child = new()
        {
            Title = Localization.T("NewTopic"),
            Parent = parent,
            X = parent.X + MeasureNodeSize(parent).Width + parent.HorizontalGap,
            Y = parent.Y + (parent.Children.Count + 1) * 24
        };
        CopyChildVisualStyle(parent, child);
        PositionNewChildNode(parent, child);

        PushUndoSnapshot();
        parent.IsCollapsed = false;
        parent.Children.Add(child);
        MarkCurrentDocumentDirty();
        AutoLayoutMap();
        SelectNode(child, focusTitle);
    }

    private void AddSibling_Click(object sender, RoutedEventArgs e)
    {
        AddSiblingNode(focusTitle: true);
    }

    private void AddSummary_Click(object sender, RoutedEventArgs e)
    {
        AddSummaryNode();
    }

    private void AddConnection_Click(object sender, RoutedEventArgs e)
    {
        AddConnectionBetweenSelectedNodes();
    }

    private void AddConnectionBetweenSelectedNodes()
    {
        List<MindMapNode> selectedNodes = TraverseDisplayedNodes()
            .Where(node => _selectedNodeIds.Contains(node.Id))
            .ToList();

        if (_selectedNode is null)
        {
            StatusText.Text = Localization.T("SelectConnectionStart");
            return;
        }

        if (selectedNodes.Count < 2)
        {
            _pendingConnectionSourceNodeId = _selectedNode.Id;
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("SelectConnectionTarget"), _selectedNode.Title);
            return;
        }

        MindMapNode target = _selectedNode;
        MindMapNode? source = selectedNodes.FirstOrDefault(node => node.Id != target.Id);
        if (source is null)
        {
            StatusText.Text = Localization.T("SelectDifferentNodes");
            return;
        }

        AddConnection(source, target);
    }

    private void CompletePendingConnection(Guid sourceNodeId, MindMapNode target)
    {
        MindMapNode? source = TraverseDisplayedNodes().FirstOrDefault(node => node.Id == sourceNodeId);
        _pendingConnectionSourceNodeId = null;
        if (source is null)
        {
            StatusText.Text = Localization.T("ConnectionSourceMissing");
            return;
        }

        if (source.Id == target.Id)
        {
            StatusText.Text = Localization.T("SelectAnotherTarget");
            return;
        }

        AddConnection(source, target);
    }

    private void CancelPendingConnection()
    {
        _pendingConnectionSourceNodeId = null;
        StatusText.Text = Localization.T("AddConnectionCanceled");
    }

    private void AddConnection(MindMapNode source, MindMapNode target)
    {
        if (CurrentDocument.Connections.Any(connection =>
                connection.SourceNodeId == source.Id && connection.TargetNodeId == target.Id))
        {
            StatusText.Text = Localization.T("DuplicateConnection");
            SelectNode(target);
            return;
        }

        MindMapConnection connection = new()
        {
            SourceNodeId = source.Id,
            TargetNodeId = target.Id
        };
        NormalizeConnectionStyle(connection);

        PushUndoSnapshot();
        CurrentDocument.Connections.Add(connection);
        MarkCurrentDocumentDirty();
        SelectConnection(connection);
        StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("ConnectionAdded"), source.Title, target.Title);
    }

    private void AddSummaryNode()
    {
        List<MindMapNode> selectedNodes = TraverseDisplayedNodes()
            .Where(node => _selectedNodeIds.Contains(node.Id))
            .ToList();

        if (selectedNodes.Count < 2)
        {
            StatusText.Text = Localization.T("SelectAtLeastTwoNodes");
            return;
        }

        MindMapNode parent = FindNearestCommonAncestor(selectedNodes) ?? _root;
        Rect bounds = GetNodeBounds(selectedNodes);
        MindMapNode summaryNode = new()
        {
            Title = Localization.T("Summary"),
            IsSummary = true,
            FillColor = "#DCFCE7",
            BorderColor = "#16A34A",
            TextColor = "#14532D",
            Shape = "RoundedRectangle",
            BorderThickness = 2,
            ConnectorColor = "#16A34A",
            ConnectorThickness = 2.5,
            ConnectorDashStyle = "Dash",
            ConnectorShape = "Curve",
            SummarySourceIds = selectedNodes.Select(node => node.Id).ToList(),
            Parent = parent
        };
        NormalizeNodeStyle(summaryNode);

        Size summarySize = MeasureNodeSize(summaryNode);
        summaryNode.X = bounds.Right + ParentChildGap;
        summaryNode.Y = bounds.Top + bounds.Height / 2 - summarySize.Height / 2;

        PushUndoSnapshot();
        parent.Children.Add(summaryNode);
        MarkCurrentDocumentDirty();
        AutoLayoutMap();
        SelectNode(summaryNode, focusTitle: true);
        StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("SummaryAdded"), selectedNodes.Count);
    }

    private MindMapNode? FindNearestCommonAncestor(IReadOnlyList<MindMapNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return null;
        }

        List<MindMapNode> firstAncestors = GetAncestorLine(nodes[0]);
        return firstAncestors.FirstOrDefault(candidate =>
            nodes.Skip(1).All(node => GetAncestorLine(node).Contains(candidate)));
    }

    private static List<MindMapNode> GetAncestorLine(MindMapNode node)
    {
        List<MindMapNode> ancestors = [];
        MindMapNode? current = node;

        while (current is not null)
        {
            ancestors.Add(current);
            current = current.Parent;
        }

        return ancestors;
    }

    private Rect GetNodeBounds(IReadOnlyList<MindMapNode> nodes)
    {
        double left = double.PositiveInfinity;
        double top = double.PositiveInfinity;
        double right = double.NegativeInfinity;
        double bottom = double.NegativeInfinity;

        foreach (MindMapNode node in nodes)
        {
            Size size = MeasureNodeSize(node);
            left = Math.Min(left, node.X);
            top = Math.Min(top, node.Y);
            right = Math.Max(right, node.X + size.Width);
            bottom = Math.Max(bottom, node.Y + size.Height);
        }

        return new Rect(new Point(left, top), new Point(right, bottom));
    }

    private static void CopyVisualStyle(MindMapNode source, MindMapNode target)
    {
        target.FillColor = source.FillColor;
        target.BorderColor = source.BorderColor;
        target.TextColor = source.TextColor;
        target.FontFamily = source.FontFamily;
        target.FontSize = source.FontSize;
        target.FontWeight = source.FontWeight;
        target.FontStyle = source.FontStyle;
        target.TextAlignment = source.TextAlignment;
        target.Shape = source.Shape;
        target.BorderThickness = source.BorderThickness;
        target.ConnectorColor = source.ConnectorColor;
        target.ConnectorThickness = source.ConnectorThickness;
        target.ConnectorDashStyle = source.ConnectorDashStyle;
        target.ConnectorShape = source.ConnectorShape;
        target.ConnectorArrowStyle = source.ConnectorArrowStyle;
    }

    private void CopyChildVisualStyle(MindMapNode parent, MindMapNode child)
    {
        MindMapNode? styleSource = NodeLayoutHierarchy.FindChildStyleSource(parent, TraverseVisibleNodes());

        if (styleSource is not null)
        {
            CopyVisualStyle(styleSource, child);
        }

        NormalizeNodeStyle(child);
    }

    private void PositionNewChildNode(MindMapNode parent, MindMapNode child)
    {
        MindMapNode root = GetVisibleRoot(parent);
        string direction = GetEffectiveChildDirection(root, parent, parent.Children.Count);
        PositionNodeFromParent(parent, child, direction);

        if (parent.Children.Count == 0)
        {
            return;
        }

        Rect siblingBounds = GetNodeBounds(parent.Children);
        Size childSize = MeasureNodeSize(child);
        double hGap = root.HorizontalGap;
        double vGap = root.VerticalGap;

        switch (direction)
        {
            case "Down":
            case "Up":
                child.X = siblingBounds.Right + hGap;
                break;
            default:
                child.Y = siblingBounds.Bottom + vGap;
                break;
        }

        if (direction == "Left" || direction == "DownLeft")
        {
            child.X = parent.X - hGap - childSize.Width;
        }

        if (direction == "Up")
        {
            child.Y = parent.Y - vGap - childSize.Height;
        }
    }

    private void PositionNewSiblingNode(MindMapNode selectedSibling, MindMapNode sibling)
    {
        MindMapNode root = GetVisibleRoot(selectedSibling);
        string direction = GetEffectiveChildDirection(root, selectedSibling.Parent ?? root, 0);
        Size selectedSize = MeasureNodeSize(selectedSibling);
        Size siblingSize = MeasureNodeSize(sibling);
        double hGap = root.HorizontalGap;
        double vGap = root.VerticalGap;

        sibling.X = selectedSibling.X;
        sibling.Y = selectedSibling.Y;

        switch (direction)
        {
            case "Down":
            case "Up":
                sibling.X = selectedSibling.X + selectedSize.Width + hGap;
                break;
            case "Left":
            case "DownLeft":
                sibling.X = selectedSibling.X + selectedSize.Width - siblingSize.Width;
                sibling.Y = selectedSibling.Y + selectedSize.Height + vGap;
                break;
            default:
                sibling.Y = selectedSibling.Y + selectedSize.Height + vGap;
                break;
        }
    }

    private void PositionNodeFromParent(MindMapNode parent, MindMapNode child, string direction)
    {
        MindMapNode root = GetVisibleRoot(parent);
        Size parentSize = MeasureNodeSize(parent);
        Size childSize = MeasureNodeSize(child);
        double hGap = root.HorizontalGap;
        double vGap = root.VerticalGap;

        switch (direction)
        {
            case "Left":
            case "DownLeft":
                child.X = parent.X - hGap - childSize.Width;
                child.Y = parent.Y + (parentSize.Height - childSize.Height) / 2;
                break;
            case "Down":
                child.X = parent.X + (parentSize.Width - childSize.Width) / 2;
                child.Y = parent.Y + parentSize.Height + vGap;
                break;
            case "Up":
                child.X = parent.X + (parentSize.Width - childSize.Width) / 2;
                child.Y = parent.Y - vGap - childSize.Height;
                break;
            default:
                child.X = parent.X + parentSize.Width + hGap;
                child.Y = parent.Y + (parentSize.Height - childSize.Height) / 2;
                break;
        }
    }

    private string GetEffectiveChildDirection(MindMapNode root, MindMapNode parent, int childIndex)
    {
        string direction = NormalizeLayoutDirection(root.LayoutDirection);
        if (direction != "Radial")
        {
            return direction;
        }

        if (ReferenceEquals(root, parent))
        {
            return (childIndex % 4) switch
            {
                1 => "Down",
                2 => "Left",
                3 => "Up",
                _ => "Right"
            };
        }

        Point rootCenter = GetNodeCenter(root);
        Point parentCenter = GetNodeCenter(parent);
        double deltaX = parentCenter.X - rootCenter.X;
        double deltaY = parentCenter.Y - rootCenter.Y;

        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            return deltaX < 0 ? "Left" : "Right";
        }

        return deltaY < 0 ? "Up" : "Down";
    }

    private Point GetNodeCenter(MindMapNode node)
    {
        Size size = MeasureNodeSize(node);
        return new Point(node.X + size.Width / 2, node.Y + size.Height / 2);
    }

    private void AddSiblingNode(bool focusTitle)
    {
        if (_selectedNode is null)
        {
            AddRootNode(focusTitle);
            return;
        }

        if (IsVisibleRoot(_selectedNode))
        {
            AddRootNode(focusTitle);
            return;
        }

        MindMapNode parent = _selectedNode.Parent ?? _root;
        MindMapNode sibling = new()
        {
            Title = Localization.T("NewTopic"),
            Parent = parent
        };
        CopyChildVisualStyle(parent, sibling);
        PositionNewSiblingNode(_selectedNode, sibling);

        int selectedIndex = parent.Children.IndexOf(_selectedNode);
        PushUndoSnapshot();
        parent.Children.Insert(selectedIndex + 1, sibling);
        MarkCurrentDocumentDirty();
        AutoLayoutMap();
        SelectNode(sibling, focusTitle);
    }

    private void DeleteNode_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedItem();
    }

    private void DeleteSelectedItem()
    {
        if (SelectedConnection is MindMapConnection selectedConnection)
        {
            PushUndoSnapshot();
            CurrentDocument.Connections.Remove(selectedConnection);
            CurrentDocument.SelectedConnectionId = null;
            MarkCurrentDocumentDirty();
            SelectCanvas();
            StatusText.Text = Localization.T("ConnectionDeleted");
            return;
        }

        if (_selectedNode is null || _selectedNode.IsCanvasRoot)
        {
            StatusText.Text = Localization.T("SelectNodeToDelete");
            return;
        }

        HashSet<Guid> selectedIds = _selectedNodeIds.Count > 0
            ? [.. _selectedNodeIds]
            : [_selectedNode.Id];
        NodeDeletionPlan deletionPlan = NodeDeletionPlanner.Create(_root, selectedIds);
        if (deletionPlan.Targets.Count == 0)
        {
            StatusText.Text = Localization.T("SelectNodeToDelete");
            return;
        }

        if (deletionPlan.RemovesAllVisibleRoots)
        {
            StatusText.Text = Localization.T("KeepOneRoot");
            return;
        }

        MindMapNode? preferredParent = _selectedNode.Parent;
        PushUndoSnapshot();
        foreach (MindMapNode target in deletionPlan.Targets)
        {
            target.Parent?.Children.Remove(target);
        }

        RemoveSummaryReferences(deletionPlan.RemovedNodeIds);
        CurrentDocument.Connections.RemoveAll(connection =>
            deletionPlan.RemovedNodeIds.Contains(connection.SourceNodeId) ||
            deletionPlan.RemovedNodeIds.Contains(connection.TargetNodeId));
        MarkCurrentDocumentDirty();
        AutoLayoutMap();
        MindMapNode? nextSelection = preferredParent is not null &&
            !preferredParent.IsCanvasRoot &&
            !deletionPlan.RemovedNodeIds.Contains(preferredParent.Id)
                ? preferredParent
                : VisibleRoots.FirstOrDefault();
        if (nextSelection is not null)
        {
            SelectNode(nextSelection);
        }
        else
        {
            SelectCanvas();
        }

        StatusText.Text = string.Format(
            CultureInfo.CurrentCulture,
            Localization.T("NodesDeleted"),
            deletionPlan.RemovedNodeIds.Count);
    }

    private void RemoveSummaryReferences(IReadOnlySet<Guid> removedIds)
    {
        foreach (MindMapNode node in TraverseVisibleNodes().Where(node => node.IsSummary))
        {
            node.SummarySourceIds.RemoveAll(removedIds.Contains);
        }
    }

    private void NormalizeSummaryReferences()
    {
        HashSet<Guid> existingIds = TraverseVisibleNodes().Select(node => node.Id).ToHashSet();

        foreach (MindMapNode node in TraverseVisibleNodes().Where(node => node.IsSummary))
        {
            node.SummarySourceIds = node.SummarySourceIds
                .Where(existingIds.Contains)
                .Distinct()
                .ToList();
        }
    }

    private void NormalizeConnections(MindMapDocument document)
    {
        NormalizeConnections(document.Root, document.Connections);
        if (document.SelectedConnectionId is Guid selectedConnectionId &&
            document.Connections.All(connection => connection.Id != selectedConnectionId))
        {
            document.SelectedConnectionId = null;
        }
    }

    private static void NormalizeConnections(MindMapNode root, List<MindMapConnection> connections)
    {
        HashSet<Guid> existingIds = TraverseVisibleNodes(root).Select(node => node.Id).ToHashSet();
        connections.RemoveAll(connection =>
            connection.SourceNodeId == connection.TargetNodeId ||
            !existingIds.Contains(connection.SourceNodeId) ||
            !existingIds.Contains(connection.TargetNodeId));

        foreach (MindMapConnection connection in connections)
        {
            NormalizeConnectionStyle(connection);
        }
    }

    private static void NormalizeConnectionStyle(MindMapConnection connection)
    {
        connection.Title = string.IsNullOrWhiteSpace(connection.Title) ? Localization.T("Relationship") : connection.Title;
        connection.Color = string.IsNullOrWhiteSpace(connection.Color) ? "#2563EB" : connection.Color;
        connection.Thickness = connection.Thickness <= 0 ? 2 : connection.Thickness;
        connection.DashStyle = string.IsNullOrWhiteSpace(connection.DashStyle) ? "Solid" : connection.DashStyle;
        connection.Shape = string.IsNullOrWhiteSpace(connection.Shape) ? "Curve" : connection.Shape;
        connection.ArrowStyle = NormalizeArrowStyle(connection.ArrowStyle);
    }

    private void NewMap_Click(object sender, RoutedEventArgs e)
    {
        AddNewDocument(selectTitle: true);
        StatusText.Text = Localization.T("NewMapCreated");
    }

    private void OpenMap_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = Localization.T("MindMapFileFilter")
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        OpenDocumentFromPath(dialog.FileName);
    }

    private bool OpenDocumentFromPath(string filePath)
    {
        try
        {
            string fullPath = IOPath.GetFullPath(filePath);
            if (TryActivateOpenDocument(fullPath))
            {
                AddRecentFile(fullPath);
                StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("SwitchedToFile"), IOPath.GetFileName(fullPath));
                return true;
            }

            string json = File.ReadAllText(fullPath);
            MindMapFile loadedFile = DeserializeMindMapFile(json);

            MindMapDocument document = new(loadedFile.Root!, $"{Localization.T("Untitled")} {++UntitledCounter}")
            {
                FilePath = fullPath,
                CanvasSettings = loadedFile.CanvasSettings,
                Connections = loadedFile.Connections
            };
            document.MarkClean();
            AddDocument(document);
            NormalizeSummaryReferences();
            NormalizeConnections(document);
            SelectNode(GetFirstVisibleRoot(document.Root) ?? document.Root);
            AddRecentFile(fullPath);
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("OpenedFile"), IOPath.GetFileName(fullPath));
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Localization.T("OpenFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void SaveMap_Click(object sender, RoutedEventArgs e)
    {
        SaveDocument(CurrentDocument, forceSaveAs: false);
    }

    private void SaveMapAs_Click(object sender, RoutedEventArgs e)
    {
        SaveDocument(CurrentDocument, forceSaveAs: true);
    }

    private void ExportImage_Click(object sender, RoutedEventArgs e)
    {
        List<MindMapNode> nodes = TraverseDisplayedNodes().ToList();
        if (nodes.Count == 0)
        {
            MessageBox.Show(this, Localization.T("NoNodesToExport"), Localization.T("ExportImage"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Rect bounds = GetNodeBounds(nodes);
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            MessageBox.Show(this, Localization.T("CannotCalculateExportBounds"), Localization.T("ExportImage"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ExportImageOptions? options = PromptForExportImageOptions(bounds);
        if (options is null)
        {
            return;
        }

        try
        {
            ExportCurrentDocumentImage(options, bounds);
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("ExportedImage"), IOPath.GetFileName(options.FilePath));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Localization.T("ExportImageFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool SaveDocument(MindMapDocument document, bool forceSaveAs)
    {
        string? filePath = document.FilePath;

        if (forceSaveAs || filePath is null)
        {
            filePath = PromptForSavePath(document);
            if (filePath is null)
            {
                return false;
            }
        }

        try
        {
            NormalizeCanvasSettings(document.CanvasSettings);
            string json = JsonSerializer.Serialize(new MindMapFile
            {
                Root = document.Root,
                CanvasSettings = document.CanvasSettings,
                Connections = document.Connections
            }, _jsonOptions);
            File.WriteAllText(filePath, json);
            document.FilePath = IOPath.GetFullPath(filePath);
            document.MarkClean();
            AddRecentFile(document.FilePath);
            UpdateCommandState();
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("SavedFile"), IOPath.GetFileName(filePath));
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Localization.T("SaveFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private string? PromptForSavePath(MindMapDocument document)
    {
        SaveFileDialog dialog = new()
        {
            Filter = Localization.T("MindMapSaveFilter"),
            DefaultExt = "swdtmap",
            AddExtension = true,
            FileName = document.FilePath is null
                ? $"{GetDefaultDocumentFileName(document)}.swdtmap"
                : IOPath.GetFileName(document.FilePath)
        };

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private static string GetDefaultDocumentFileName(MindMapDocument document)
    {
        string title = GetFirstVisibleRoot(document.Root)?.Title ?? document.UntitledName;
        HashSet<char> invalidCharacters = [.. IOPath.GetInvalidFileNameChars()];
        string fileName = new string(title.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray())
            .Trim()
            .TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = document.UntitledName;
        }

        string nameWithoutExtension = IOPath.GetFileNameWithoutExtension(fileName);
        string[] reservedNames = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
        if (reservedNames.Contains(nameWithoutExtension, StringComparer.OrdinalIgnoreCase))
        {
            fileName = $"_{fileName}";
        }

        return fileName.Length > 120 ? fileName[..120].TrimEnd('.', ' ') : fileName;
    }

    private ExportImageOptions? PromptForExportImageOptions(Rect bounds)
    {
        int naturalWidth = Math.Clamp((int)Math.Ceiling(bounds.Width + ExportPadding * 2), MinExportPixelSize, MaxExportPixelSize);
        int naturalHeight = Math.Clamp((int)Math.Ceiling(bounds.Height + ExportPadding * 2), MinExportPixelSize, MaxExportPixelSize);
        string baseName = CurrentDocument.FilePath is null
            ? CurrentDocument.UntitledName
            : IOPath.GetFileNameWithoutExtension(CurrentDocument.FilePath);

        if (baseName.EndsWith(".swdtmap", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^".swdtmap".Length];
        }

        string defaultDirectory = CurrentDocument.FilePath is null
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : IOPath.GetDirectoryName(CurrentDocument.FilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string defaultFilePath = IOPath.Combine(defaultDirectory, $"{baseName}.png");

        ExportImageOptionsDialog dialog = new(defaultFilePath, naturalWidth, naturalHeight)
        {
            Owner = this
        };

        return dialog.ShowDialog() == true ? dialog.Options : null;
    }

    private void ExportCurrentDocumentImage(ExportImageOptions options, Rect bounds)
    {
        int naturalWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width + ExportPadding * 2));
        int naturalHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height + ExportPadding * 2));
        double scaleX = options.PixelWidth / (double)naturalWidth;
        double scaleY = options.PixelHeight / (double)naturalHeight;

        Grid exportRoot = new()
        {
            Width = options.PixelWidth,
            Height = options.PixelHeight,
            Background = CreateExportBackgroundBrush(CurrentDocument.CanvasSettings, scaleX, scaleY)
        };

        Canvas exportCanvas = new()
        {
            Width = naturalWidth,
            Height = naturalHeight,
            ClipToBounds = true,
            RenderTransform = new ScaleTransform(scaleX, scaleY)
        };
        exportRoot.Children.Add(exportCanvas);

        foreach (MindMapNode root in VisibleRoots)
        {
            DrawExportConnections(exportCanvas, root, bounds);
        }

        DrawExportSummaryConnections(exportCanvas, bounds);
        DrawExportCustomConnections(exportCanvas, bounds);

        foreach (MindMapNode node in TraverseDisplayedNodes())
        {
            DrawExportNode(exportCanvas, node, bounds);
        }

        exportRoot.Measure(new Size(options.PixelWidth, options.PixelHeight));
        exportRoot.Arrange(new Rect(0, 0, options.PixelWidth, options.PixelHeight));
        exportRoot.UpdateLayout();

        RenderTargetBitmap bitmap = new(options.PixelWidth, options.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(exportRoot);

        BitmapEncoder encoder = CreateImageEncoder(options.FormatKey);
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        string? directory = IOPath.GetDirectoryName(options.FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(options.FilePath);
        encoder.Save(stream);
    }

    private Brush CreateExportBackgroundBrush(CanvasSettings settings, double scaleX, double scaleY)
    {
        NormalizeCanvasSettings(settings);
        Brush background = CreateBrush(GetEffectiveCanvasBackgroundColor(settings), IsDarkThemeActive() ? Color.FromRgb(11, 18, 32) : Colors.White);
        if (!settings.ShowGrid)
        {
            return background;
        }

        double gridWidth = Math.Max(1, settings.GridSize * scaleX);
        double gridHeight = Math.Max(1, settings.GridSize * scaleY);
        Pen gridPen = new(CreateBrush(GetEffectiveGridColor(settings), IsDarkThemeActive() ? Color.FromRgb(36, 48, 68) : Color.FromRgb(229, 231, 235)), 1);
        DrawingGroup drawingGroup = new();
        drawingGroup.Children.Add(new GeometryDrawing(background, null, new RectangleGeometry(new Rect(0, 0, gridWidth, gridHeight))));
        drawingGroup.Children.Add(new GeometryDrawing(null, gridPen, new LineGeometry(new Point(0, 0), new Point(gridWidth, 0))));
        drawingGroup.Children.Add(new GeometryDrawing(null, gridPen, new LineGeometry(new Point(0, 0), new Point(0, gridHeight))));

        return new DrawingBrush(drawingGroup)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, gridWidth, gridHeight),
            ViewportUnits = BrushMappingMode.Absolute
        };
    }

    private static BitmapEncoder CreateImageEncoder(string formatKey)
    {
        return formatKey switch
        {
            "jpg" => new JpegBitmapEncoder { QualityLevel = 92 },
            "bmp" => new BmpBitmapEncoder(),
            "gif" => new GifBitmapEncoder(),
            "tiff" => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
    }

    private void DrawExportConnections(Canvas targetCanvas, MindMapNode parent, Rect bounds)
    {
        if (parent.IsCollapsed)
        {
            return;
        }

        Size parentSize = MeasureNodeSize(parent);

        foreach (MindMapNode child in parent.Children)
        {
            if (child.IsSummary && child.SummarySourceIds.Count > 0)
            {
                DrawExportConnections(targetCanvas, child, bounds);
                continue;
            }

            Size childSize = MeasureNodeSize(child);
            (Point start, Point end) = GetParentChildConnectionPoints(parent, parentSize, child, childSize);
            Point exportStart = TransformExportPoint(start, bounds);
            Point exportEnd = TransformExportPoint(end, bounds);
            MindMapNode connectorStyle = NodeLayoutHierarchy.GetStructuralConnectorStyle(parent, child);
            Brush connectorBrush = CreateBrush(connectorStyle.ConnectorColor, Color.FromRgb(148, 163, 184));
            AddExportConnector(targetCanvas, exportStart, exportEnd, connectorStyle.ConnectorShape, connectorStyle.ConnectorDashStyle, connectorBrush, connectorStyle.ConnectorThickness);
            DrawExportArrowheads(targetCanvas, exportStart, exportEnd, connectorStyle.ConnectorArrowStyle, connectorBrush, connectorStyle.ConnectorThickness);
            DrawExportConnections(targetCanvas, child, bounds);
        }
    }

    private void DrawExportSummaryConnections(Canvas targetCanvas, Rect bounds)
    {
        Dictionary<Guid, MindMapNode> nodeById = TraverseDisplayedNodes().ToDictionary(node => node.Id);

        foreach (MindMapNode summaryNode in nodeById.Values.Where(node => node.IsSummary && node.SummarySourceIds.Count > 0))
        {
            foreach (Guid sourceId in summaryNode.SummarySourceIds)
            {
                if (!nodeById.TryGetValue(sourceId, out MindMapNode? sourceNode))
                {
                    continue;
                }

                Size sourceSize = MeasureNodeSize(sourceNode);
                Size summarySize = MeasureNodeSize(summaryNode);
                string direction = NormalizeLayoutDirection(GetVisibleRoot(sourceNode).LayoutDirection);
                (Point start, Point end) = direction == "Radial"
                    ? GetNearestConnectionPoints(sourceNode, sourceSize, summaryNode, summarySize)
                    : GetDirectionalConnectionPoints(sourceNode, sourceSize, summaryNode, summarySize, direction);
                Point exportStart = TransformExportPoint(start, bounds);
                Point exportEnd = TransformExportPoint(end, bounds);
                Brush connectorBrush = CreateBrush(summaryNode.ConnectorColor, Color.FromRgb(22, 163, 74));
                AddExportConnector(targetCanvas, exportStart, exportEnd, summaryNode.ConnectorShape, summaryNode.ConnectorDashStyle, connectorBrush, summaryNode.ConnectorThickness);
                DrawExportArrowheads(targetCanvas, exportStart, exportEnd, summaryNode.ConnectorArrowStyle, connectorBrush, summaryNode.ConnectorThickness);
            }
        }
    }

    private void DrawExportCustomConnections(Canvas targetCanvas, Rect bounds)
    {
        Dictionary<Guid, MindMapNode> nodeById = TraverseDisplayedNodes().ToDictionary(node => node.Id);
        foreach (MindMapConnection connection in CurrentDocument.Connections)
        {
            if (!nodeById.TryGetValue(connection.SourceNodeId, out MindMapNode? sourceNode) ||
                !nodeById.TryGetValue(connection.TargetNodeId, out MindMapNode? targetNode))
            {
                continue;
            }

            NormalizeConnectionStyle(connection);
            Size sourceSize = MeasureNodeSize(sourceNode);
            Size targetSize = MeasureNodeSize(targetNode);
            (Point start, Point end) = GetNearestConnectionPoints(sourceNode, sourceSize, targetNode, targetSize);
            Point exportStart = TransformExportPoint(start, bounds);
            Point exportEnd = TransformExportPoint(end, bounds);
            Brush connectorBrush = CreateBrush(connection.Color, Color.FromRgb(37, 99, 235));
            AddExportConnector(targetCanvas, exportStart, exportEnd, connection.Shape, connection.DashStyle, connectorBrush, connection.Thickness);
            DrawExportArrowheads(targetCanvas, exportStart, exportEnd, connection.ArrowStyle, connectorBrush, connection.Thickness);

            if (!string.IsNullOrWhiteSpace(connection.Title))
            {
                TextBlock label = new()
                {
                    Text = connection.Title,
                    FontSize = 12,
                    Padding = new Thickness(6, 2, 6, 2),
                    Background = CreateBrush(GetEffectiveCanvasBackgroundColor(CurrentDocument.CanvasSettings), Colors.White),
                    Foreground = CreateBrush(connection.Color, Color.FromRgb(37, 99, 235)),
                    IsHitTestVisible = false
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Point mid = new((exportStart.X + exportEnd.X) / 2, (exportStart.Y + exportEnd.Y) / 2);
                Canvas.SetLeft(label, mid.X - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, mid.Y - label.DesiredSize.Height / 2);
                targetCanvas.Children.Add(label);
            }
        }
    }

    private void DrawExportNode(Canvas targetCanvas, MindMapNode node, Rect bounds)
    {
        Size nodeSize = MeasureNodeSize(node);
        Border border = new()
        {
            Width = nodeSize.Width,
            Height = nodeSize.Height,
            CornerRadius = GetNodeCornerRadius(node, nodeSize),
            Background = GetNodeBackground(node, isSelected: false),
            BorderBrush = GetNodeBorderBrush(isSelected: false, isMultiSelected: false, node),
            BorderThickness = new Thickness(node.BorderThickness),
            Child = CreateNodeTitleBlock(node, nodeSize),
            IsHitTestVisible = false
        };

        Point location = TransformExportPoint(new Point(node.X, node.Y), bounds);
        Canvas.SetLeft(border, location.X);
        Canvas.SetTop(border, location.Y);
        targetCanvas.Children.Add(border);
    }

    private static void AddExportConnector(Canvas targetCanvas, Point start, Point end, string connectorShape, string dashStyle, Brush brush, double thickness)
    {
        targetCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeDashArray = GetConnectorDashArray(dashStyle),
            Data = CreateConnectorGeometry(start, end, connectorShape),
            IsHitTestVisible = false
        });
    }

    private static void DrawExportArrowheads(Canvas targetCanvas, Point start, Point end, string arrowStyle, Brush brush, double thickness)
    {
        string normalizedArrowStyle = NormalizeArrowStyle(arrowStyle);
        if (normalizedArrowStyle is "None")
        {
            return;
        }

        Vector forward = end - start;
        if (forward.Length < 0.1)
        {
            return;
        }

        if (normalizedArrowStyle is "Start" or "Both")
        {
            AddExportArrowhead(targetCanvas, start, -forward, brush, thickness);
        }

        if (normalizedArrowStyle is "End" or "Both")
        {
            AddExportArrowhead(targetCanvas, end, forward, brush, thickness);
        }
    }

    private static void AddExportArrowhead(Canvas targetCanvas, Point tip, Vector direction, Brush brush, double thickness)
    {
        if (direction.Length < 0.1)
        {
            return;
        }

        direction.Normalize();
        Vector perpendicular = new(-direction.Y, direction.X);
        double arrowLength = Math.Clamp(8 + thickness * 2, 10, 18);
        double arrowWidth = Math.Clamp(5 + thickness * 1.4, 7, 14);
        Point baseCenter = tip - direction * arrowLength;

        targetCanvas.Children.Add(new Polygon
        {
            Points = [tip, baseCenter + perpendicular * (arrowWidth / 2), baseCenter - perpendicular * (arrowWidth / 2)],
            Fill = brush,
            Stroke = brush,
            StrokeThickness = 0,
            IsHitTestVisible = false
        });
    }

    private static Point TransformExportPoint(Point point, Rect bounds)
    {
        return new Point(point.X - bounds.Left + ExportPadding, point.Y - bounds.Top + ExportPadding);
    }

    private sealed class ExportImageOptionsDialog : Window
    {
        private readonly double _aspectRatio;
        private readonly TextBox _filePathBox;
        private readonly ComboBox _formatBox;
        private readonly TextBox _widthBox;
        private readonly TextBox _heightBox;
        private readonly TextBlock _validationText;
        private readonly Button _okButton;
        private bool _isUpdatingSize;

        public ExportImageOptionsDialog(string defaultFilePath, int naturalWidth, int naturalHeight)
        {
            _aspectRatio = naturalWidth / (double)Math.Max(1, naturalHeight);
            Title = Localization.T("ExportImage");
            Width = 520;
            Height = 300;
            MinWidth = 480;
            MinHeight = 280;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            Grid root = new()
            {
                Margin = new Thickness(16)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Content = root;

            TextBlock pathLabel = CreateDialogLabel(Localization.T("ExportFile"));
            Grid.SetRow(pathLabel, 0);
            Grid.SetColumn(pathLabel, 0);
            root.Children.Add(pathLabel);

            _filePathBox = new TextBox
            {
                MinHeight = 30,
                Padding = new Thickness(7, 4, 7, 4),
                Text = defaultFilePath
            };
            _filePathBox.TextChanged += (_, _) => Validate();
            Grid.SetRow(_filePathBox, 0);
            Grid.SetColumn(_filePathBox, 1);
            root.Children.Add(_filePathBox);

            Button browseButton = new()
            {
                Content = Localization.T("Browse"),
                MinWidth = 78,
                Height = 30,
                Margin = new Thickness(8, 0, 0, 0)
            };
            browseButton.Click += BrowseButton_Click;
            Grid.SetRow(browseButton, 0);
            Grid.SetColumn(browseButton, 2);
            root.Children.Add(browseButton);

            TextBlock formatLabel = CreateDialogLabel(Localization.T("ExportFormat"));
            formatLabel.Margin = new Thickness(0, 12, 10, 0);
            Grid.SetRow(formatLabel, 1);
            Grid.SetColumn(formatLabel, 0);
            root.Children.Add(formatLabel);

            _formatBox = new ComboBox
            {
                MinHeight = 30,
                Margin = new Thickness(0, 12, 0, 0),
                ItemsSource = ExportImageFormats,
                DisplayMemberPath = nameof(ExportImageFormat.Label),
                SelectedIndex = 0
            };
            _formatBox.SelectionChanged += FormatBox_SelectionChanged;
            Grid.SetRow(_formatBox, 1);
            Grid.SetColumn(_formatBox, 1);
            Grid.SetColumnSpan(_formatBox, 2);
            root.Children.Add(_formatBox);

            TextBlock sizeLabel = CreateDialogLabel(Localization.T("ExportSize"));
            sizeLabel.Margin = new Thickness(0, 12, 10, 0);
            Grid.SetRow(sizeLabel, 2);
            Grid.SetColumn(sizeLabel, 0);
            root.Children.Add(sizeLabel);

            StackPanel sizePanel = new()
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 12, 0, 0)
            };
            _widthBox = new TextBox
            {
                Width = 96,
                MinHeight = 30,
                Padding = new Thickness(7, 4, 7, 4),
                Text = naturalWidth.ToString(CultureInfo.InvariantCulture)
            };
            _heightBox = new TextBox
            {
                Width = 96,
                MinHeight = 30,
                Padding = new Thickness(7, 4, 7, 4),
                Text = naturalHeight.ToString(CultureInfo.InvariantCulture)
            };
            _widthBox.TextChanged += WidthBox_TextChanged;
            _heightBox.TextChanged += HeightBox_TextChanged;
            sizePanel.Children.Add(_widthBox);
            sizePanel.Children.Add(new TextBlock
            {
                Text = " x ",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            });
            sizePanel.Children.Add(_heightBox);
            sizePanel.Children.Add(new TextBlock
            {
                Text = "px",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });
            Grid.SetRow(sizePanel, 2);
            Grid.SetColumn(sizePanel, 1);
            Grid.SetColumnSpan(sizePanel, 2);
            root.Children.Add(sizePanel);

            TextBlock hintText = new()
            {
                Text = Localization.T("ExportHint"),
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(hintText, 3);
            Grid.SetColumn(hintText, 1);
            Grid.SetColumnSpan(hintText, 2);
            root.Children.Add(hintText);

            _validationText = new TextBlock
            {
                Foreground = Brushes.Firebrick,
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(_validationText, 4);
            Grid.SetColumn(_validationText, 1);
            Grid.SetColumnSpan(_validationText, 2);
            root.Children.Add(_validationText);

            StackPanel buttonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            _okButton = new Button
            {
                Content = Localization.T("Export"),
                IsDefault = true,
                MinWidth = 82,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _okButton.Click += OkButton_Click;
            Button cancelButton = new()
            {
                Content = Localization.T("Cancel"),
                IsCancel = true,
                MinWidth = 82,
                Height = 32
            };
            buttonPanel.Children.Add(_okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 5);
            Grid.SetColumn(buttonPanel, 1);
            Grid.SetColumnSpan(buttonPanel, 2);
            root.Children.Add(buttonPanel);

            Validate();
        }

        public ExportImageOptions? Options { get; private set; }

        private static TextBlock CreateDialogLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ExportImageFormat format = SelectedFormat;
            SaveFileDialog dialog = new()
            {
                Title = Localization.T("ExportImage"),
                Filter = BuildExportImageFilter(),
                FilterIndex = Math.Max(1, _formatBox.SelectedIndex + 1),
                FileName = NormalizeExportPath(_filePathBox.Text, format),
                DefaultExt = format.DefaultExtension.TrimStart('.'),
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            if (dialog.FilterIndex >= 1 && dialog.FilterIndex <= ExportImageFormats.Length)
            {
                _formatBox.SelectedIndex = dialog.FilterIndex - 1;
            }
            else if (FindFormatByExtension(IOPath.GetExtension(dialog.FileName)) is ExportImageFormat inferredFormat)
            {
                _formatBox.SelectedItem = inferredFormat;
            }

            _filePathBox.Text = dialog.FileName;
            Validate();
        }

        private void FormatBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_filePathBox.Text))
            {
                return;
            }

            _filePathBox.Text = NormalizeExportPath(_filePathBox.Text, SelectedFormat);
            Validate();
        }

        private void WidthBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingSize)
            {
                return;
            }

            if (int.TryParse(_widthBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) && width > 0)
            {
                _isUpdatingSize = true;
                _heightBox.Text = Math.Clamp((int)Math.Round(width / _aspectRatio), MinExportPixelSize, MaxExportPixelSize).ToString(CultureInfo.InvariantCulture);
                _isUpdatingSize = false;
            }

            Validate();
        }

        private void HeightBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingSize)
            {
                return;
            }

            if (int.TryParse(_heightBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height) && height > 0)
            {
                _isUpdatingSize = true;
                _widthBox.Text = Math.Clamp((int)Math.Round(height * _aspectRatio), MinExportPixelSize, MaxExportPixelSize).ToString(CultureInfo.InvariantCulture);
                _isUpdatingSize = false;
            }

            Validate();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildOptions(out ExportImageOptions? options, out string error))
            {
                _validationText.Text = error;
                return;
            }

            Options = options;
            DialogResult = true;
        }

        private void Validate()
        {
            bool valid = TryBuildOptions(out _, out string error);
            _validationText.Text = valid ? string.Empty : error;
            _okButton.IsEnabled = valid;
        }

        private bool TryBuildOptions(out ExportImageOptions? options, out string error)
        {
            options = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(_filePathBox.Text))
            {
                error = Localization.T("ExportPathRequired");
                return false;
            }

            string normalizedPath = NormalizeExportPath(_filePathBox.Text.Trim(), SelectedFormat);
            string? directory = IOPath.GetDirectoryName(IOPath.GetFullPath(normalizedPath));
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                error = Localization.T("ExportDirectoryMissing");
                return false;
            }

            if (!int.TryParse(_widthBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) ||
                !int.TryParse(_heightBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
            {
                error = Localization.T("ExportInvalidSize");
                return false;
            }

            if (width is < MinExportPixelSize or > MaxExportPixelSize ||
                height is < MinExportPixelSize or > MaxExportPixelSize)
            {
                error = string.Format(CultureInfo.CurrentCulture, Localization.T("ExportSizeOutOfRange"), MinExportPixelSize, MaxExportPixelSize);
                return false;
            }

            options = new ExportImageOptions(IOPath.GetFullPath(normalizedPath), SelectedFormat.Key, width, height);
            return true;
        }

        private ExportImageFormat SelectedFormat => _formatBox.SelectedItem as ExportImageFormat ?? ExportImageFormats[0];

        private static string BuildExportImageFilter()
        {
            return string.Join("|", ExportImageFormats.Select(format => $"{format.Label} ({format.FilterPattern})|{format.FilterPattern}"));
        }

        private static ExportImageFormat? FindFormatByExtension(string extension)
        {
            return ExportImageFormats.FirstOrDefault(format =>
                format.Extensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase)));
        }

        private static string NormalizeExportPath(string path, ExportImageFormat format)
        {
            string extension = IOPath.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return path + format.DefaultExtension;
            }

            return format.Extensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase))
                ? path
                : IOPath.ChangeExtension(path, format.DefaultExtension);
        }
    }

    private MindMapFile DeserializeMindMapFile(string json)
    {
        MindMapFile? file = JsonSerializer.Deserialize<MindMapFile>(json, _jsonOptions);
        if (file?.Root is not null)
        {
            file.CanvasSettings ??= new CanvasSettings();
            file.Connections ??= [];
            NormalizeCanvasSettings(file.CanvasSettings);
            file.Root = EnsureCanvasRoot(file.Root, file.CanvasSettings);
            NormalizeConnections(file.Root, file.Connections);
            return file;
        }

        MindMapNode? legacyRoot = JsonSerializer.Deserialize<MindMapNode>(json, _jsonOptions);
        if (legacyRoot is null)
        {
            throw new InvalidOperationException(Localization.T("InvalidMindMapFile"));
        }

        return new MindMapFile
        {
            Root = EnsureCanvasRoot(legacyRoot, new CanvasSettings()),
            CanvasSettings = new CanvasSettings(),
            Connections = []
        };
    }

    private bool TryActivateOpenDocument(string filePath)
    {
        string fullPath = IOPath.GetFullPath(filePath);

        foreach (MainWindow window in OpenWindows)
        {
            MindMapDocument? existing = window._documents.FirstOrDefault(document =>
                document.FilePath is not null &&
                string.Equals(IOPath.GetFullPath(document.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                continue;
            }

            window.DocumentsTabControl.SelectedItem = existing;
            window.Activate();
            return true;
        }

        return false;
    }

    private void OpenRecentFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string filePath })
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            MessageBox.Show(this, Localization.T("RecentFileMissing"), Localization.T("RecentFilesTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            RemoveRecentFile(filePath);
            return;
        }

        OpenDocumentFromPath(filePath);
    }

    private void AddRecentFile(string filePath)
    {
        string fullPath = IOPath.GetFullPath(filePath);
        AppSettings.RecentFiles.RemoveAll(path => string.Equals(path, fullPath, StringComparison.OrdinalIgnoreCase));
        AppSettings.RecentFiles.Insert(0, fullPath);
        AppSettings.RecentFiles = AppSettings.RecentFiles
            .Where(File.Exists)
            .Take(MaxRecentFiles)
            .ToList();
        SaveAppSettings();

        foreach (MainWindow window in OpenWindows)
        {
            window.UpdateRecentFilesMenu();
        }
    }

    private void RemoveRecentFile(string filePath)
    {
        AppSettings.RecentFiles.RemoveAll(path => string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase));
        SaveAppSettings();

        foreach (MainWindow window in OpenWindows)
        {
            window.UpdateRecentFilesMenu();
        }
    }

    private void UpdateRecentFilesMenu()
    {
        if (RecentFilesMenuItem is null)
        {
            return;
        }

        RecentFilesMenuItem.Items.Clear();
        List<string> existingFiles = AppSettings.RecentFiles.Where(File.Exists).Take(MaxRecentFiles).ToList();
        if (existingFiles.Count != AppSettings.RecentFiles.Count)
        {
            AppSettings.RecentFiles = existingFiles;
            SaveAppSettings();
        }

        if (existingFiles.Count == 0)
        {
            RecentFilesMenuItem.Items.Add(new MenuItem
            {
                Header = Localization.T("NoRecentFiles"),
                IsEnabled = false,
                Icon = CreateMenuIcon("\uE81C")
            });
            return;
        }

        foreach (string path in existingFiles)
        {
            RecentFilesMenuItem.Items.Add(new MenuItem
            {
                Header = IOPath.GetFileName(path),
                ToolTip = path,
                Tag = path,
                Icon = CreateMenuIcon("\uE8E5")
            });
        }

        foreach (MenuItem item in RecentFilesMenuItem.Items.OfType<MenuItem>().Where(item => item.IsEnabled))
        {
            item.Click += OpenRecentFile_Click;
        }
    }

    private TextBlock CreateMenuIcon(string glyph)
    {
        TextBlock icon = new()
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 13,
            Width = 18,
            TextAlignment = TextAlignment.Center
        };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        return icon;
    }

    private void ShowCurrentFileInExplorer_Click(object sender, RoutedEventArgs e)
    {
        ShowDocumentInExplorer(CurrentDocument);
    }

    private void ShowTabFileInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: MindMapDocument document })
        {
            ShowDocumentInExplorer(document);
        }
    }

    private void ShowDocumentInExplorer(MindMapDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            MessageBox.Show(this, Localization.T("CurrentFileNotSaved"), Localization.T("Explorer"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{document.FilePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Localization.T("ExplorerFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ConfirmCloseDocument(MindMapDocument document)
    {
        if (!document.IsDirty)
        {
            return true;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            string.Format(CultureInfo.CurrentCulture, Localization.T("SaveChangesPrompt"), document.DisplayName.TrimEnd('*')),
            Localization.T("SaveChangesTitle"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            MessageBoxResult.Yes => SaveDocument(document, forceSaveAs: false),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MindMapDocument document })
        {
            return;
        }

        CloseDocumentFromUi(document);
    }

    private void CloseTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: MindMapDocument document })
        {
            CloseDocumentFromUi(document);
        }
    }

    private void CloseDocumentFromUi(MindMapDocument document)
    {
        if (!ConfirmCloseDocument(document))
        {
            return;
        }

        if (_documents.Count == 1)
        {
            _skipClosePrompt = true;
            Close();
            return;
        }

        RemoveDocument(document);
    }

    private void CloseCurrentTab_Click(object sender, RoutedEventArgs e)
    {
        if (_documents.Count == 0)
        {
            return;
        }

        MindMapDocument document = CurrentDocument;
        if (!ConfirmCloseDocument(document))
        {
            return;
        }

        if (_documents.Count == 1)
        {
            _skipClosePrompt = true;
            Close();
            return;
        }

        RemoveDocument(document);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_skipClosePrompt)
        {
            return;
        }

        foreach (MindMapDocument document in _documents.ToList())
        {
            if (!ConfirmCloseDocument(document))
            {
                e.Cancel = true;
                return;
            }
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        OpenWindows.Remove(this);
    }

    private void AutoLayout_Click(object sender, RoutedEventArgs e)
    {
        PushUndoSnapshot();
        AutoLayoutMap();
        MarkCurrentDocumentDirty();
        RenderCanvas();
        StatusText.Text = Localization.T("LayoutDone");
    }

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        ResetView();
        CaptureDocumentView();
        StatusText.Text = Localization.T("ViewReset");
        UpdateStats();
    }

    private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string themeMode })
        {
            return;
        }

        AppSettings.ThemeMode = NormalizeThemeMode(themeMode);
        SaveAppSettings();
        ApplyThemeToAllWindows();
        StatusText.Text = AppSettings.ThemeMode switch
        {
            ThemeLight => Localization.T("ThemeLightChanged"),
            ThemeDark => Localization.T("ThemeDarkChanged"),
            _ => Localization.T("ThemeSystemChanged")
        };
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string language })
        {
            return;
        }

        AppSettings.Language = Localization.NormalizeLanguage(language);
        SaveAppSettings();
        ApplyLocalizationToAllWindows();
        StatusText.Text = Localization.T("LanguageChanged");
    }

    private void FitAllNodes_Click(object sender, RoutedEventArgs e)
    {
        List<MindMapNode> nodes = TraverseDisplayedNodes().ToList();
        if (nodes.Count == 0)
        {
            return;
        }

        FitBoundsInView(GetNodeBounds(nodes), Localization.T("FitAllDone"));
    }

    private void CenterSelectedNode_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode is null)
        {
            return;
        }

        FitBoundsInView(new Rect(new Point(_selectedNode.X, _selectedNode.Y), MeasureNodeSize(_selectedNode)), Localization.T("CenterSelectedDone"), preserveScale: true);
    }

    private void FitBoundsInView(Rect bounds, string statusText, bool preserveScale = false)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        double width = Math.Max(1, CanvasViewport.ActualWidth);
        double height = Math.Max(1, CanvasViewport.ActualHeight);
        double scale = preserveScale
            ? CanvasScale.ScaleX
            : Math.Clamp(Math.Min((width - 80) / Math.Max(bounds.Width, 1), (height - 80) / Math.Max(bounds.Height, 1)), 0.35, 2.4);

        CanvasScale.ScaleX = scale;
        CanvasScale.ScaleY = scale;
        CanvasTranslate.X = width / 2 - (bounds.Left + bounds.Width / 2) * scale;
        CanvasTranslate.Y = height / 2 - (bounds.Top + bounds.Height / 2) * scale;
        ApplyCanvasAppearance();
        CaptureDocumentView();
        StatusText.Text = statusText;
        UpdateStats();
    }

    private void ToggleRightSidebar_Click(object sender, RoutedEventArgs e)
    {
        if (_isRightSidebarCollapsed)
        {
            RightSidebar.Visibility = Visibility.Visible;
            RightSidebarColumn.MinWidth = 260;
            RightSidebarColumn.Width = _previousRightSidebarWidth.Value > 0
                ? _previousRightSidebarWidth
                : new GridLength(300);
            ToggleRightSidebarButton.Content = "\uE8A0";
            ToggleRightSidebarButton.ToolTip = Localization.T("HideSidebar");
            System.Windows.Automation.AutomationProperties.SetName(ToggleRightSidebarButton, Localization.T("HideSidebar"));
            _isRightSidebarCollapsed = false;
            StatusText.Text = Localization.T("SidebarExpanded");
            return;
        }

        if (RightSidebarColumn.ActualWidth > 0)
        {
            _previousRightSidebarWidth = new GridLength(RightSidebarColumn.ActualWidth);
        }

        RightSidebar.Visibility = Visibility.Collapsed;
        RightSidebarColumn.MinWidth = 0;
        RightSidebarColumn.Width = new GridLength(0);
        ToggleRightSidebarButton.Content = "\uE8A1";
        ToggleRightSidebarButton.ToolTip = Localization.T("ShowSidebar");
        System.Windows.Automation.AutomationProperties.SetName(ToggleRightSidebarButton, Localization.T("ShowSidebar"));
        _isRightSidebarCollapsed = true;
        StatusText.Text = Localization.T("SidebarCollapsed");
    }

    private void SelectNode(MindMapNode node, bool focusTitle = false)
    {
        _hasTextEditUndoSnapshot = false;
        EndTextEditLayoutSession();
        _hasNodeDragUndoSnapshot = false;
        _editingNodeId = focusTitle ? node.Id : null;
        CurrentDocument.SelectedConnectionId = null;
        _selectedNode = node;
        _selectedNodeIds.Clear();
        _selectedNodeIds.Add(node.Id);
        RenderCanvas();
        EnsureNodeVisible(node, prioritizeInlineCaret: false);
        if (focusTitle)
        {
            QueueInlineEditorFocus(node.Id, selectAll: true);
        }
        else
        {
            QueueNodeSelectionFocus(node.Id);
        }

        StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("SelectedNode"), node.Title);
    }

    private void SelectCanvas()
    {
        _editingNodeId = null;
        _pendingConnectionSourceNodeId = null;
        _hasTextEditUndoSnapshot = false;
        EndTextEditLayoutSession();
        _hasNodeDragUndoSnapshot = false;
        CurrentDocument.SelectedConnectionId = null;
        _selectedNode = null;
        _selectedNodeIds.Clear();
        RenderCanvas();
        StatusText.Text = Localization.T("SelectedCanvas");
    }

    private void SelectConnection(MindMapConnection connection)
    {
        _editingNodeId = null;
        _pendingConnectionSourceNodeId = null;
        _hasTextEditUndoSnapshot = false;
        EndTextEditLayoutSession();
        _hasNodeDragUndoSnapshot = false;
        CurrentDocument.SelectedConnectionId = connection.Id;
        _selectedNode = null;
        _selectedNodeIds.Clear();
        RenderCanvas();
        StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("SelectedConnection"), connection.Title);
    }

    private void MindMapTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_isUpdatingTree || e.NewValue is not TreeViewItem { Tag: Guid nodeId })
        {
            return;
        }

        MindMapNode? node = TraverseVisibleNodes().FirstOrDefault(item => item.Id == nodeId);
        if (node is null)
        {
            return;
        }

        SelectNode(node);
    }

    private void UpdateTree()
    {
        _isUpdatingTree = true;
        MindMapTree.Items.Clear();
        foreach (MindMapNode root in VisibleRoots)
        {
            MindMapTree.Items.Add(CreateTreeItem(root));
        }

        SelectTreeItem(_selectedNode?.Id);
        _isUpdatingTree = false;
    }

    private TreeViewItem CreateTreeItem(MindMapNode node)
    {
        TreeViewItem item = new()
        {
            Header = node.IsSummary ? string.Format(CultureInfo.CurrentCulture, Localization.T("SummarySuffix"), node.Title) : node.Title,
            Tag = node.Id,
            IsExpanded = !node.IsCollapsed
        };
        item.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
        item.SetResourceReference(Control.BackgroundProperty, "PanelBackgroundBrush");

        foreach (MindMapNode child in node.Children)
        {
            item.Items.Add(CreateTreeItem(child));
        }

        return item;
    }

    private void SelectTreeItem(Guid? nodeId)
    {
        if (nodeId is null)
        {
            return;
        }

        foreach (object item in MindMapTree.Items)
        {
            if (FindTreeItem(item as TreeViewItem, nodeId.Value) is TreeViewItem treeItem)
            {
                treeItem.IsSelected = true;
                treeItem.BringIntoView();
                return;
            }
        }
    }

    private void UpdateTreeItemHeader(MindMapNode node)
    {
        foreach (object item in MindMapTree.Items)
        {
            if (FindTreeItem(item as TreeViewItem, node.Id) is TreeViewItem treeItem)
            {
                treeItem.Header = node.IsSummary ? string.Format(CultureInfo.CurrentCulture, Localization.T("SummarySuffix"), node.Title) : node.Title;
                return;
            }
        }
    }

    private static TreeViewItem? FindTreeItem(TreeViewItem? item, Guid nodeId)
    {
        if (item is null)
        {
            return null;
        }

        if (item.Tag is Guid id && id == nodeId)
        {
            return item;
        }

        foreach (object child in item.Items)
        {
            if (FindTreeItem(child as TreeViewItem, nodeId) is TreeViewItem result)
            {
                return result;
            }
        }

        return null;
    }

    private void InlineTitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInlineEditor || sender is not TextBox editor || _selectedNode is null)
        {
            return;
        }

        UpdateSelectedTitle(editor.Text);
    }

    private void InlineTitleBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _hasTextEditUndoSnapshot = false;
        EndTextEditLayoutSession();
        if (sender is TextBox { Tag: Guid nodeId } && _editingNodeId == nodeId)
        {
            _editingNodeId = null;
        }

        RefreshCanvasAfterEdit();
    }

    private void TextComposition_Started(object sender, TextCompositionEventArgs e)
    {
        _isComposingText = true;
    }

    private void TextComposition_Completed(object sender, TextCompositionEventArgs e)
    {
        Dispatcher.BeginInvoke(() => _isComposingText = false);
    }

    private void UpdateSelectedTitle(string title)
    {
        if (_selectedNode is null)
        {
            return;
        }

        string nextTitle = string.IsNullOrWhiteSpace(title) ? Localization.T("Untitled") : title.Trim();
        if (_selectedNode.Title != nextTitle)
        {
            BeginTextEditLayoutSession(_selectedNode);
            if (!_hasTextEditUndoSnapshot)
            {
                PushUndoSnapshot();
                _hasTextEditUndoSnapshot = true;
            }

            _selectedNode.Title = nextTitle;
            RestoreTextEditLayoutBaseline();
            ApplyNodeSizeChangeLayout(_selectedNode, _textEditLayoutBaseSize, MeasureNodeSize(_selectedNode));
            MarkCurrentDocumentDirty();
        }

        RefreshNodeLayoutVisuals();
        UpdateTreeItemHeader(_selectedNode);

        if (!ReferenceEquals(Keyboard.FocusedElement, NodeTitleBox))
        {
            _isUpdatingInspector = true;
            NodeTitleBox.Text = _selectedNode.Title;
            _isUpdatingInspector = false;
        }

        UpdateStats();
        UpdateMiniMapContent();
        QueueEnsureNodeVisible(_selectedNode.Id, prioritizeInlineCaret: _editingNodeId == _selectedNode.Id);
        StatusText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("SelectedNode"), _selectedNode.Title);
    }

    private void EnsureNodeVisible(MindMapNode node, bool prioritizeInlineCaret)
    {
        Size nodeSize = MeasureNodeSize(node);
        ViewportVisibilityBounds? priorityBounds = prioritizeInlineCaret
            ? GetInlineCaretViewportBounds(node.Id)
            : null;
        ViewportTranslationDelta delta = ViewportVisibilityGeometry.GetTranslationDelta(
            new ViewportVisibilityBounds(node.X, node.Y, nodeSize.Width, nodeSize.Height),
            new ViewportVisibilitySize(CanvasViewport.ActualWidth, CanvasViewport.ActualHeight),
            CanvasScale.ScaleX,
            CanvasTranslate.X,
            CanvasTranslate.Y,
            NodeVisibilityMargin,
            priorityBounds);

        if (Math.Abs(delta.X) < 0.001 && Math.Abs(delta.Y) < 0.001)
        {
            return;
        }

        CanvasTranslate.X += delta.X;
        CanvasTranslate.Y += delta.Y;
        ApplyCanvasAppearance();
        CaptureDocumentView();
    }

    private ViewportVisibilityBounds? GetInlineCaretViewportBounds(Guid nodeId)
    {
        if (_inlineTitleBox is not TextBox { Tag: Guid editorNodeId } editor || editorNodeId != nodeId)
        {
            return null;
        }

        int caretIndex = Math.Clamp(editor.CaretIndex, 0, editor.Text.Length);
        Rect caretRect = editor.GetRectFromCharacterIndex(caretIndex);
        if (caretRect.IsEmpty)
        {
            return null;
        }

        Point topLeft = editor.TranslatePoint(caretRect.TopLeft, CanvasViewport);
        Point bottomRight = editor.TranslatePoint(caretRect.BottomRight, CanvasViewport);
        return new ViewportVisibilityBounds(
            Math.Min(topLeft.X, bottomRight.X),
            Math.Min(topLeft.Y, bottomRight.Y),
            Math.Max(1, Math.Abs(bottomRight.X - topLeft.X)),
            Math.Max(1, Math.Abs(bottomRight.Y - topLeft.Y)));
    }

    private void QueueEnsureNodeVisible(Guid nodeId, bool prioritizeInlineCaret)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            if (_selectedNode?.Id != nodeId)
            {
                return;
            }

            bool shouldPrioritizeCaret = prioritizeInlineCaret && _editingNodeId == nodeId;
            EnsureNodeVisible(_selectedNode, shouldPrioritizeCaret);
        });
    }

    private void RefreshNodeLayoutVisuals()
    {
        foreach (MindMapNode node in TraverseDisplayedNodes())
        {
            Size size = MeasureNodeSize(node);
            if (_nodeControls.TryGetValue(node.Id, out Border? border))
            {
                border.Width = size.Width;
                border.Height = size.Height;
                Canvas.SetLeft(border, node.X);
                Canvas.SetTop(border, node.Y);
                ApplyTitleControlWidth(border, size);
            }

            if (_collapseToggleControls.TryGetValue(node.Id, out Button? toggle))
            {
                const double toggleSize = 20;
                Point center = GetCollapseToggleCenter(node, size);
                Canvas.SetLeft(toggle, center.X - toggleSize / 2);
                Canvas.SetTop(toggle, center.Y - toggleSize / 2);
            }
        }

        RefreshConnections();
    }

    private void RefreshConnections()
    {
        for (int index = MindMapCanvas.Children.Count - 1; index >= 0; index--)
        {
            if (MindMapCanvas.Children[index] is System.Windows.Shapes.Path ||
                MindMapCanvas.Children[index] is Polygon ||
                MindMapCanvas.Children[index] is TextBlock { Tag: Guid })
            {
                MindMapCanvas.Children.RemoveAt(index);
            }
        }

        foreach (MindMapNode root in VisibleRoots)
        {
            DrawConnections(root);
        }

        DrawSummaryConnections();
        DrawCustomConnections();

        foreach (MindMapNode node in TraverseDisplayedNodes().Where(node => node.Children.Count > 0))
        {
            DrawCollapseToggleTrunk(node, MeasureNodeSize(node));
        }
    }

    private void RefreshCanvasAfterEdit()
    {
        RenderCanvas();
    }

    private void FocusInlineEditor(bool selectAll)
    {
        if (_inlineTitleBox is null)
        {
            return;
        }

        _inlineTitleBox.Focus();
        if (selectAll)
        {
            _inlineTitleBox.SelectAll();
        }
    }

    private void QueueInlineEditorFocus(Guid nodeId, bool selectAll)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (_selectedNode?.Id != nodeId ||
                _inlineTitleBox?.Tag is not Guid editorNodeId ||
                editorNodeId != nodeId)
            {
                return;
            }

            FocusInlineEditor(selectAll);
            Keyboard.Focus(_inlineTitleBox);
        });
    }

    private void QueueNodeSelectionFocus(Guid nodeId)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (_selectedNode?.Id == nodeId && _editingNodeId is null)
            {
                CanvasViewport.Focus();
                Keyboard.Focus(CanvasViewport);
            }
        });
    }

    private static T? FindVisualAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void AutoLayoutMap()
    {
        double nextTop = 110;
        foreach (MindMapNode root in VisibleRoots.Where(node => !node.IsSummary))
        {
            ArrangeVisibleRoot(root, new Point(110, nextTop));
            List<MindMapNode> structuralNodes = NodeLayoutHierarchy.TraverseStructuralNodes(root).ToList();
            Rect bounds = GetNodeBounds(structuralNodes);
            double shiftX = 110 - bounds.Left;
            double shiftY = nextTop - bounds.Top;
            ShiftSubtree(root, shiftX, shiftY);
            bounds = GetNodeBounds(structuralNodes);
            nextTop = bounds.Bottom + Math.Max(90, root.VerticalGap * 2);
        }

        PositionSummaryNodes();
    }

    private static List<MindMapNode> GetLayoutChildren(MindMapNode node)
    {
        return node.IsCollapsed ? [] : node.Children.Where(child => !child.IsSummary).ToList();
    }

    private void PositionSummaryNodes()
    {
        Dictionary<Guid, MindMapNode> nodeById = TraverseDisplayedNodes().ToDictionary(node => node.Id);
        foreach (MindMapNode summary in nodeById.Values.Where(node => node.IsSummary))
        {
            List<MindMapNode> sources = summary.SummarySourceIds
                .Where(nodeById.ContainsKey)
                .Select(id => nodeById[id])
                .Where(node => !node.IsSummary)
                .ToList();
            if (sources.Count == 0)
            {
                continue;
            }

            Rect sourceBounds = GetNodeBounds(sources);
            Size summarySize = MeasureNodeSize(summary);
            MindMapNode root = GetVisibleRoot(sources[0]);
            string direction = NormalizeLayoutDirection(root.LayoutDirection);
            PositionSummaryAnchor(summary, sourceBounds, summarySize, root, direction);

            ArrangeSummarySubtree(summary, direction, root.HorizontalGap, root.VerticalGap);
            ReserveSummarySubtreeSpace(summary, sources, root, direction);
        }
    }

    private static void PositionSummaryAnchor(
        MindMapNode summary,
        Rect sourceBounds,
        Size summarySize,
        MindMapNode root,
        string direction)
    {
        switch (direction)
        {
            case "Left":
            case "DownLeft":
                summary.X = sourceBounds.Left - root.HorizontalGap - summarySize.Width;
                summary.Y = sourceBounds.Top + (sourceBounds.Height - summarySize.Height) / 2;
                break;
            case "Down":
                summary.X = sourceBounds.Left + (sourceBounds.Width - summarySize.Width) / 2;
                summary.Y = sourceBounds.Bottom + root.VerticalGap;
                break;
            case "Up":
                summary.X = sourceBounds.Left + (sourceBounds.Width - summarySize.Width) / 2;
                summary.Y = sourceBounds.Top - root.VerticalGap - summarySize.Height;
                break;
            default:
                summary.X = sourceBounds.Right + root.HorizontalGap;
                summary.Y = sourceBounds.Top + (sourceBounds.Height - summarySize.Height) / 2;
                break;
        }
    }

    private void ReserveSummarySubtreeSpace(
        MindMapNode summary,
        IReadOnlyList<MindMapNode> sources,
        MindMapNode root,
        string direction)
    {
        if (summary.Parent is not MindMapNode commonAncestor || direction == "Radial")
        {
            return;
        }

        List<MindMapNode> siblingBranches = GetLayoutChildren(commonAncestor);
        if (siblingBranches.Count == 0)
        {
            return;
        }

        HashSet<Guid> sourceBranchIds = sources
            .Select(source => GetDirectChildUnderAncestor(source, commonAncestor))
            .Where(branch => branch is not null)
            .Select(branch => branch!.Id)
            .ToHashSet();
        int lastSourceBranchIndex = siblingBranches
            .Select((branch, index) => sourceBranchIds.Contains(branch.Id) ? index : -1)
            .DefaultIfEmpty(-1)
            .Max();
        int firstFollowingBranchIndex = lastSourceBranchIndex + 1;
        if (firstFollowingBranchIndex < 0 || firstFollowingBranchIndex >= siblingBranches.Count)
        {
            return;
        }

        NodeLayoutAxis axis = direction is "Down" or "Up"
            ? NodeLayoutAxis.Horizontal
            : NodeLayoutAxis.Vertical;
        double gap = axis == NodeLayoutAxis.Horizontal ? root.HorizontalGap : root.VerticalGap;
        Rect summaryBounds = GetNodeBounds(TraverseDisplayed(summary).ToList());
        Rect followingBounds = GetNodeBounds(
            NodeLayoutHierarchy.TraverseStructuralNodes(siblingBranches[firstFollowingBranchIndex]).ToList());
        double separation = NodeLayoutGeometry.GetFollowingBranchSeparation(
            new NodeLayoutRect(summaryBounds.Left, summaryBounds.Top, summaryBounds.Right, summaryBounds.Bottom),
            new NodeLayoutRect(followingBounds.Left, followingBounds.Top, followingBounds.Right, followingBounds.Bottom),
            gap,
            axis);
        if (separation < 0.001)
        {
            return;
        }

        for (int index = firstFollowingBranchIndex; index < siblingBranches.Count; index++)
        {
            ShiftSubtreeAlongAxis(siblingBranches[index], separation, axis);
        }

        RecenterLayoutAncestors(commonAncestor, axis);
    }

    private static MindMapNode? GetDirectChildUnderAncestor(MindMapNode node, MindMapNode ancestor)
    {
        MindMapNode current = node;
        while (current.Parent is not null && current.Parent != ancestor)
        {
            current = current.Parent;
        }

        return current.Parent == ancestor ? current : null;
    }

    private void RecenterLayoutAncestors(MindMapNode ancestor, NodeLayoutAxis axis)
    {
        MindMapNode? current = ancestor;
        while (current is not null && !current.IsCanvasRoot)
        {
            List<MindMapNode> children = GetLayoutChildren(current);
            if (children.Count > 0)
            {
                Rect firstBounds = GetNodeBounds(NodeLayoutHierarchy.TraverseStructuralNodes(children[0]).ToList());
                Rect lastBounds = GetNodeBounds(NodeLayoutHierarchy.TraverseStructuralNodes(children[^1]).ToList());
                Size currentSize = MeasureNodeSize(current);
                if (axis == NodeLayoutAxis.Vertical)
                {
                    double firstCenter = firstBounds.Top + firstBounds.Height / 2;
                    double lastCenter = lastBounds.Top + lastBounds.Height / 2;
                    current.Y = (firstCenter + lastCenter) / 2 - currentSize.Height / 2;
                }
                else
                {
                    double firstCenter = firstBounds.Left + firstBounds.Width / 2;
                    double lastCenter = lastBounds.Left + lastBounds.Width / 2;
                    current.X = (firstCenter + lastCenter) / 2 - currentSize.Width / 2;
                }
            }

            current = current.Parent;
        }
    }

    private void ArrangeSummarySubtree(
        MindMapNode summary,
        string direction,
        double horizontalGap,
        double verticalGap)
    {
        List<MindMapNode> children = GetLayoutChildren(summary);
        if (children.Count == 0)
        {
            return;
        }

        Size summarySize = MeasureNodeSize(summary);
        NodeLayoutOffset childOrigin = NodeLayoutGeometry.GetSummaryChildOrigin(
            direction,
            new NodeLayoutRect(
                summary.X,
                summary.Y,
                summary.X + summarySize.Width,
                summary.Y + summarySize.Height),
            horizontalGap,
            verticalGap);

        switch (direction)
        {
            case "Left":
            {
                double nextY = childOrigin.Y;
                foreach (MindMapNode child in children)
                {
                    ArrangeNodeLeft(child, childOrigin.X, ref nextY, horizontalGap, verticalGap);
                }

                break;
            }
            case "Down":
            {
                double nextX = childOrigin.X;
                foreach (MindMapNode child in children)
                {
                    ArrangeNodeDown(child, childOrigin.Y, ref nextX, horizontalGap, verticalGap);
                }

                break;
            }
            case "Up":
            {
                double nextX = childOrigin.X;
                foreach (MindMapNode child in children)
                {
                    ArrangeNodeUp(child, childOrigin.Y, ref nextX, horizontalGap, verticalGap);
                }

                break;
            }
            case "DownLeft":
            {
                double nextChildY = childOrigin.Y;
                foreach (MindMapNode child in children)
                {
                    Rect childBounds = ArrangeNodeDownLeft(child, childOrigin.X, nextChildY, horizontalGap, verticalGap);
                    nextChildY = childBounds.Bottom + verticalGap;
                }

                break;
            }
            case "DownRight":
            {
                double nextChildY = childOrigin.Y;
                foreach (MindMapNode child in children)
                {
                    Rect childBounds = ArrangeNodeDownRight(child, childOrigin.X, nextChildY, horizontalGap, verticalGap);
                    nextChildY = childBounds.Bottom + verticalGap;
                }

                break;
            }
            case "Radial":
                ArrangeNodeRadial(summary, new Point(summary.X, summary.Y), horizontalGap, verticalGap);
                break;
            default:
            {
                double nextY = childOrigin.Y;
                foreach (MindMapNode child in children)
                {
                    ArrangeNodeRight(child, childOrigin.X, ref nextY, horizontalGap, verticalGap);
                }

                break;
            }
        }
    }

    private void ArrangeVisibleRoot(MindMapNode root, Point origin)
    {
        NormalizeNodeStyle(root);
        double hGap = root.HorizontalGap;
        double vGap = root.VerticalGap;
        string direction = NormalizeLayoutDirection(root.LayoutDirection);
        Size rootSize = MeasureNodeSize(root);

        switch (direction)
        {
            case "Left":
            {
                double nextY = origin.Y;
                ArrangeNodeLeft(root, origin.X + rootSize.Width, ref nextY, hGap, vGap);
                break;
            }
            case "Down":
            {
                double nextX = origin.X;
                ArrangeNodeDown(root, origin.Y, ref nextX, hGap, vGap);
                break;
            }
            case "Up":
            {
                double nextX = origin.X;
                ArrangeNodeUp(root, origin.Y + rootSize.Height, ref nextX, hGap, vGap);
                break;
            }
            case "DownLeft":
            {
                ArrangeNodeDownLeft(root, origin.X + rootSize.Width, origin.Y, hGap, vGap);
                break;
            }
            case "DownRight":
            {
                ArrangeNodeDownRight(root, origin.X, origin.Y, hGap, vGap);
                break;
            }
            case "Radial":
                ArrangeNodeRadial(root, origin, hGap, vGap);
                break;
            default:
            {
                double nextY = origin.Y;
                ArrangeNodeRight(root, origin.X, ref nextY, hGap, vGap);
                break;
            }
        }
    }

    private LayoutBounds ArrangeNodeRight(MindMapNode node, double x, ref double nextY, double hGap, double vGap)
    {
        Size nodeSize = MeasureNodeSize(node);
        node.X = x;
        double subtreeStart = nextY;

        List<MindMapNode> layoutChildren = GetLayoutChildren(node);
        if (layoutChildren.Count == 0)
        {
            node.Y = nextY;
            double leafBottom = node.Y + nodeSize.Height;
            nextY = leafBottom + vGap;
            return new LayoutBounds(node.Y, leafBottom);
        }

        List<LayoutBounds> childBounds = [];
        double childX = x + nodeSize.Width + hGap;

        foreach (MindMapNode child in layoutChildren)
        {
            childBounds.Add(ArrangeNodeRight(child, childX, ref nextY, hGap, vGap));
        }

        double firstChildCenter = childBounds.First().Top + (childBounds.First().Bottom - childBounds.First().Top) / 2;
        double lastChildCenter = childBounds.Last().Top + (childBounds.Last().Bottom - childBounds.Last().Top) / 2;
        node.Y = (firstChildCenter + lastChildCenter) / 2 - nodeSize.Height / 2;

        double top = Math.Min(node.Y, childBounds.Min(item => item.Top));
        double subtreeBottom = Math.Max(node.Y + nodeSize.Height, childBounds.Max(item => item.Bottom));

        if (top < subtreeStart)
        {
            double shift = subtreeStart - top;
            ShiftSubtreeY(node, shift);
            top += shift;
            subtreeBottom += shift;
        }

        nextY = Math.Max(nextY, subtreeBottom + vGap);
        return new LayoutBounds(top, subtreeBottom);
    }

    private LayoutBounds ArrangeNodeLeft(MindMapNode node, double rightEdge, ref double nextY, double hGap, double vGap)
    {
        Size nodeSize = MeasureNodeSize(node);
        node.X = rightEdge - nodeSize.Width;
        double subtreeStart = nextY;

        List<MindMapNode> layoutChildren = GetLayoutChildren(node);
        if (layoutChildren.Count == 0)
        {
            node.Y = nextY;
            double leafBottom = node.Y + nodeSize.Height;
            nextY = leafBottom + vGap;
            return new LayoutBounds(node.Y, leafBottom);
        }

        List<LayoutBounds> childBounds = [];
        double childRightEdge = node.X - hGap;

        foreach (MindMapNode child in layoutChildren)
        {
            childBounds.Add(ArrangeNodeLeft(child, childRightEdge, ref nextY, hGap, vGap));
        }

        double firstChildCenter = childBounds.First().Top + (childBounds.First().Bottom - childBounds.First().Top) / 2;
        double lastChildCenter = childBounds.Last().Top + (childBounds.Last().Bottom - childBounds.Last().Top) / 2;
        node.Y = (firstChildCenter + lastChildCenter) / 2 - nodeSize.Height / 2;

        double top = Math.Min(node.Y, childBounds.Min(item => item.Top));
        double subtreeBottom = Math.Max(node.Y + nodeSize.Height, childBounds.Max(item => item.Bottom));

        if (top < subtreeStart)
        {
            double shift = subtreeStart - top;
            ShiftSubtreeY(node, shift);
            top += shift;
            subtreeBottom += shift;
        }

        nextY = Math.Max(nextY, subtreeBottom + vGap);
        return new LayoutBounds(top, subtreeBottom);
    }

    private LayoutBounds ArrangeNodeDown(MindMapNode node, double y, ref double nextX, double hGap, double vGap)
    {
        Size nodeSize = MeasureNodeSize(node);
        node.Y = y;
        double subtreeStart = nextX;

        List<MindMapNode> layoutChildren = GetLayoutChildren(node);
        if (layoutChildren.Count == 0)
        {
            node.X = nextX;
            double leafRight = node.X + nodeSize.Width;
            nextX = leafRight + hGap;
            return new LayoutBounds(node.X, leafRight);
        }

        List<LayoutBounds> childBounds = [];
        double childY = y + nodeSize.Height + vGap;

        foreach (MindMapNode child in layoutChildren)
        {
            childBounds.Add(ArrangeNodeDown(child, childY, ref nextX, hGap, vGap));
        }

        double firstChildCenter = childBounds.First().Top + (childBounds.First().Bottom - childBounds.First().Top) / 2;
        double lastChildCenter = childBounds.Last().Top + (childBounds.Last().Bottom - childBounds.Last().Top) / 2;
        node.X = (firstChildCenter + lastChildCenter) / 2 - nodeSize.Width / 2;

        double left = Math.Min(node.X, childBounds.Min(item => item.Top));
        double subtreeRight = Math.Max(node.X + nodeSize.Width, childBounds.Max(item => item.Bottom));

        if (left < subtreeStart)
        {
            double shift = subtreeStart - left;
            ShiftSubtreeX(node, shift);
            left += shift;
            subtreeRight += shift;
        }

        nextX = Math.Max(nextX, subtreeRight + hGap);
        return new LayoutBounds(left, subtreeRight);
    }

    private LayoutBounds ArrangeNodeUp(MindMapNode node, double bottomEdge, ref double nextX, double hGap, double vGap)
    {
        Size nodeSize = MeasureNodeSize(node);
        node.Y = bottomEdge - nodeSize.Height;
        double subtreeStart = nextX;

        List<MindMapNode> layoutChildren = GetLayoutChildren(node);
        if (layoutChildren.Count == 0)
        {
            node.X = nextX;
            double leafRight = node.X + nodeSize.Width;
            nextX = leafRight + hGap;
            return new LayoutBounds(node.X, leafRight);
        }

        List<LayoutBounds> childBounds = [];
        double childBottomEdge = node.Y - vGap;

        foreach (MindMapNode child in layoutChildren)
        {
            childBounds.Add(ArrangeNodeUp(child, childBottomEdge, ref nextX, hGap, vGap));
        }

        double firstChildCenter = childBounds.First().Top + (childBounds.First().Bottom - childBounds.First().Top) / 2;
        double lastChildCenter = childBounds.Last().Top + (childBounds.Last().Bottom - childBounds.Last().Top) / 2;
        node.X = (firstChildCenter + lastChildCenter) / 2 - nodeSize.Width / 2;

        double left = Math.Min(node.X, childBounds.Min(item => item.Top));
        double subtreeRight = Math.Max(node.X + nodeSize.Width, childBounds.Max(item => item.Bottom));

        if (left < subtreeStart)
        {
            double shift = subtreeStart - left;
            ShiftSubtreeX(node, shift);
            left += shift;
            subtreeRight += shift;
        }

        nextX = Math.Max(nextX, subtreeRight + hGap);
        return new LayoutBounds(left, subtreeRight);
    }

    private Rect ArrangeNodeDownRight(MindMapNode node, double x, double y, double hGap, double vGap)
    {
        Size nodeSize = MeasureNodeSize(node);
        node.X = x;
        node.Y = y;
        Rect bounds = new(new Point(node.X, node.Y), nodeSize);

        double childX = node.X + nodeSize.Width + hGap;
        double nextChildY = node.Y + nodeSize.Height + vGap;
        foreach (MindMapNode child in GetLayoutChildren(node))
        {
            Rect childBounds = ArrangeNodeDownRight(child, childX, nextChildY, hGap, vGap);
            bounds.Union(childBounds);
            nextChildY = childBounds.Bottom + vGap;
        }

        return bounds;
    }

    private Rect ArrangeNodeDownLeft(MindMapNode node, double rightEdge, double y, double hGap, double vGap)
    {
        Size nodeSize = MeasureNodeSize(node);
        node.X = rightEdge - nodeSize.Width;
        node.Y = y;
        Rect bounds = new(new Point(node.X, node.Y), nodeSize);

        double childRightEdge = node.X - hGap;
        double nextChildY = node.Y + nodeSize.Height + vGap;
        foreach (MindMapNode child in GetLayoutChildren(node))
        {
            Rect childBounds = ArrangeNodeDownLeft(child, childRightEdge, nextChildY, hGap, vGap);
            bounds.Union(childBounds);
            nextChildY = childBounds.Bottom + vGap;
        }

        return bounds;
    }

    private void ArrangeNodeRadial(MindMapNode root, Point origin, double hGap, double vGap)
    {
        root.X = origin.X;
        root.Y = origin.Y;
        List<MindMapNode> layoutChildren = GetLayoutChildren(root);
        List<MindMapNode>[] groups = [[], [], [], []];
        for (int index = 0; index < layoutChildren.Count; index++)
        {
            groups[index % groups.Length].Add(layoutChildren[index]);
        }

        ArrangeRadialGroup(root, groups[0], "Right", hGap, vGap);
        ArrangeRadialGroup(root, groups[1], "Down", hGap, vGap);
        ArrangeRadialGroup(root, groups[2], "Left", hGap, vGap);
        ArrangeRadialGroup(root, groups[3], "Up", hGap, vGap);
    }

    private void ArrangeRadialGroup(MindMapNode root, List<MindMapNode> children, string direction, double hGap, double vGap)
    {
        if (children.Count == 0)
        {
            return;
        }

        double next = 0;
        foreach (MindMapNode child in children)
        {
            switch (direction)
            {
                case "Left":
                    ArrangeNodeLeft(child, 0, ref next, hGap, vGap);
                    break;
                case "Down":
                    ArrangeNodeDown(child, 0, ref next, hGap, vGap);
                    break;
                case "Up":
                    ArrangeNodeUp(child, 0, ref next, hGap, vGap);
                    break;
                default:
                    ArrangeNodeRight(child, 0, ref next, hGap, vGap);
                    break;
            }
        }

        List<MindMapNode> nodes = children.SelectMany(NodeLayoutHierarchy.TraverseStructuralNodes).ToList();
        Rect bounds = GetNodeBounds(nodes);
        Size rootSize = MeasureNodeSize(root);
        Point rootCenter = new(root.X + rootSize.Width / 2, root.Y + rootSize.Height / 2);
        double shiftX;
        double shiftY;

        switch (direction)
        {
            case "Left":
                shiftX = root.X - hGap - bounds.Right;
                shiftY = rootCenter.Y - (bounds.Top + bounds.Height / 2);
                break;
            case "Down":
                shiftX = rootCenter.X - (bounds.Left + bounds.Width / 2);
                shiftY = root.Y + rootSize.Height + vGap - bounds.Top;
                break;
            case "Up":
                shiftX = rootCenter.X - (bounds.Left + bounds.Width / 2);
                shiftY = root.Y - vGap - bounds.Bottom;
                break;
            default:
                shiftX = root.X + rootSize.Width + hGap - bounds.Left;
                shiftY = rootCenter.Y - (bounds.Top + bounds.Height / 2);
                break;
        }

        foreach (MindMapNode child in children)
        {
            ShiftSubtree(child, shiftX, shiftY);
        }
    }

    private static void ShiftSubtreeY(MindMapNode node, double offsetY)
    {
        node.Y += offsetY;

        foreach (MindMapNode child in node.Children)
        {
            ShiftSubtreeY(child, offsetY);
        }
    }

    private static void ShiftSubtree(MindMapNode node, double offsetX, double offsetY)
    {
        node.X += offsetX;
        node.Y += offsetY;

        foreach (MindMapNode child in node.Children)
        {
            ShiftSubtree(child, offsetX, offsetY);
        }
    }

    private void BeginTextEditLayoutSession(MindMapNode node)
    {
        if (_textEditLayoutNodeId == node.Id)
        {
            return;
        }

        _textEditLayoutNodeId = node.Id;
        _textEditLayoutBaseSize = MeasureNodeSize(node);
        _textEditLayoutBasePositions.Clear();
        foreach (MindMapNode candidate in Traverse(CurrentDocument.Root))
        {
            _textEditLayoutBasePositions[candidate.Id] = new Point(candidate.X, candidate.Y);
        }
    }

    private void RestoreTextEditLayoutBaseline()
    {
        foreach (MindMapNode node in Traverse(CurrentDocument.Root))
        {
            if (_textEditLayoutBasePositions.TryGetValue(node.Id, out Point position))
            {
                node.X = position.X;
                node.Y = position.Y;
            }
        }
    }

    private void EndTextEditLayoutSession()
    {
        _textEditLayoutNodeId = null;
        _textEditLayoutBasePositions.Clear();
    }

    private void ApplyNodeSizeChangeLayout(MindMapNode node, Size oldSize, Size newSize)
    {
        string direction = GetNodeLayoutDirection(node);
        NodeLayoutOffset offset = NodeLayoutGeometry.GetAnchoredOffset(
            direction,
            new NodeLayoutSize(oldSize.Width, oldSize.Height),
            new NodeLayoutSize(newSize.Width, newSize.Height));
        ShiftSubtree(node, offset.X, offset.Y);

        EnsureChildSubtreesOutsideNode(node, newSize);
        ResolveEditedNodeSiblingCollisions(node, direction);
    }

    private string GetNodeLayoutDirection(MindMapNode node)
    {
        if (IsVisibleRoot(node) || node.Parent is null)
        {
            return "Root";
        }

        MindMapNode parent = node.Parent;
        List<MindMapNode> siblings = GetLayoutChildren(parent);
        int childIndex = siblings.IndexOf(node);
        if (childIndex >= 0)
        {
            return GetEffectiveChildDirection(GetVisibleRoot(node), parent, childIndex);
        }

        Point parentCenter = GetNodeCenter(parent);
        Point nodeCenter = GetNodeCenter(node);
        double deltaX = nodeCenter.X - parentCenter.X;
        double deltaY = nodeCenter.Y - parentCenter.Y;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            return deltaX < 0 ? "Left" : "Right";
        }

        return deltaY < 0 ? "Up" : "Down";
    }

    private void EnsureChildSubtreesOutsideNode(MindMapNode parent, Size parentSize)
    {
        List<MindMapNode> children = GetLayoutChildren(parent);
        if (children.Count == 0)
        {
            return;
        }

        MindMapNode root = GetVisibleRoot(parent);
        NodeLayoutRect parentRect = ToLayoutRect(parent, parentSize);
        for (int index = 0; index < children.Count; index++)
        {
            MindMapNode child = children[index];
            Size childSize = MeasureNodeSize(child);
            NodeLayoutRect childRect = ToLayoutRect(child, childSize);
            string direction = GetEffectiveChildDirection(root, parent, index);
            double shiftX = 0;
            double shiftY = 0;

            switch (direction)
            {
                case "Left":
                case "DownLeft":
                    shiftX = Math.Min(0, parentRect.Left - root.HorizontalGap - childRect.Right);
                    break;
                case "Down":
                    shiftY = Math.Max(0, parentRect.Bottom + root.VerticalGap - childRect.Top);
                    break;
                case "Up":
                    shiftY = Math.Min(0, parentRect.Top - root.VerticalGap - childRect.Bottom);
                    break;
                default:
                    shiftX = Math.Max(0, parentRect.Right + root.HorizontalGap - childRect.Left);
                    break;
            }

            ShiftSubtree(child, shiftX, shiftY);
        }
    }

    private void ResolveEditedNodeSiblingCollisions(MindMapNode node, string direction)
    {
        if (node.Parent is not MindMapNode parent || direction == "Root")
        {
            return;
        }

        MindMapNode root = GetVisibleRoot(node);
        List<MindMapNode> layoutChildren = GetLayoutChildren(parent);
        List<MindMapNode> siblings = layoutChildren
            .Where((candidate, index) =>
                GetEffectiveChildDirection(root, parent, index) == direction)
            .OrderBy(candidate => GetSiblingAxisCenter(candidate, direction))
            .ToList();
        int editedIndex = siblings.IndexOf(node);
        if (editedIndex < 0)
        {
            return;
        }

        NodeLayoutAxis axis = direction is "Down" or "Up"
            ? NodeLayoutAxis.Horizontal
            : NodeLayoutAxis.Vertical;
        double gap = axis == NodeLayoutAxis.Horizontal ? root.HorizontalGap : root.VerticalGap;

        NodeLayoutRect fixedRect = GetDisplayedSubtreeLayoutRect(node);
        for (int index = editedIndex - 1; index >= 0; index--)
        {
            MindMapNode sibling = siblings[index];
            NodeLayoutRect movingRect = GetDisplayedSubtreeLayoutRect(sibling);
            double separation = NodeLayoutGeometry.GetBackwardSeparation(fixedRect, movingRect, gap, axis);
            ShiftSubtreeAlongAxis(sibling, separation, axis);
            fixedRect = OffsetLayoutRect(movingRect, separation, axis);
        }

        fixedRect = GetDisplayedSubtreeLayoutRect(node);
        for (int index = editedIndex + 1; index < siblings.Count; index++)
        {
            MindMapNode sibling = siblings[index];
            NodeLayoutRect movingRect = GetDisplayedSubtreeLayoutRect(sibling);
            double separation = NodeLayoutGeometry.GetForwardSeparation(fixedRect, movingRect, gap, axis);
            ShiftSubtreeAlongAxis(sibling, separation, axis);
            fixedRect = OffsetLayoutRect(movingRect, separation, axis);
        }
    }

    private double GetSiblingAxisCenter(MindMapNode node, string direction)
    {
        NodeLayoutRect bounds = GetDisplayedSubtreeLayoutRect(node);
        return direction is "Down" or "Up"
            ? (bounds.Left + bounds.Right) / 2
            : (bounds.Top + bounds.Bottom) / 2;
    }

    private NodeLayoutRect GetDisplayedSubtreeLayoutRect(MindMapNode node)
    {
        Rect bounds = GetNodeBounds(TraverseDisplayed(node).ToList());
        return new NodeLayoutRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    private static NodeLayoutRect ToLayoutRect(MindMapNode node, Size size)
    {
        return new NodeLayoutRect(node.X, node.Y, node.X + size.Width, node.Y + size.Height);
    }

    private static NodeLayoutRect OffsetLayoutRect(NodeLayoutRect rect, double offset, NodeLayoutAxis axis)
    {
        return axis == NodeLayoutAxis.Horizontal
            ? rect with { Left = rect.Left + offset, Right = rect.Right + offset }
            : rect with { Top = rect.Top + offset, Bottom = rect.Bottom + offset };
    }

    private static void ShiftSubtreeAlongAxis(MindMapNode node, double offset, NodeLayoutAxis axis)
    {
        if (Math.Abs(offset) < 0.001)
        {
            return;
        }

        if (axis == NodeLayoutAxis.Horizontal)
        {
            ShiftSubtreeX(node, offset);
        }
        else
        {
            ShiftSubtreeY(node, offset);
        }
    }

    private void SnapNodeToGrid(MindMapNode node)
    {
        CanvasSettings settings = CurrentDocument.CanvasSettings;
        if (!settings.SnapToGrid || settings.GridSize <= 0)
        {
            return;
        }

        double gridSize = settings.GridSize;
        node.X = Math.Round(node.X / gridSize) * gridSize;
        node.Y = Math.Round(node.Y / gridSize) * gridSize;
    }

    private void SnapDraggedNodesToGrid()
    {
        if (_draggedNode is null)
        {
            return;
        }

        CanvasSettings settings = CurrentDocument.CanvasSettings;
        if (!settings.SnapToGrid || settings.GridSize <= 0)
        {
            return;
        }

        double originalX = _draggedNode.X;
        double originalY = _draggedNode.Y;
        SnapNodeToGrid(_draggedNode);
        double offsetX = _draggedNode.X - originalX;
        double offsetY = _draggedNode.Y - originalY;

        foreach (MindMapNode node in _draggedNodes.Where(node => !ReferenceEquals(node, _draggedNode)))
        {
            node.X += offsetX;
            node.Y += offsetY;
        }
    }

    private static void ShiftSubtreeX(MindMapNode node, double offsetX)
    {
        node.X += offsetX;

        foreach (MindMapNode child in node.Children)
        {
            ShiftSubtreeX(child, offsetX);
        }
    }

    private static double GetNodeContentWidth(Size nodeSize)
    {
        return Math.Max(0, nodeSize.Width - NodeHorizontalPadding);
    }

    private static void ApplyTitleControlWidth(Border border, Size nodeSize)
    {
        if (border.Child is not FrameworkElement titleControl)
        {
            return;
        }

        double contentWidth = GetNodeContentWidth(nodeSize);
        titleControl.Width = contentWidth;
        titleControl.MaxWidth = contentWidth;
    }

    private static double GetConnectorBend(Point start, Point end)
    {
        double distance = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        return Math.Clamp(distance * 0.45, 40, 140);
    }

    private static PathGeometry CreateConnectorGeometry(Point start, Point end, string connectorShape)
    {
        PathFigure figure = new() { StartPoint = start };
        switch (connectorShape)
        {
            case "Straight":
                figure.Segments.Add(new LineSegment(end, true));
                break;
            case "Elbow":
                if (Math.Abs(end.Y - start.Y) > Math.Abs(end.X - start.X))
                {
                    double midY = (start.Y + end.Y) / 2;
                    figure.Segments.Add(new LineSegment(new Point(start.X, midY), true));
                    figure.Segments.Add(new LineSegment(new Point(end.X, midY), true));
                }
                else
                {
                    double midX = (start.X + end.X) / 2;
                    figure.Segments.Add(new LineSegment(new Point(midX, start.Y), true));
                    figure.Segments.Add(new LineSegment(new Point(midX, end.Y), true));
                }

                figure.Segments.Add(new LineSegment(end, true));
                break;
            case "AngleBracket":
                AddBracketSegments(figure, start, end, useSquareCorners: false);
                figure.Segments.Add(new LineSegment(end, true));
                break;
            case "SquareBracket":
                AddBracketSegments(figure, start, end, useSquareCorners: true);
                figure.Segments.Add(new LineSegment(end, true));
                break;
            default:
                double bend = GetConnectorBend(start, end);
                if (Math.Abs(end.Y - start.Y) > Math.Abs(end.X - start.X))
                {
                    double signY = Math.Sign(end.Y - start.Y);
                    if (signY == 0)
                    {
                        signY = 1;
                    }

                    figure.Segments.Add(new BezierSegment(
                        new Point(start.X, start.Y + bend * signY),
                        new Point(end.X, end.Y - bend * signY),
                        end,
                        true));
                }
                else
                {
                    double signX = Math.Sign(end.X - start.X);
                    if (signX == 0)
                    {
                        signX = 1;
                    }

                    figure.Segments.Add(new BezierSegment(
                        new Point(start.X + bend * signX, start.Y),
                        new Point(end.X - bend * signX, end.Y),
                        end,
                        true));
                }

                break;
        }

        return new PathGeometry([figure]);
    }

    private static void AddBracketSegments(PathFigure figure, Point start, Point end, bool useSquareCorners)
    {
        double deltaX = end.X - start.X;
        double deltaY = end.Y - start.Y;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            double direction = Math.Sign(deltaX);
            double shoulder = Math.Min(Math.Abs(deltaX) * 0.28, 56);
            Point firstShoulder = new(start.X + shoulder * direction, start.Y);
            Point lastShoulder = new(end.X - shoulder * direction, end.Y);
            figure.Segments.Add(new LineSegment(firstShoulder, true));

            if (useSquareCorners)
            {
                double midX = (firstShoulder.X + lastShoulder.X) / 2;
                figure.Segments.Add(new LineSegment(new Point(midX, start.Y), true));
                figure.Segments.Add(new LineSegment(new Point(midX, end.Y), true));
            }

            figure.Segments.Add(new LineSegment(lastShoulder, true));
            return;
        }

        double verticalDirection = Math.Sign(deltaY);
        double verticalShoulder = Math.Min(Math.Abs(deltaY) * 0.28, 56);
        Point firstVerticalShoulder = new(start.X, start.Y + verticalShoulder * verticalDirection);
        Point lastVerticalShoulder = new(end.X, end.Y - verticalShoulder * verticalDirection);
        figure.Segments.Add(new LineSegment(firstVerticalShoulder, true));

        if (useSquareCorners)
        {
            double midY = (firstVerticalShoulder.Y + lastVerticalShoulder.Y) / 2;
            figure.Segments.Add(new LineSegment(new Point(start.X, midY), true));
            figure.Segments.Add(new LineSegment(new Point(end.X, midY), true));
        }

        figure.Segments.Add(new LineSegment(lastVerticalShoulder, true));
    }

    private static DoubleCollection GetConnectorDashArray(string dashStyle)
    {
        return dashStyle switch
        {
            "Dash" => [6, 4],
            "Dot" => [1, 3],
            _ => []
        };
    }

    private void DrawConnectorArrowheads(Point start, Point end, string arrowStyle, Brush brush, double thickness, bool insertBehindNodes)
    {
        string normalizedArrowStyle = NormalizeArrowStyle(arrowStyle);
        if (normalizedArrowStyle is "None")
        {
            return;
        }

        Vector forward = end - start;
        if (forward.Length < 0.1)
        {
            return;
        }

        if (normalizedArrowStyle is "Start" or "Both")
        {
            AddArrowhead(start, -forward, brush, thickness, insertBehindNodes);
        }

        if (normalizedArrowStyle is "End" or "Both")
        {
            AddArrowhead(end, forward, brush, thickness, insertBehindNodes);
        }
    }

    private void AddArrowhead(Point tip, Vector direction, Brush brush, double thickness, bool insertBehindNodes)
    {
        if (direction.Length < 0.1)
        {
            return;
        }

        direction.Normalize();
        Vector perpendicular = new(-direction.Y, direction.X);
        double arrowLength = Math.Clamp(8 + thickness * 2, 10, 18);
        double arrowWidth = Math.Clamp(5 + thickness * 1.4, 7, 14);
        Point baseCenter = tip - direction * arrowLength;

        Polygon arrow = new()
        {
            Points = [tip, baseCenter + perpendicular * (arrowWidth / 2), baseCenter - perpendicular * (arrowWidth / 2)],
            Fill = brush,
            Stroke = brush,
            StrokeThickness = 0,
            IsHitTestVisible = false
        };

        if (insertBehindNodes)
        {
            MindMapCanvas.Children.Insert(Math.Min(1, MindMapCanvas.Children.Count), arrow);
        }
        else
        {
            MindMapCanvas.Children.Add(arrow);
        }
    }

    private static FontFamily GetNodeFontFamily(MindMapNode node)
    {
        return new FontFamily(string.IsNullOrWhiteSpace(node.FontFamily) ? "Microsoft YaHei UI" : node.FontFamily);
    }

    private static FontWeight GetNodeFontWeight(MindMapNode node)
    {
        return node.FontWeight switch
        {
            "Normal" => FontWeights.Normal,
            "Bold" => FontWeights.Bold,
            _ => FontWeights.SemiBold
        };
    }

    private static System.Windows.FontStyle GetNodeFontStyle(MindMapNode node)
    {
        return string.Equals(node.FontStyle, "Italic", StringComparison.OrdinalIgnoreCase)
            ? FontStyles.Italic
            : FontStyles.Normal;
    }

    private static TextAlignment GetNodeTextAlignment(MindMapNode node)
    {
        return node.TextAlignment switch
        {
            "Left" => TextAlignment.Left,
            "Right" => TextAlignment.Right,
            _ => TextAlignment.Center
        };
    }

    private Size MeasureNodeSize(MindMapNode node)
    {
        string title = string.IsNullOrWhiteSpace(node.Title) ? Localization.T("Untitled") : node.Title.Trim();
        double textMaxWidth = MaxNodeWidth - NodeHorizontalPadding;
        FormattedText formattedText = new(
            title,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(GetNodeFontFamily(node), GetNodeFontStyle(node), GetNodeFontWeight(node), FontStretches.Normal),
            node.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = textMaxWidth
        };

        double width = Math.Clamp(formattedText.WidthIncludingTrailingWhitespace + NodeHorizontalPadding, MinNodeWidth, MaxNodeWidth);
        double height = Math.Max(MinNodeHeight, formattedText.Height + NodeVerticalPadding);
        return new Size(Math.Ceiling(width), Math.Ceiling(height));
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || _isUpdatingNodeProperties)
        {
            return;
        }

        string[] parts = tag.Split('|', 2);
        if (parts.Length != 2)
        {
            return;
        }

        ApplyColorChange(parts[0], parts[1]);
    }

    private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string propertyName } || _isUpdatingNodeProperties)
        {
            return;
        }

        _activeColorProperty = propertyName;
        _pendingColor = ParseColorValue(GetSelectedColor(propertyName), Colors.Black);
        PopupColorTitle.Text = GetColorPropertyDisplayName(propertyName);
        ColorPickerPopup.PlacementTarget = (Button)sender;
        UpdateColorPickerInputs(_pendingColor);
        ColorPickerPopup.IsOpen = true;
    }

    private void ColorPickerPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string colorText })
        {
            SetPendingColor(ParseColorValue(colorText, _pendingColor));
        }
    }

    private void ColorHexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingColorPicker)
        {
            return;
        }

        if (TryParseHexColor(ColorHexBox.Text, out Color color))
        {
            SetPendingColor(color);
        }
    }

    private void ColorRgbBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingColorPicker)
        {
            return;
        }

        if (TryParseByte(ColorRedBox.Text, out byte red) &&
            TryParseByte(ColorGreenBox.Text, out byte green) &&
            TryParseByte(ColorBlueBox.Text, out byte blue))
        {
            SetPendingColor(Color.FromRgb(red, green, blue));
        }
    }

    private void ColorRgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingColorPicker)
        {
            return;
        }

        SetPendingColor(Color.FromRgb(
            (byte)Math.Round(ColorRedSlider.Value),
            (byte)Math.Round(ColorGreenSlider.Value),
            (byte)Math.Round(ColorBlueSlider.Value)));
    }

    private void ColorPickerOk_Click(object sender, RoutedEventArgs e)
    {
        if (_activeColorProperty is null)
        {
            return;
        }

        ApplyColorChange(_activeColorProperty, ColorToHex(_pendingColor));
        ColorPickerPopup.IsOpen = false;
    }

    private void ColorPickerCancel_Click(object sender, RoutedEventArgs e)
    {
        ColorPickerPopup.IsOpen = false;
    }

    private void SetPendingColor(Color color)
    {
        _pendingColor = color;
        UpdateColorPickerInputs(color);
    }

    private void UpdateColorPickerInputs(Color color)
    {
        _isUpdatingColorPicker = true;
        string hex = ColorToHex(color);
        PopupColorPreview.Background = new SolidColorBrush(color);
        ColorHexBox.Text = hex;
        ColorRedBox.Text = color.R.ToString(CultureInfo.InvariantCulture);
        ColorGreenBox.Text = color.G.ToString(CultureInfo.InvariantCulture);
        ColorBlueBox.Text = color.B.ToString(CultureInfo.InvariantCulture);
        ColorRedSlider.Value = color.R;
        ColorGreenSlider.Value = color.G;
        ColorBlueSlider.Value = color.B;
        _isUpdatingColorPicker = false;
    }

    private static string GetColorPropertyDisplayName(string propertyName)
    {
        return propertyName switch
        {
            "FillColor" => Localization.T("FillColor"),
            "BorderColor" => Localization.T("BorderColor"),
            "TextColor" => Localization.T("TextColor"),
            "ConnectorColor" => Localization.T("LineColor"),
            "ConnectionColor" => Localization.T("LineColor"),
            "CanvasBackgroundColor" => Localization.T("BackgroundColor"),
            "GridColor" => Localization.T("GridColor"),
            _ => Localization.T("Color")
        };
    }

    private string GetSelectedColor(string propertyName)
    {
        MindMapNode? node = _selectedNode;
        return propertyName switch
        {
            "FillColor" => node?.FillColor ?? "#FFFFFF",
            "BorderColor" => node?.BorderColor ?? "#CBD5E1",
            "TextColor" => node?.TextColor ?? "#0F172A",
            "ConnectorColor" => node?.ConnectorColor ?? "#94A3B8",
            "ConnectionColor" => SelectedConnection?.Color ?? "#2563EB",
            "CanvasBackgroundColor" => CurrentDocument.CanvasSettings.CanvasBackgroundColor,
            "GridColor" => CurrentDocument.CanvasSettings.GridColor,
            _ => "#000000"
        };
    }

    private static Color ParseColorValue(string colorText, Color fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(colorText)!;
        }
        catch
        {
            return fallback;
        }
    }

    private static bool TryParseHexColor(string text, out Color color)
    {
        color = Colors.Black;
        string value = text.Trim();
        if (!value.StartsWith('#'))
        {
            value = $"#{value}";
        }

        if (value.Length != 7)
        {
            return false;
        }

        try
        {
            color = (Color)ColorConverter.ConvertFromString(value)!;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseByte(string text, out byte value)
    {
        value = 0;
        return byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void ApplyColorChange(string propertyName, string color)
    {
        if (propertyName is "CanvasBackgroundColor" or "GridColor")
        {
            ApplyCanvasSettingChange(settings =>
            {
                if (propertyName == "CanvasBackgroundColor")
                {
                    settings.CanvasBackgroundColor = color;
                }
                else
                {
                    settings.GridColor = color;
                }
            });
            return;
        }

        if (propertyName == "ConnectionColor")
        {
            ApplyConnectionStyleChange(connection => connection.Color = color);
            return;
        }

        ApplyNodeStyleChange(node =>
        {
            switch (propertyName)
            {
                case "FillColor":
                    node.FillColor = color;
                    break;
                case "BorderColor":
                    node.BorderColor = color;
                    break;
                case "TextColor":
                    node.TextColor = color;
                    break;
                case "ConnectorColor":
                    node.ConnectorColor = color;
                    break;
            }
        });
    }

    private void CanvasSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingNodeProperties)
        {
            return;
        }

        ApplyCanvasSettingChange(settings =>
        {
            settings.ShowGrid = ShowGridCheckBox.IsChecked == true;
            settings.SnapToGrid = SnapToGridCheckBox.IsChecked == true;
            settings.GridSize = GetSelectedComboNumber(GridSizeComboBox, settings.GridSize);
        });
    }

    private void RootLayoutSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingNodeProperties || _selectedNode is null || !IsVisibleRoot(_selectedNode))
        {
            return;
        }

        PushUndoSnapshot();
        _selectedNode.LayoutDirection = NormalizeLayoutDirection((LayoutDirectionComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString());
        _selectedNode.HorizontalGap = GetSelectedComboNumber(HorizontalGapComboBox, _selectedNode.HorizontalGap);
        _selectedNode.VerticalGap = GetSelectedComboNumber(VerticalGapComboBox, _selectedNode.VerticalGap);
        NormalizeNodeStyle(_selectedNode);
        AutoLayoutMap();
        MarkCurrentDocumentDirty();
        RenderCanvas();
        StatusText.Text = Localization.T("RootLayoutUpdated");
    }

    private void ApplyCanvasSettingChange(Action<CanvasSettings> apply)
    {
        PushUndoSnapshot();
        apply(CurrentDocument.CanvasSettings);
        NormalizeCanvasSettings(CurrentDocument.CanvasSettings);
        MarkCurrentDocumentDirty();
        RenderCanvas();
    }

    private static double GetSelectedComboNumber(ComboBox comboBox, double fallback)
    {
        return comboBox.SelectedItem is ComboBoxItem { Tag: string text } &&
            double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out double value)
            ? value
            : fallback;
    }

    private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || FontFamilyComboBox.SelectedItem is not ComboBoxItem { Tag: string fontFamily })
        {
            return;
        }

        ApplyNodeStyleChange(node => node.FontFamily = fontFamily);
    }

    private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || FontSizeComboBox.SelectedItem is not ComboBoxItem { Tag: string fontSizeText })
        {
            return;
        }

        if (!double.TryParse(fontSizeText, NumberStyles.Number, CultureInfo.InvariantCulture, out double fontSize))
        {
            return;
        }

        ApplyNodeStyleChange(node => node.FontSize = fontSize);
    }

    private void FontWeightComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || FontWeightComboBox.SelectedItem is not ComboBoxItem { Tag: string fontWeight })
        {
            return;
        }

        ApplyNodeStyleChange(node => node.FontWeight = fontWeight);
    }

    private void FontStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || FontStyleComboBox.SelectedItem is not ComboBoxItem { Tag: string fontStyle })
        {
            return;
        }

        ApplyNodeStyleChange(node => node.FontStyle = fontStyle);
    }

    private void TextAlignmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || TextAlignmentComboBox.SelectedItem is not ComboBoxItem { Tag: string textAlignment })
        {
            return;
        }

        ApplyNodeStyleChange(node => node.TextAlignment = textAlignment);
    }

    private void ShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || ShapeComboBox.SelectedItem is not ComboBoxItem { Tag: string shape })
        {
            return;
        }

        ApplyNodeStyleChange(node => node.Shape = shape);
    }

    private void BorderThicknessComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || BorderThicknessComboBox.SelectedItem is not ComboBoxItem { Tag: string thicknessText })
        {
            return;
        }

        if (!double.TryParse(thicknessText, NumberStyles.Number, CultureInfo.InvariantCulture, out double thickness))
        {
            return;
        }

        ApplyNodeStyleChange(node => node.BorderThickness = thickness);
    }

    private void ConnectorThicknessComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || ConnectorThicknessComboBox.SelectedItem is not ComboBoxItem { Tag: string thicknessText })
        {
            return;
        }

        if (!double.TryParse(thicknessText, NumberStyles.Number, CultureInfo.InvariantCulture, out double thickness))
        {
            return;
        }

        ApplyNodeStyleChange(node => node.ConnectorThickness = thickness);
    }

    private void ConnectorDashStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || ConnectorDashStyleComboBox.SelectedItem is not ComboBoxItem { Tag: string dashStyle })
        {
            return;
        }

        ApplyNodeStyleChange(node => node.ConnectorDashStyle = dashStyle);
    }

    private void ConnectorShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || ConnectorShapeComboBox.SelectedItem is not ComboBoxItem { Tag: string connectorShape })
        {
            return;
        }

        ApplyNodeStyleChange(node => node.ConnectorShape = connectorShape);
    }

    private void ConnectorArrowStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || ConnectorArrowStyleComboBox.SelectedItem is not ComboBoxItem { Tag: string arrowStyle })
        {
            return;
        }

        ApplyNodeStyleChange(node => node.ConnectorArrowStyle = NormalizeArrowStyle(arrowStyle));
    }

    private void ConnectionTitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInspector || SelectedConnection is null)
        {
            return;
        }

        string title = string.IsNullOrWhiteSpace(ConnectionTitleBox.Text) ? Localization.T("Relationship") : ConnectionTitleBox.Text.Trim();
        ApplyConnectionStyleChange(connection => connection.Title = title);
    }

    private void ConnectionThicknessComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || ConnectionThicknessComboBox.SelectedItem is not ComboBoxItem { Tag: string thicknessText })
        {
            return;
        }

        if (!double.TryParse(thicknessText, NumberStyles.Number, CultureInfo.InvariantCulture, out double thickness))
        {
            return;
        }

        ApplyConnectionStyleChange(connection => connection.Thickness = thickness);
    }

    private void ConnectionDashStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || ConnectionDashStyleComboBox.SelectedItem is not ComboBoxItem { Tag: string dashStyle })
        {
            return;
        }

        ApplyConnectionStyleChange(connection => connection.DashStyle = dashStyle);
    }

    private void ConnectionShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || ConnectionShapeComboBox.SelectedItem is not ComboBoxItem { Tag: string connectorShape })
        {
            return;
        }

        ApplyConnectionStyleChange(connection => connection.Shape = connectorShape);
    }

    private void ConnectionArrowStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingNodeProperties || ConnectionArrowStyleComboBox.SelectedItem is not ComboBoxItem { Tag: string arrowStyle })
        {
            return;
        }

        ApplyConnectionStyleChange(connection => connection.ArrowStyle = NormalizeArrowStyle(arrowStyle));
    }

    private void ApplyConnectionStyleChange(Action<MindMapConnection> apply)
    {
        if (SelectedConnection is not MindMapConnection connection)
        {
            return;
        }

        PushUndoSnapshot();
        apply(connection);
        NormalizeConnectionStyle(connection);
        MarkCurrentDocumentDirty();
        RenderCanvas();
    }

    private void ApplyNodeStyleChange(Action<MindMapNode> apply)
    {
        List<MindMapNode> targets = GetStyleTargetNodes();
        if (targets.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        EndTextEditLayoutSession();
        Dictionary<Guid, Size> oldSizes = targets.ToDictionary(node => node.Id, MeasureNodeSize);
        foreach (MindMapNode node in targets)
        {
            apply(node);
            NormalizeNodeStyle(node);
        }

        foreach (MindMapNode node in targets)
        {
            Size oldSize = oldSizes[node.Id];
            Size newSize = MeasureNodeSize(node);
            if (Math.Abs(oldSize.Width - newSize.Width) >= 0.001 ||
                Math.Abs(oldSize.Height - newSize.Height) >= 0.001)
            {
                ApplyNodeSizeChangeLayout(node, oldSize, newSize);
            }
        }

        MarkCurrentDocumentDirty();
        RenderCanvas();
    }

    private List<MindMapNode> GetStyleTargetNodes()
    {
        List<MindMapNode> nodes = TraverseVisibleNodes()
            .Where(node => _selectedNodeIds.Contains(node.Id))
            .ToList();

        if (nodes.Count == 0 && _selectedNode is not null)
        {
            nodes.Add(_selectedNode);
        }

        return nodes;
    }

    private static void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
    {
        foreach (object item in comboBox.Items)
        {
            if (item is ComboBoxItem comboBoxItem &&
                string.Equals(comboBoxItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = comboBoxItem;
                return;
            }
        }

        comboBox.SelectedIndex = -1;
    }

    private void ResetView()
    {
        CanvasScale.ScaleX = 1;
        CanvasScale.ScaleY = 1;
        CanvasTranslate.X = 0;
        CanvasTranslate.Y = 0;
        ApplyCanvasAppearance();
    }

    private void UpdateInspector()
    {
        _isUpdatingInspector = true;
        _isUpdatingNodeProperties = true;
        NodeTitleBox.IsEnabled = _selectedNode is not null;
        NodeTitleBox.Text = _selectedNode?.Title ?? string.Empty;
        SelectComboBoxItemByTag(FontFamilyComboBox, _selectedNode?.FontFamily ?? "Microsoft YaHei UI");
        SelectComboBoxItemByTag(FontSizeComboBox, (_selectedNode?.FontSize ?? NodeFontSize).ToString(CultureInfo.InvariantCulture));
        SelectComboBoxItemByTag(FontWeightComboBox, _selectedNode?.FontWeight ?? "SemiBold");
        SelectComboBoxItemByTag(FontStyleComboBox, _selectedNode?.FontStyle ?? "Normal");
        SelectComboBoxItemByTag(TextAlignmentComboBox, _selectedNode?.TextAlignment ?? "Center");
        SelectComboBoxItemByTag(ShapeComboBox, _selectedNode?.Shape ?? "RoundedRectangle");
        SelectComboBoxItemByTag(BorderThicknessComboBox, (_selectedNode?.BorderThickness ?? 1).ToString(CultureInfo.InvariantCulture));
        SelectComboBoxItemByTag(ConnectorThicknessComboBox, (_selectedNode?.ConnectorThickness ?? 2).ToString(CultureInfo.InvariantCulture));
        SelectComboBoxItemByTag(ConnectorDashStyleComboBox, _selectedNode?.ConnectorDashStyle ?? "Solid");
        SelectComboBoxItemByTag(ConnectorShapeComboBox, _selectedNode?.ConnectorShape ?? "Curve");
        SelectComboBoxItemByTag(ConnectorArrowStyleComboBox, _selectedNode?.ConnectorArrowStyle ?? "None");
        MindMapConnection? selectedConnection = SelectedConnection;
        ConnectionTitleBox.IsEnabled = selectedConnection is not null;
        ConnectionTitleBox.Text = selectedConnection?.Title ?? string.Empty;
        SelectComboBoxItemByTag(ConnectionThicknessComboBox, (selectedConnection?.Thickness ?? 2).ToString(CultureInfo.InvariantCulture));
        SelectComboBoxItemByTag(ConnectionDashStyleComboBox, selectedConnection?.DashStyle ?? "Solid");
        SelectComboBoxItemByTag(ConnectionShapeComboBox, selectedConnection?.Shape ?? "Curve");
        SelectComboBoxItemByTag(ConnectionArrowStyleComboBox, selectedConnection?.ArrowStyle ?? "None");
        CanvasSettings canvasSettings = CurrentDocument.CanvasSettings;
        NormalizeCanvasSettings(canvasSettings);
        ShowGridCheckBox.IsChecked = canvasSettings.ShowGrid;
        SnapToGridCheckBox.IsChecked = canvasSettings.SnapToGrid;
        SelectComboBoxItemByTag(GridSizeComboBox, canvasSettings.GridSize.ToString(CultureInfo.InvariantCulture));
        MindMapNode? selectedRoot = _selectedNode is not null && IsVisibleRoot(_selectedNode) ? _selectedNode : null;
        SelectComboBoxItemByTag(LayoutDirectionComboBox, selectedRoot?.LayoutDirection ?? "Right");
        SelectComboBoxItemByTag(HorizontalGapComboBox, (selectedRoot?.HorizontalGap ?? ParentChildGap).ToString(CultureInfo.InvariantCulture));
        SelectComboBoxItemByTag(VerticalGapComboBox, (selectedRoot?.VerticalGap ?? VerticalGap).ToString(CultureInfo.InvariantCulture));
        ZoomText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("Zoom"), CanvasScale.ScaleX);
        bool hasSelection = _selectedNode is not null;
        bool hasConnectionSelection = selectedConnection is not null;
        bool isRootSelected = _selectedNode is not null && IsVisibleRoot(_selectedNode);
        NodePropertiesPanel.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        RootLayoutPropertiesPanel.Visibility = isRootSelected ? Visibility.Visible : Visibility.Collapsed;
        NodeStylePropertiesPanel.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        ConnectionPropertiesPanel.Visibility = hasConnectionSelection ? Visibility.Visible : Visibility.Collapsed;
        CanvasPropertiesPanel.Visibility = hasSelection || hasConnectionSelection ? Visibility.Collapsed : Visibility.Visible;
        FontFamilyComboBox.IsEnabled = hasSelection;
        FontSizeComboBox.IsEnabled = hasSelection;
        FontWeightComboBox.IsEnabled = hasSelection;
        FontStyleComboBox.IsEnabled = hasSelection;
        TextAlignmentComboBox.IsEnabled = hasSelection;
        ShapeComboBox.IsEnabled = hasSelection;
        BorderThicknessComboBox.IsEnabled = hasSelection;
        ConnectorThicknessComboBox.IsEnabled = hasSelection;
        ConnectorDashStyleComboBox.IsEnabled = hasSelection;
        ConnectorShapeComboBox.IsEnabled = hasSelection;
        ConnectorArrowStyleComboBox.IsEnabled = hasSelection;
        ConnectionThicknessComboBox.IsEnabled = hasConnectionSelection;
        ConnectionDashStyleComboBox.IsEnabled = hasConnectionSelection;
        ConnectionShapeComboBox.IsEnabled = hasConnectionSelection;
        ConnectionArrowStyleComboBox.IsEnabled = hasConnectionSelection;
        LayoutDirectionComboBox.IsEnabled = isRootSelected;
        HorizontalGapComboBox.IsEnabled = isRootSelected;
        VerticalGapComboBox.IsEnabled = isRootSelected;
        _isUpdatingNodeProperties = false;
        _isUpdatingInspector = false;
        AddSummaryToolbarButton.IsEnabled = _selectedNodeIds.Count >= 2;
        AddConnectionToolbarButton.IsEnabled = _selectedNode is not null || _selectedNodeIds.Count >= 2;
        UpdateCommandState();
    }

    private void UpdateCommandState()
    {
        if (_documents.Count == 0)
        {
            return;
        }

        bool canUndo = CurrentDocument.UndoStack.Count > 0;
        bool canRedo = CurrentDocument.RedoStack.Count > 0;
        UndoMenuItem.IsEnabled = canUndo;
        RedoMenuItem.IsEnabled = canRedo;
        UndoToolbarButton.IsEnabled = canUndo;
        RedoToolbarButton.IsEnabled = canRedo;
        ShowCurrentFileInExplorerMenuItem.IsEnabled = CurrentDocument.FilePath is not null;
        UpdateRecentFilesMenu();
    }

    private void UpdateStats()
    {
        int nodeCount = TraverseDisplayedNodes().Count();
        int maxDepth = VisibleRoots.Count == 0 ? 0 : VisibleRoots.Max(GetMaxDepth);
        int summaryCount = TraverseDisplayedNodes().Count(node => node.IsSummary);
        ZoomText.Text = string.Format(CultureInfo.CurrentCulture, Localization.T("Zoom"), CanvasScale.ScaleX);
        string currentSelection = _selectedNode?.Title ?? Localization.T("SelectionCanvas");
        if (SelectedConnection is MindMapConnection selectedConnection)
        {
            currentSelection = $"{Localization.T("SelectionConnection")}: {selectedConnection.Title}";
        }

        StatsText.Text =
            $"{Localization.T("NodeCount")}: {nodeCount}\n" +
            $"{Localization.T("SummaryNodeCount")}: {summaryCount}\n" +
            $"{Localization.T("Depth")}: {maxDepth}\n" +
            $"{Localization.T("CurrentSelection")}: {currentSelection}\n" +
            $"{Localization.T("MultiSelectionCount")}: {_selectedNodeIds.Count}";
    }

    private static int GetMaxDepth(MindMapNode node)
    {
        if (node.IsCollapsed || node.Children.Count == 0)
        {
            return 1;
        }

        return 1 + node.Children.Max(GetMaxDepth);
    }

    private bool IsVisibleRoot(MindMapNode node)
    {
        return node.Parent?.IsCanvasRoot == true;
    }

    private MindMapNode GetVisibleRoot(MindMapNode node)
    {
        MindMapNode current = node;
        while (current.Parent is not null && !current.Parent.IsCanvasRoot)
        {
            current = current.Parent;
        }

        return current;
    }

    private IEnumerable<MindMapNode> TraverseVisibleNodes()
    {
        return TraverseVisibleNodes(_root);
    }

    private IEnumerable<MindMapNode> TraverseDisplayedNodes()
    {
        return _root.IsCanvasRoot
            ? _root.Children.SelectMany(TraverseDisplayed)
            : TraverseDisplayed(_root);
    }

    private static IEnumerable<MindMapNode> TraverseDisplayed(MindMapNode node)
    {
        yield return node;
        if (node.IsCollapsed)
        {
            yield break;
        }

        foreach (MindMapNode child in node.Children)
        {
            foreach (MindMapNode descendant in TraverseDisplayed(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<MindMapNode> TraverseVisibleNodes(MindMapNode root)
    {
        if (!root.IsCanvasRoot)
        {
            return Traverse(root);
        }

        return root.Children.SelectMany(Traverse);
    }

    private static IEnumerable<MindMapNode> Traverse(MindMapNode node)
    {
        yield return node;

        foreach (MindMapNode child in node.Children)
        {
            foreach (MindMapNode descendant in Traverse(child))
            {
                yield return descendant;
            }
        }
    }
}
