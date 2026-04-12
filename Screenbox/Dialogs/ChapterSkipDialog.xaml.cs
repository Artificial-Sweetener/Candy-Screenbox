#nullable enable

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Screenbox.Core.Models;
using Screenbox.Core.Services;
using Screenbox.Core.ViewModels;
using Screenbox.Helpers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Screenbox.Dialogs;

public sealed partial class ChapterSkipDialog : ContentDialog
{
    public ObservableCollection<ChapterSkipRuleEditorItem> Rules { get; } = new();

    public ObservableCollection<ChapterSkipScopeOption> ScopeOptions { get; } = new();

    private readonly IChapterSkipStore _chapterSkipStore;
    private bool _profilesLoaded;

    public ChapterSkipDialog(MediaViewModel? media)
    {
        InitializeComponent();
        FlowDirection = GlobalizationHelper.GetFlowDirection();
        RequestedTheme = ((FrameworkElement)Window.Current.Content).RequestedTheme;
        _chapterSkipStore = Ioc.Default.GetRequiredService<IChapterSkipStore>();

        ScopeOptions.Add(new ChapterSkipScopeOption(
            ChapterSkipScope.Global,
            string.Empty,
            Strings.Resources.ChapterSkipScopeGlobal));

        if (media != null && !string.IsNullOrWhiteSpace(media.Location))
        {
            ScopeOptions.Insert(0, new ChapterSkipScopeOption(
                ChapterSkipScope.File,
                media.Location,
                Strings.Resources.ChapterSkipScopeFile));

            string folderLocation = GetFolderLocation(media);
            if (!string.IsNullOrWhiteSpace(folderLocation))
            {
                ScopeOptions.Insert(1, new ChapterSkipScopeOption(
                    ChapterSkipScope.Folder,
                    folderLocation,
                    Strings.Resources.ChapterSkipScopeFolder));
            }
        }
    }

    private async void ChapterSkipDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        await _chapterSkipStore.LoadAsync();
        _profilesLoaded = true;
        ScopeComboBox.SelectedIndex = 0;
        LoadSelectedProfile();
    }

    private void ScopeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_profilesLoaded) return;
        LoadSelectedProfile();
    }

    private void AddRuleButton_OnClick(object sender, RoutedEventArgs e)
    {
        Rules.Add(new ChapterSkipRuleEditorItem());
    }

    private void RemoveRuleButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChapterSkipRuleEditorItem rule })
            Rules.Remove(rule);
    }

    private async void ChapterSkipDialog_OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        try
        {
            await SaveSelectedProfileAsync();
        }
        catch (Exception e)
        {
            args.Cancel = true;
            ErrorText.Text = e.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void LoadSelectedProfile()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        Rules.Clear();

        if (ScopeComboBox.SelectedItem is not ChapterSkipScopeOption option) return;

        TargetLocationText.Text = option.Scope == ChapterSkipScope.Global
            ? Strings.Resources.ChapterSkipGlobalDescription
            : option.Location;

        ChapterSkipProfile? profile = _chapterSkipStore.GetProfile(option.Scope, option.Location);
        if (profile == null) return;

        foreach (ChapterSkipRule rule in profile.Rules)
        {
            Rules.Add(ChapterSkipRuleEditorItem.FromRule(rule));
        }
    }

    private async Task SaveSelectedProfileAsync()
    {
        if (ScopeComboBox.SelectedItem is not ChapterSkipScopeOption option) return;

        ChapterSkipProfile profile = new()
        {
            Scope = option.Scope,
            Location = option.Location,
            Rules = Rules.Select(ToRule).Where(rule => rule != null).Cast<ChapterSkipRule>().ToList()
        };

        if (profile.Rules.Count == 0)
        {
            await _chapterSkipStore.RemoveProfileAsync(option.Scope, option.Location);
        }
        else
        {
            await _chapterSkipStore.SetProfileAsync(profile);
        }
    }

    private static ChapterSkipRule? ToRule(ChapterSkipRuleEditorItem item)
    {
        string value = item.Value.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;

        ChapterMatchMode matchMode = item.MatchMode;
        ChapterSkipRule rule = new()
        {
            IsEnabled = item.IsEnabled,
            MatchMode = matchMode
        };

        if (matchMode == ChapterMatchMode.Index)
        {
            if (!int.TryParse(value, out int chapterNumber) || chapterNumber <= 0)
                throw new InvalidOperationException(Strings.Resources.ChapterSkipInvalidChapterNumber);

            rule.Index = chapterNumber - 1;
        }
        else
        {
            rule.Pattern = value;
        }

        return rule;
    }

    private static string GetFolderLocation(MediaViewModel media)
    {
        string location = media.Location;
        if (Uri.TryCreate(location, UriKind.Absolute, out Uri uri))
        {
            if (!uri.IsFile) return string.Empty;
            location = uri.LocalPath;
        }

        try
        {
            return Path.GetDirectoryName(location) ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}

public sealed class ChapterSkipRuleEditorItem
{
    public bool IsEnabled { get; set; } = true;

    public int MatchModeIndex { get; set; }

    public string Value { get; set; } = string.Empty;

    public ChapterMatchMode MatchMode => MatchModeIndex switch
    {
        1 => ChapterMatchMode.TitleEquals,
        2 => ChapterMatchMode.TitleRegex,
        3 => ChapterMatchMode.Index,
        _ => ChapterMatchMode.TitleContains
    };

    public static ChapterSkipRuleEditorItem FromRule(ChapterSkipRule rule)
    {
        return new ChapterSkipRuleEditorItem
        {
            IsEnabled = rule.IsEnabled,
            MatchModeIndex = rule.MatchMode switch
            {
                ChapterMatchMode.TitleEquals => 1,
                ChapterMatchMode.TitleRegex => 2,
                ChapterMatchMode.Index => 3,
                _ => 0
            },
            Value = rule.MatchMode == ChapterMatchMode.Index
                ? (rule.Index + 1).ToString()
                : rule.Pattern
        };
    }
}

public sealed class ChapterSkipScopeOption
{
    public ChapterSkipScope Scope { get; }

    public string Location { get; }

    public string Label { get; }

    public ChapterSkipScopeOption(ChapterSkipScope scope, string location, string label)
    {
        Scope = scope;
        Location = location;
        Label = label;
    }
}
