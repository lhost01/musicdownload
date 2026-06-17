using Avalonia.Platform.Storage;
using Musicbox.Helpers;

namespace Musicbox.Services;

public sealed class StorageService
{
    private static readonly FilePickerFileType NcmFileType = new("NCM Files")
    {
        Patterns = ["*.ncm"]
    };

    private static readonly FilePickerFileType AudioFileType = new("Audio Files")
    {
        Patterns = ["*.mp3", "*.flac", "*.wav", "*.ncm"]
    };

    private static readonly FilePickerFileType ImageFileType = new("Image Files")
    {
        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp"]
    };

    public async Task<IReadOnlyList<string>> PickNcmFilesAsync(bool allowMultiple)
    {
        var owner = WindowHost.MainWindow;
        if (owner is null)
        {
            return [];
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            Title = "选择 NCM 文件",
            FileTypeFilter = [NcmFileType]
        });

        return files.Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var owner = WindowHost.MainWindow;
        if (owner is null)
        {
            return null;
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = title
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<IReadOnlyList<string>> PickAudioFilesAsync()
    {
        var owner = WindowHost.MainWindow;
        if (owner is null)
        {
            return [];
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "选择音频文件",
            FileTypeFilter = [AudioFileType]
        });

        return files.Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
    }

    public async Task<string?> PickImageAsync()
    {
        var owner = WindowHost.MainWindow;
        if (owner is null)
        {
            return null;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择封面图片",
            FileTypeFilter = [ImageFileType]
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}
