using System.IO;
using System.Reflection;

namespace SWDT;

internal static class MarkdownEditorAssets
{
    private const string AssetPrefix = "SWDT.MarkdownEditor/";
    private const string AssetVersion = "vditor-3.11.2-host-28";
    private static readonly SemaphoreSlim ExtractionLock = new(1, 1);

    public static async Task<string> EnsureExtractedAsync()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SWDT",
            "MarkdownEditor",
            AssetVersion);
        string marker = Path.Combine(root, ".ready");
        if (File.Exists(marker))
        {
            return root;
        }

        await ExtractionLock.WaitAsync();
        try
        {
            if (File.Exists(marker))
            {
                return root;
            }

            Directory.CreateDirectory(root);
            Assembly assembly = typeof(MarkdownEditorAssets).Assembly;
            foreach (string resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = resourceName[AssetPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
                string destination = Path.GetFullPath(Path.Combine(root, relativePath));
                if (!destination.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Invalid embedded Markdown editor resource path.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using Stream input = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
                await using FileStream output = File.Create(destination);
                await input.CopyToAsync(output);
            }

            await File.WriteAllTextAsync(marker, AssetVersion);
            return root;
        }
        finally
        {
            ExtractionLock.Release();
        }
    }
}
