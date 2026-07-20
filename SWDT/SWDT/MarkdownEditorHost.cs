using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace SWDT;

public sealed class MarkdownContentChangedEventArgs(string content) : EventArgs
{
    public string Content { get; } = content;
}

public sealed class MarkdownFocusChangedEventArgs(bool isFocused) : EventArgs
{
    public bool IsFocused { get; } = isFocused;
}

public sealed class MarkdownAssetCreatedEventArgs(MarkdownAsset asset) : EventArgs
{
    public MarkdownAsset Asset { get; } = asset;
}

public sealed class MarkdownEditorHost : Grid, IDisposable
{
    private const string EditorHostName = "swdt-editor.local";
    private const string AssetHostName = "swdt-assets.local";
    private readonly WebView2 _webView = new();
    private readonly Border _errorPanel;
    private readonly bool _compactToolbar;
    private readonly bool _compactLayout;
    private readonly Dictionary<string, TaskCompletionSource<string>> _flushRequests = [];
    private IReadOnlyList<MarkdownAsset> _assets = [];
    private string _content = string.Empty;
    private string _language = "en_US";
    private string _placeholder = string.Empty;
    private bool _darkTheme;
    private bool _outlineVisible;
    private bool _pageReady;
    private bool _initialized;
    private bool _initializing;
    private bool _disposed;

    public MarkdownEditorHost(bool compactToolbar = false, bool compactLayout = false)
    {
        _compactToolbar = compactToolbar;
        _compactLayout = compactLayout;
        _errorPanel = CreateErrorPanel();
        Children.Add(_webView);
        Children.Add(_errorPanel);
        Loaded += MarkdownEditorHost_Loaded;
    }

    public event EventHandler<MarkdownContentChangedEventArgs>? ContentChanged;

    public event EventHandler<MarkdownFocusChangedEventArgs>? EditorFocusChanged;

    public event EventHandler<MarkdownAssetCreatedEventArgs>? AssetCreated;

    public event EventHandler? UndoRequested;

    public event EventHandler? RedoRequested;

    public event EventHandler? SaveRequested;

    public event EventHandler? EscapeRequested;

    public event EventHandler<Exception>? InitializationFailed;

    public bool IsEditorFocused { get; private set; }

    public void LoadContent(
        string content,
        IReadOnlyList<MarkdownAsset> assets,
        bool darkTheme,
        string language,
        string placeholder)
    {
        _content = content ?? string.Empty;
        _assets = assets;
        _darkTheme = darkTheme;
        _language = language;
        _placeholder = placeholder;
        if (_pageReady)
        {
            if (_initialized)
            {
                Post(new { type = "setContent", value = _content });
                SetTheme(darkTheme);
            }
            else
            {
                SendInitialize();
            }
        }
    }

    public async Task<string> FlushAsync()
    {
        if (!_initialized || _webView.CoreWebView2 is null)
        {
            return _content;
        }

        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<string> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _flushRequests[requestId] = completion;
        Post(new { type = "flush", requestId });
        Task finished = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        _flushRequests.Remove(requestId);
        return finished == completion.Task ? await completion.Task : _content;
    }

    public void ExecuteCommand(MarkdownCommand command)
    {
        string name = command switch
        {
            MarkdownCommand.Heading => "heading",
            MarkdownCommand.Bold => "bold",
            MarkdownCommand.Italic => "italic",
            MarkdownCommand.Strike => "strike",
            MarkdownCommand.Quote => "quote",
            MarkdownCommand.AlignLeft => "alignLeft",
            MarkdownCommand.AlignCenter => "alignCenter",
            MarkdownCommand.AlignRight => "alignRight",
            MarkdownCommand.UnorderedList => "unorderedList",
            MarkdownCommand.OrderedList => "orderedList",
            MarkdownCommand.TaskList => "taskList",
            MarkdownCommand.InlineCode => "inlineCode",
            MarkdownCommand.CodeBlock => "codeBlock",
            MarkdownCommand.Link => "link",
            MarkdownCommand.Image => "image",
            MarkdownCommand.Table => "table",
            _ => string.Empty
        };
        Post(new { type = "command", command = name });
    }

    public void SetTheme(bool darkTheme)
    {
        _darkTheme = darkTheme;
        if (_initialized)
        {
            Post(new { type = "theme", dark = darkTheme });
        }
    }

    public void SetOutlineVisible(bool visible)
    {
        _outlineVisible = visible;
        if (_initialized)
        {
            Post(new { type = "outline", visible });
        }
    }

    public void InsertAsset(MarkdownAsset asset)
    {
        Post(new
        {
            type = "insertAsset",
            fileName = asset.FileName,
            uri = asset.Uri
        });
    }

    public void FocusEditor()
    {
        Post(new { type = "focusEditor" });
    }

    private async void MarkdownEditorHost_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initializing || _disposed || _webView.CoreWebView2 is not null)
        {
            return;
        }

        _initializing = true;
        try
        {
            string assetRoot = await MarkdownEditorAssets.EnsureExtractedAsync();
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SWDT",
                "WebView2");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await _webView.EnsureCoreWebView2Async(environment);
            ConfigureWebView(assetRoot);
        }
        catch (Exception ex)
        {
            _webView.Visibility = Visibility.Collapsed;
            _errorPanel.Visibility = Visibility.Visible;
            InitializationFailed?.Invoke(this, ex);
        }
        finally
        {
            _initializing = false;
        }
    }

    private void ConfigureWebView(string assetRoot)
    {
        CoreWebView2 core = _webView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.SetVirtualHostNameToFolderMapping(EditorHostName, assetRoot, CoreWebView2HostResourceAccessKind.Allow);
        core.AddWebResourceRequestedFilter($"https://{AssetHostName}/*", CoreWebView2WebResourceContext.Image);
        core.WebResourceRequested += Core_WebResourceRequested;
        core.WebMessageReceived += Core_WebMessageReceived;
        core.NavigationStarting += Core_NavigationStarting;
        core.NewWindowRequested += Core_NewWindowRequested;
        _webView.Source = new Uri($"https://{EditorHostName}/host.html");
    }

    private void Core_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        Uri uri = new(e.Request.Uri);
        string firstSegment = uri.AbsolutePath.Trim('/').Split('/')[0];
        MarkdownAsset? asset = Guid.TryParse(firstSegment, out Guid id)
            ? _assets.FirstOrDefault(candidate => candidate.Id == id)
            : null;
        if (asset is null)
        {
            e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                Stream.Null,
                404,
                "Not Found",
                "Content-Type: text/plain");
            return;
        }

        e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
            new MemoryStream(asset.Data, writable: false),
            200,
            "OK",
            $"Content-Type: {asset.MediaType}\r\nCache-Control: no-store");
    }

    private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using JsonDocument message = JsonDocument.Parse(e.WebMessageAsJson);
        JsonElement root = message.RootElement;
        string type = GetString(root, "type");
        switch (type)
        {
            case "ready":
                _pageReady = true;
                SendInitialize();
                break;
            case "initialized":
                _initialized = true;
                _webView.Visibility = Visibility.Visible;
                _errorPanel.Visibility = Visibility.Collapsed;
                break;
            case "content":
                SetContentFromEditor(GetString(root, "value"));
                break;
            case "focus":
                IsEditorFocused = root.TryGetProperty("focused", out JsonElement focusElement) && focusElement.GetBoolean();
                EditorFocusChanged?.Invoke(this, new MarkdownFocusChangedEventArgs(IsEditorFocused));
                break;
            case "flushResult":
                CompleteFlush(GetString(root, "requestId"), GetString(root, "value"));
                break;
            case "asset":
                AddAsset(root);
                break;
            case "undo":
                UndoRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "redo":
                RedoRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "save":
                SaveRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "escape":
                EscapeRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "openLink":
                OpenExternalLink(GetString(root, "uri"));
                break;
        }
    }

    private void SendInitialize()
    {
        if (!_pageReady || _initialized)
        {
            return;
        }

        Post(new
        {
            type = "initialize",
            value = _content,
            compactToolbar = _compactToolbar,
            compactLayout = _compactLayout,
            outlineVisible = _outlineVisible,
            dark = _darkTheme,
            language = _language,
            placeholder = _placeholder
        });
    }

    private void SetContentFromEditor(string value)
    {
        if (value == _content)
        {
            return;
        }

        _content = value;
        ContentChanged?.Invoke(this, new MarkdownContentChangedEventArgs(value));
    }

    private void CompleteFlush(string requestId, string value)
    {
        SetContentFromEditor(value);
        if (_flushRequests.TryGetValue(requestId, out TaskCompletionSource<string>? completion))
        {
            completion.TrySetResult(value);
        }
    }

    private void AddAsset(JsonElement root)
    {
        try
        {
            MarkdownAsset asset = new()
            {
                FileName = Path.GetFileName(GetString(root, "fileName")),
                MediaType = GetString(root, "mediaType"),
                Data = Convert.FromBase64String(GetString(root, "dataBase64"))
            };
            AssetCreated?.Invoke(this, new MarkdownAssetCreatedEventArgs(asset));
            Post(new
            {
                type = "insertAsset",
                requestId = GetString(root, "requestId"),
                fileName = asset.FileName,
                uri = asset.Uri
            });
        }
        catch (Exception ex)
        {
            InitializationFailed?.Invoke(this, ex);
        }
    }

    private void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!e.Uri.StartsWith($"https://{EditorHostName}/", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenExternalLink(e.Uri);
    }

    private static void OpenExternalLink(string uriText)
    {
        if (Uri.TryCreate(uriText, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private void Post(object message)
    {
        if (_webView.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
        }
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) ? property.GetString() ?? string.Empty : string.Empty;
    }

    private static Border CreateErrorPanel()
    {
        return new Border
        {
            Visibility = Visibility.Collapsed,
            Padding = new Thickness(24),
            Background = SystemColors.ControlBrush,
            Child = new TextBlock
            {
                Text = "Markdown editor could not start. Install or repair Microsoft Edge WebView2 Runtime.",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= MarkdownEditorHost_Loaded;
        if (_webView.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.WebResourceRequested -= Core_WebResourceRequested;
            _webView.CoreWebView2.WebMessageReceived -= Core_WebMessageReceived;
            _webView.CoreWebView2.NavigationStarting -= Core_NavigationStarting;
            _webView.CoreWebView2.NewWindowRequested -= Core_NewWindowRequested;
        }

        _webView.Dispose();
    }
}
