using System.Net.Http;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DrekoAccSwitcher.Core;
using DrekoAccSwitcher.Models;
using DrekoAccSwitcher.Services;
using Localization = DrekoAccSwitcher.Services.Localization;

namespace DrekoAccSwitcher.ViewModels;

public sealed class PlatformItem : ObservableObject
{
    public required PlatformDefinition Definition { get; init; }
    public string Id => Definition.Id;
    public string Name => Definition.DisplayName;
    public string Glyph => Definition.Glyph;
    public string IconPath => $"/Assets/{Id switch
    {
        "steam" => "steam",
        "epic" => "epic",
        "battlenet" => "battlenet",
        "riot" => "riot",
        "ea" => "ea",
        _ => throw new InvalidOperationException($"No launcher icon is configured for '{Id}'.")
    }}.png";
    public string InstalledText => Localization.Text(Installed ? "installed" : "notFound");
    public System.Windows.Media.Brush Accent { get; init; } = Brushes.White;
    public bool Installed { get; init; }
    public void RefreshLocalization() => Raise(nameof(InstalledText));

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }
}

public sealed class AccountItem : ObservableObject
{
    public required AccountView Account { get; init; }
    public string DisplayName => UseWesternDigits(Account.DisplayName);
    public System.Windows.FlowDirection DisplayNameFlowDirection =>
        ContainsArabicText(Account.DisplayName)
            ? Localization.FlowDirection
            : System.Windows.FlowDirection.LeftToRight;
    public System.Windows.FlowDirection SubtitleFlowDirection =>
        !string.IsNullOrWhiteSpace(Account.UserName) && Account.UserName != Account.DisplayName
            ? System.Windows.FlowDirection.LeftToRight
            : Localization.FlowDirection;
    public string Subtitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Account.UserName) && Account.UserName != Account.DisplayName)
                return UseWesternDigits(Account.UserName!);
            if (Account.LastUsedAt is DateTimeOffset used)
                return UseWesternDigits($"{Localization.Text("lastUsed")} {used.LocalDateTime.ToString("g")}");
            return Account.FromLauncher ? Localization.Text("rememberedByLauncher") : Localization.Text("savedLocally");
        }
    }

    private static string UseWesternDigits(string value) =>
        value
            .Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
            .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9')
            .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
            .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7').Replace('۸', '8').Replace('۹', '9');

    private static bool ContainsArabicText(string value) =>
        value.Any(character =>
            character is >= '\u0600' and <= '\u06FF'
            or >= '\u0750' and <= '\u077F'
            or >= '\u08A0' and <= '\u08FF'
            or >= '\uFB50' and <= '\uFDFF'
            or >= '\uFE70' and <= '\uFEFF');

    public void RefreshLocalization()
    {
        Raise(nameof(Subtitle));
        Raise(nameof(SubtitleFlowDirection));
        Raise(nameof(DisplayNameFlowDirection));
    }
    public string Initials
    {
        get
        {
            var name = DisplayName.Trim();
            if (name.Length == 0) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1 ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
                : string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
        }
    }
    public string? AvatarPath => Account.AvatarPath;
    public bool HasAvatar => File.Exists(Account.AvatarPath ?? "");
}

public sealed class MainViewModel : ObservableObject
{
    private readonly AccountRepository _repo = new();
    private readonly SwitcherEngine _engine;
    private readonly GitHubReleaseService _releaseService = new();
    private bool _busy;
    private string _status;
    private UpdateCheckState _updateCheckState = UpdateCheckState.Checking;
    private string? _latestReleaseTag;
    private string _search = "";
    private bool _searchVisible;
    private PlatformItem? _selectedPlatform;

    public ObservableCollection<PlatformItem> Platforms { get; } = [];
    public ObservableCollection<AccountItem> Accounts { get; } = [];

    public ICommand SelectPlatformCommand { get; }
    public ICommand SwitchCommand { get; }
    public ICommand SaveCurrentCommand { get; }
    public ICommand AddNewCommand { get; }
    public ICommand ForgetCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand LaunchCommand { get; }
    public ICommand OpenDataCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand HomeCommand { get; }
    private int? _totalAccountCount;

    private enum UpdateCheckState
    {
        Checking,
        Available,
        UpToDate,
        NoRelease,
        Failed
    }

    public MainViewModel()
    {
        _engine = new SwitcherEngine(_repo);
        _status = Localization.Text("ready");
        Localization.Changed += OnLanguageChanged;
        HomeCommand = new RelayCommand(ShowHome);
        SelectPlatformCommand = new RelayCommand(p => { if (p is PlatformItem item) SelectPlatform(item); });
        SwitchCommand = new RelayCommand(async p => { if (p is AccountItem item) await RunAsync(() => _engine.SwitchToAsync(SelectedPlatform!.Definition, item.Account), Localization.Text("switched")); });
        SaveCurrentCommand = new RelayCommand(async () => await SaveCurrentAsync(), () => !Busy);
        AddNewCommand = new RelayCommand(async () => await AddNewAccountAsync(), () => !Busy);
        ForgetCommand = new RelayCommand(p =>
        {
            if (p is not AccountItem item || SelectedPlatform is null) return;
            if (MessageBox.Show(Localization.Format("forgetPrompt", item.DisplayName),
                    Localization.Text("forgetTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question,
                    MessageBoxResult.No, Localization.IsArabic
                        ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                        : MessageBoxOptions.None) != MessageBoxResult.Yes)
                return;
            _engine.Forget(SelectedPlatform.Definition, item.Account);
            ReloadAccounts();
            Status = Localization.Text("forgotten");
        });
        RenameCommand = new RelayCommand(p =>
        {
            if (p is not AccountItem item || SelectedPlatform is null) return;
            var name = Prompt(Localization.Text("renameTitle"), Localization.Text("displayName"), item.DisplayName);
            if (string.IsNullOrWhiteSpace(name)) return;
            _engine.Rename(SelectedPlatform.Definition, item.Account, name.Trim());
            ReloadAccounts();
        });
        RefreshCommand = new RelayCommand(ReloadAccounts);
        LaunchCommand = new RelayCommand(() =>
        {
            try
            {
                if (SelectedPlatform is null) return;
                _engine.Launch(SelectedPlatform.Definition);
                Status = Localization.Text("launcherStarted");
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        });
        OpenDataCommand = new RelayCommand(() =>
        {
            AppPaths.EnsureLayout();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppPaths.Root,
                UseShellExecute = true
            });
        });
        SearchCommand = new RelayCommand(() =>
        {
            SearchVisible = !SearchVisible;
            if (!SearchVisible)
                Search = "";
        });
        ClearSearchCommand = new RelayCommand(() => Search = "");

        Load();
        _ = CheckForUpdatesAsync();
    }

    public bool Busy
    {
        get => _busy;
        set
        {
            if (Set(ref _busy, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public string Search
    {
        get => _search;
        set
        {
            if (Set(ref _search, value))
                ReloadAccounts();
        }
    }

    public bool SearchVisible
    {
        get => _searchVisible;
        set
        {
            if (Set(ref _searchVisible, value))
                Raise(nameof(SearchBoxVisibility));
        }
    }
    public Visibility SearchBoxVisibility => SearchVisible ? Visibility.Visible : Visibility.Collapsed;

    public PlatformItem? SelectedPlatform
    {
        get => _selectedPlatform;
        private set
        {
            if (Set(ref _selectedPlatform, value))
            {
                Raise(nameof(HeaderTitle));
                Raise(nameof(HeaderHint));
                Raise(nameof(InstalledLabel));
                Raise(nameof(IsHome));
            }
        }
    }

    public bool IsHome => SelectedPlatform is null;
    public string HeaderTitle => SelectedPlatform?.Name ?? Localization.Text("selectLauncher");
    public string HeaderHint => SelectedPlatform?.Id == "steam"
        ? Localization.Text("steamHint")
        : Localization.Text("saveHint");
    public string InstalledLabel => SelectedPlatform is null
        ? ""
        : SelectedPlatform.Installed ? Localization.Text("installed") : Localization.Text("notFound");
    public System.Windows.FlowDirection FlowDirection => Localization.FlowDirection;
    public string LaunchersText => Localization.Text("launchers");
    public string OpenDataText => Localization.Text("openData");
    public string SettingsText => Localization.Text("settings");
    public string LaunchText => Localization.Text("launch");
    public string AddNewText => Localization.Text("addNew");
    public string SaveAccountText => Localization.Text("saveAccount");
    public string SearchText => Localization.Text("search");
    public string ClearSearchText => Localization.Text("clearSearch");
    public string SearchPlaceholder => Localization.Text("searchAccounts");
    public string SwitchText => Localization.Text("switch");
    public string RenameText => Localization.Text("rename");
    public string ForgetText => Localization.Text("forget");
    public string RightClickHint => Localization.Text("rightClickHint");
    public string NoAccountsText => Localization.Text("noAccounts");
    public string EmptyHint => Localization.Text("emptyHint");
    public string AppTitle => Localization.Text("appTitle");
    public string HomeText => Localization.Text("home");
    public string HomeTitle => Localization.Text("homeTitle");
    public string HomeDescription => Localization.Text("homeDescription");
    public string TotalAccountsText => Localization.Text("totalAccounts");
    public string AccountCountText => _totalAccountCount?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                      ?? Localization.Text("unavailable");
    public string AppVersionText => $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";
    public string UpdateStatusTitle => Localization.Text("updateStatus");
    public string UpdateStatusText => _updateCheckState switch
    {
        UpdateCheckState.Checking => Localization.Text("updateChecking"),
        UpdateCheckState.Available => Localization.Format("updateAvailable", _latestReleaseTag!),
        UpdateCheckState.UpToDate => Localization.Text("upToDate"),
        UpdateCheckState.NoRelease => Localization.Text("noRelease"),
        UpdateCheckState.Failed => Localization.Text("updateCheckFailed"),
        _ => throw new InvalidOperationException($"Unknown update check state: {_updateCheckState}.")
    };
    public string HowToUseTitle => Localization.Text("howToUse");
    public string HowToUseStepOne => Localization.Text("howToUseStepOne");
    public string HowToUseStepTwo => Localization.Text("howToUseStepTwo");
    public string HowToUseStepThree => Localization.Text("howToUseStepThree");
    public string PrivacyNoteTitle => Localization.Text("privacyNoteTitle");
    public string PrivacyNoteText => Localization.Text("privacyNoteText");
    public TextAlignment PrivacyNoteTextAlignment =>
        Localization.IsArabic ? TextAlignment.Right : TextAlignment.Left;

    public bool HasAccounts => Accounts.Count > 0;

    private void Load()
    {
        AppPaths.EnsureLayout();
        try
        {
            _repo.Load();
        }
        catch (Exception ex)
        {
            Fail(ex);
            return;
        }

        Platforms.Clear();
        foreach (var def in PlatformCatalog.All)
        {
            Platforms.Add(new PlatformItem
            {
                Definition = def,
                Installed = LauncherLocator.IsInstalled(def),
                Accent = (Brush)new BrushConverter().ConvertFromString(def.AccentHex)!
            });
        }

        RefreshTotalAccountCount();
    }

    private void ShowHome()
    {
        foreach (var platform in Platforms)
            platform.Selected = false;
        SelectedPlatform = null;
        Search = "";
        SearchVisible = false;
        Accounts.Clear();
        Status = Localization.Text("ready");
    }

    private void SelectPlatform(PlatformItem item)
    {
        foreach (var p in Platforms)
            p.Selected = p == item;
        SelectedPlatform = item;
        Status = item.Installed
            ? Localization.Format("launcherReady", item.Name)
            : Localization.Format("launcherMissing", item.Name);
        ReloadAccounts();
    }

    private void ReloadAccounts()
    {
        Accounts.Clear();
        RefreshTotalAccountCount();
        if (SelectedPlatform is null) return;
        try
        {
            IEnumerable<AccountView> list = _engine.ListAccounts(SelectedPlatform.Definition);
            if (!string.IsNullOrWhiteSpace(Search))
            {
                list = list.Where(a =>
                    a.DisplayName.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                    (a.UserName?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            foreach (var account in list.OrderByDescending(a => a.LastUsedAt ?? DateTimeOffset.MinValue).ThenBy(a => a.DisplayName))
                Accounts.Add(new AccountItem { Account = account });
        }
        catch (Exception ex)
        {
            Accounts.Clear();
            Log.Write($"Account list load failed for {SelectedPlatform.Id}: {ex}");
            Status = Localization.Format("listFailed", ex.Message);
        }

        Raise(nameof(HasAccounts));
    }

    private void RefreshTotalAccountCount()
    {
        try
        {
            _totalAccountCount = Platforms
                .SelectMany(platform => _engine.ListAccounts(platform.Definition))
                .Select(account => (account.PlatformId, account.Id))
                .Distinct()
                .Count();
        }
        catch (Exception ex)
        {
            _totalAccountCount = null;
            Log.Write($"Dashboard account count failed: {ex}");
        }

        Raise(nameof(AccountCountText));
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var latestRelease = await _releaseService.GetLatestAsync();
            if (latestRelease is null)
            {
                _updateCheckState = UpdateCheckState.NoRelease;
            }
            else
            {
                var latestVersionText = latestRelease.TagName.TrimStart('v', 'V');
                if (!Version.TryParse(latestVersionText, out var latestVersion))
                    throw new InvalidDataException($"GitHub release tag '{latestRelease.TagName}' is not a valid version.");

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version
                                     ?? new Version(1, 0, 0);
                _latestReleaseTag = latestRelease.TagName;
                _updateCheckState = latestVersion > currentVersion
                    ? UpdateCheckState.Available
                    : UpdateCheckState.UpToDate;
            }
        }
        catch (HttpRequestException ex)
        {
            Log.Write($"GitHub update check failed: {ex}");
            _updateCheckState = UpdateCheckState.Failed;
        }
        catch (TaskCanceledException ex)
        {
            Log.Write($"GitHub update check timed out: {ex}");
            _updateCheckState = UpdateCheckState.Failed;
        }
        catch (JsonException ex)
        {
            Log.Write($"GitHub returned invalid release data: {ex}");
            _updateCheckState = UpdateCheckState.Failed;
        }
        catch (InvalidDataException ex)
        {
            Log.Write($"GitHub release data is invalid: {ex}");
            _updateCheckState = UpdateCheckState.Failed;
        }
        catch (InvalidOperationException ex)
        {
            Log.Write($"GitHub release data could not be read: {ex}");
            _updateCheckState = UpdateCheckState.Failed;
        }

        Raise(nameof(UpdateStatusText));
    }

    private async Task SaveCurrentAsync()
    {
        if (SelectedPlatform is null) return;
        var suggested = SelectedPlatform.Id == "steam"
            ? SteamSwitcher.ListRememberedAccounts().FirstOrDefault(a => a.Id == SteamSwitcher.GetMostRecentSteamId())?.DisplayName
            : null;
        var name = Prompt(Localization.Text("saveTitle"), Localization.Text("nameAccount"), suggested ?? "");
        if (string.IsNullOrWhiteSpace(name)) return;
        await RunAsync(async () =>
        {
            await _engine.SaveCurrentAsync(SelectedPlatform.Definition, name.Trim());
        }, Localization.Text("accountSaved"));
        ReloadAccounts();
    }

    private async Task AddNewAccountAsync()
    {
        if (SelectedPlatform is null) return;
        var isEpic = SelectedPlatform.Id.Equals("epic", StringComparison.OrdinalIgnoreCase);
        await RunAsync(async () =>
        {
            await _engine.AddNewAsync(SelectedPlatform.Definition);
            if (isEpic)
            {
                MessageBox.Show(Localization.Text("epicSignOutRequired"), Localization.Text("appTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK,
                    Localization.IsArabic
                        ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                        : MessageBoxOptions.None);
            }
        }, isEpic ? Localization.Text("epicAddReady") : Localization.Text("launcherOpened"));
    }

    private async Task RunAsync(Func<Task> work, string ok)
    {
        if (Busy) return;
        Busy = true;
        Status = Localization.Text("working");
        try
        {
            await work();
            Status = ok;
            ReloadAccounts();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    private static string? Prompt(string title, string label, string initial)
    {
        var dialog = new Views.NameDialog(title, label, initial)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.ResultText : null;
    }

    private void Fail(Exception ex)
    {
        Log.Write(ex.ToString());
        Status = ex.Message;
        MessageBox.Show(ex.Message, Localization.Text("appTitle"), MessageBoxButton.OK, MessageBoxImage.Warning,
            MessageBoxResult.OK, Localization.IsArabic
                ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                : MessageBoxOptions.None);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Raise(nameof(FlowDirection));
        Raise(nameof(SearchBoxVisibility));
        Raise(nameof(HeaderTitle));
        Raise(nameof(HeaderHint));
        Raise(nameof(InstalledLabel));
        Raise(nameof(LaunchersText));
        Raise(nameof(OpenDataText));
        Raise(nameof(SettingsText));
        Raise(nameof(LaunchText));
        Raise(nameof(AddNewText));
        Raise(nameof(SaveAccountText));
        Raise(nameof(SearchText));
        Raise(nameof(ClearSearchText));
        Raise(nameof(SearchPlaceholder));
        Raise(nameof(SwitchText));
        Raise(nameof(RenameText));
        Raise(nameof(ForgetText));
        Raise(nameof(RightClickHint));
        Raise(nameof(NoAccountsText));
        Raise(nameof(EmptyHint));
        Raise(nameof(AppTitle));
        Raise(nameof(HomeText));
        Raise(nameof(HomeTitle));
        Raise(nameof(HomeDescription));
        Raise(nameof(TotalAccountsText));
        Raise(nameof(AccountCountText));
        Raise(nameof(UpdateStatusTitle));
        Raise(nameof(UpdateStatusText));
        Raise(nameof(HowToUseTitle));
        Raise(nameof(HowToUseStepOne));
        Raise(nameof(HowToUseStepTwo));
        Raise(nameof(HowToUseStepThree));
        Raise(nameof(PrivacyNoteTitle));
        Raise(nameof(PrivacyNoteText));
        Raise(nameof(PrivacyNoteTextAlignment));
        foreach (var account in Accounts)
            account.RefreshLocalization();
        foreach (var platform in Platforms)
            platform.RefreshLocalization();

        Status = SelectedPlatform is null
            ? Localization.Text("ready")
            : SelectedPlatform.Installed
                ? Localization.Format("launcherReady", SelectedPlatform.Name)
                : Localization.Format("launcherMissing", SelectedPlatform.Name);
    }
}
