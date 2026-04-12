#nullable enable

using System;
using CommunityToolkit.Mvvm.Input;
using Screenbox.Core.ViewModels;
using Screenbox.Dialogs;

namespace Screenbox.Commands;

internal sealed class ConfigureChapterSkipCommand : IRelayCommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public async void Execute(object? parameter)
    {
        MediaViewModel? media = TryGetMedia(parameter);
        ChapterSkipDialog dialog = new(media);
        await dialog.ShowAsync();
    }

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static MediaViewModel? TryGetMedia(object? parameter) => parameter switch
    {
        MediaViewModel media => media,
        StorageItemViewModel storageItemViewModel => storageItemViewModel.Media,
        _ => null
    };
}
