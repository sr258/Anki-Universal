﻿/*
Copyright (C) 2016 Anki Universal Team <ankiuniversal@outlook.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using AnkiU.AnkiCore;
using AnkiU.AnkiCore.Importer;
using Windows.Storage;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Core;
using Windows.Data.Json;
using AnkiU.ViewModels;
using AnkiU.Models;
using AnkiU.Views;
using AnkiU.Pages;
using Windows.UI;
using Windows.Graphics.Display;
using AnkiU.Anki;
using AnkiU.UIUtilities;
using Windows.UI.Input.Inking;
using Windows.UI.Text.Core;
using Windows.Globalization;
using Windows.UI.Popups;
using AnkiU.Interfaces;
using AnkiU.AnkiCore.Exporter;
using AnkiU.UserControls;
using System.IO.Compression;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Media.Imaging;
using AnkiU.Anki.Syncer;
using Shared;
using Windows.ApplicationModel.Background;
using AnkiBackgroundRuntimeComponent;

namespace AnkiU
{
    public enum WindowSizeState
    {
        narrow,
        medium,
        wide
    }

    public delegate void NoticeRoutedHandler();

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, IInkToTextUIControl
    {
        public const int APP_VER = 1;

        private const string WINSIZE_NARROW = "narrow";
        private const string WINSIZE_MEDIUM = "medium";
        private const string WINSIZE_WIDE = "wide";        

        public const int COMMAND_BAR_EXPAND_HEIGHT = 13;
        public const int CUSTOM_CURSOR_CIRCLE_ID = 101;
        public const bool IS_VISUAL_EFFECT_ENABLE = false;
        public const double DEFAULT_OPACITY = 0.8;
        public const float BLUR_AMOUNT = 5;
        public const double MIN_ZOOM = 0.25;
        public const double MAX_ZOOM = 6;
        public const double ZOOM_STEP = 0.25;        
        public readonly string USER_PREF_FILE_PATH = Storage.AppLocalFolder.Path + "\\" + Constant.USER_PREF;

        private readonly Style primaryAppButtonStyle;
        private readonly Style secondaryAppButtonStyle;
        private bool isVisible = false; //WARNING: On desktop this does not ensure that the app is in foreground

        private DeckChooserFlyout nlpDeckImportFlyout;
        private WwwFormUrlDecoder decoder;
        private ThreeOptionsDialog nlpImportDiaglog;
        private string deckToImportName;
        private long deckToImportID = 0;
        private bool isInProtocolActivate = false;

        private CoreCursor cursor = new CoreCursor(CoreCursorType.Arrow, 0);

        private ISync sync = null;

        private AllHelps allHelps;        
        public AllHelps AllHelps
        {
            get
            {
                InitAllHelpsIfNeeded();
                return allHelps;
            }
        }

        private bool isFinishInitation = false;

        public static AnkiDeckBackgroundRegisterHelper BackgroundTaskHelper { get; private set; }

        public enum PrimaryButtons
        {            
            Undo,
            Save,
            Edit,

            InkOnOffSeparator,
            InkOnOff,
            InkRecognize,
            InkHideToggle,
            InkClear,
            InkEraserToggle,

            Add,
            listView,
            GridView,
            Sync,

            ZoomSeparator,
            ZoomOut,
            ZoonReset,
            ZoomIn,

            ReadModeSeparator,
            ReadMode,
        }

        public enum SecondaryButtons
        {            
            TextToSpeech,
            OneHand,
            InkOnOff,

            ZoomSeparator,
            ZoomIn,            
            ZoonReset,
            ZoomOut,

            ReadModeSeparator,
            ReadMode, 
        }        

        public Frame ContentFrame { get { return contentFrame; } }

        private CoreDispatcher currentDispatcher;
        public CoreDispatcher CurrentDispatcher { get { return currentDispatcher; } }

        private ProgressDialog progressDialog;
        private StorageFolder exportFolder;

        public const int ZINDEX_HIGHEST = 1;
        public const int ZINDEX_MIDDLE = 0;
        public const int ZINDEX_LOWSET = -1;

        public const string UNDO_LIMIT_REACHED_STRING = "Undo Limit Reached.";
        private const string NLP_JDICT_PKG_NAME = "36558AnkiUniversal.NLPJapaneseDictionary_qh2hfqm01f5q4";

        private Collection collection;
        public Collection Collection
        {
            get
            {
                return collection;
            }
            set
            {
                collection = value;
                //Make sure we notify users about full sync
                collection.ConfirmModSchemaEvent -= OnCollectionConfirmModSchemaEvent;
                collection.ConfirmModSchemaEvent += OnCollectionConfirmModSchemaEvent;
            }
        }

        public static DB UserPrefDatabase { get; set; }        
        public static GeneralPreference UserPrefs { get; set; }
        public static DeckInkPreferences DeckInkPrefs { get; set; }        
        public static DeckTextSynthPreferences DeckTextSynthPrefs { get; set; }

        public InkRecognizerContainer InkRecognizerContainer { get; set; } = null;
        private CoreTextServicesManager textServiceManager = null;
        private AvailableRecoginitionLanguageViewModel languagesViewModel = null;
        private Language previousInputLanguage = null;
        private IReadOnlyList<InkRecognizer> installedLanguagesList = null;

        public delegate void NotificationRoutedHandler();        
        public event NotificationRoutedHandler InitCollectionFinished;
        public event NotificationRoutedHandler InkToTextEnableToggled;
        public event SelectionChangedEventHandler InkToTextSelectedItemChanged;
        public event RoutedEventHandler InkToTextSelectorLoaded;
        public event RoutedEventHandler InkHideToggleButtonClick;
        public event RoutedEventHandler InkEraserToggleButtonClick;
        public event RoutedEventHandler TextToSpeechToggleButtonClick;        

        public delegate void DeckImageChangeHandler(StorageFile fileToChange, long deckId, long modifiedTime);
        public event DeckImageChangeHandler DeckImageChangedEvent;
        public delegate void DeckChangedHandler(long deckId);
        public event DeckChangedHandler DeckChanged;

        #region UIElement wrapper
        public Grid MainGrid { get { return mainGrid; } } 

        public SplitView HelpSplitView { get { return helpSplitView; } }
        public NavigationView RootSplitView { get { return splitView; } }

        public WindowSizeState WindowSizeState
        {
            get
            {
                string name = WindowSizeStates.CurrentState.Name;
                if (name == "narrow")
                    return WindowSizeState.narrow;
                else if (name == "medium")
                    return WindowSizeState.medium;
                else
                    return WindowSizeState.wide;
            }
        }        

        public AppBarButton DragAndDropButton
        {
            get
            {
                if (dragAndHoldButton == null)
                    this.FindName("dragAndHoldButton");
                return dragAndHoldButton;
            }
        }

        public AppBarButton AddButton
        {
            get
            {
                if (addButton == null)
                    this.FindName("addButton");
                return addButton;
            }
        }

        public AppBarButton SyncButton
        {
            get
            {
                if (syncButton == null)
                    this.FindName("syncButton");
                return syncButton;
            }
        }

        public AppBarButton GridViewButton
        {
            get
            {
                if (gridViewButton == null)
                    this.FindName("gridViewButton");
                return gridViewButton;
            }
        }

        public AppBarButton ListViewButton
        {
            get
            {
                if (listViewButton == null)
                    this.FindName("listViewButton");
                return listViewButton;
            }
        }

        public AppBarButton ReadModeButton
        {
            get
            {
                if (readModeButton == null)
                {
                    this.FindName("readModeButton");
                    this.FindName("readModeButtonSeparator");
                }
                return readModeButton;
            }
        }

        public AppBarButton ZoomInButton
        {
            get
            {
                if (zoomInButton == null)
                    this.FindName("zoomInButton");                
                return zoomInButton;
            }
        }

        public AppBarButton ZoomOutButton
        {
            get
            {
                if (zoomOutButton == null)
                    this.FindName("zoomOutButton");
                return zoomOutButton;
            }
        }

        public AppBarButton ZoomResetButton
        {
            get
            {
                if (zoomResetButton == null)
                {
                    this.FindName("zoomResetButton");
                    this.FindName("zoomButtonsSeparator");                    
                }
                return zoomResetButton;
            }
        }

        public AppBarSeparator ZoomButtonsSeparator
        {
            get
            {
                if (zoomButtonsSeparator == null)
                {
                    this.FindName("zoomResetButton");
                    this.FindName("zoomButtonsSeparator");
                }
                return zoomButtonsSeparator;
            }
        }

        public AppBarSeparator ReadModeButtonSeparator
        {
            get
            {
                if (readModeButtonSeparator == null)
                {
                    this.FindName("readModeButton");
                    this.FindName("readModeButtonSeparator");
                }
                return readModeButtonSeparator;
            }
        }

        public Windows.UI.Xaml.Shapes.Path ReadModeButtonSymbol
        {
            get
            {
                if (readModeButton == null)
                    FindName("readModeButton");
                return readModeButtonSymbol;
            }
        }

        public Windows.UI.Xaml.Shapes.Path InkOnSymbol
        {
            get
            {
                if (inkOnOffButton == null)
                    FindName("inkOnOffButton");
                return inkOnSymbol;
            }
        }

        public Windows.UI.Xaml.Shapes.Path InkOffSymbol
        {
            get
            {
                if (inkOnOffButton == null)
                    FindName("inkOnOffButton");
                return inkOffSymbol;
            }
        }

        public AppBarButton InkOnOffButton
        {
            get
            {
                if (inkOnOffButton == null)
                { 
                    FindName("inkOnOffButton");
                    FindName("inkSeparator");
                }
                return inkOnOffButton;
            }
        }

        public AppBarButton InkClearButton
        {
            get
            {
                if (inkClearButton == null)
                    FindName("inkClearButton");
                return inkClearButton;
            }
        }

        private AppBarButton InkEraserToggleButton
        {
            get
            {
                if (inkEraserToggleButton == null)
                    FindName("inkEraserToggleButton");
                return inkEraserToggleButton;
            }
        }

        private AppBarButton InkHideToggleButton
        {
            get
            {
                if (inkHideToggleButton == null)
                    FindName("inkHideToggleButton");
                return inkHideToggleButton;
            }
        }

        public Windows.UI.Xaml.Shapes.Path InkHideSymbol
        {
            get
            {
                if (inkHideToggleButton == null)
                    FindName("inkHideToggleButton");
                return inkHideSymbol;
            }
        }

        public Windows.UI.Xaml.Shapes.Path InkShowSymbol
        {
            get
            {
                if (inkHideToggleButton == null)
                    FindName("inkHideToggleButton");
                return inkShowSymbol;
            }
        }

        public AppBarButton EditButton
        {
            get
            {
                if (editButton == null)
                {
                    FindName("editButton");
                }
                return editButton;
            }
        }

        public AppBarButton SaveButton
        {
            get
            {
                if(saveButton == null)
                    FindName("editButton"); ;

                return saveButton;
            }
        }

        public AppBarButton UndoButton
        {
            get
            {
                if (undoButton == null)
                {
                    FindName("undoButton");
                }
                return undoButton;
            }
        }

        public AppBarButton InkRecognizeButton
        {
            get
            {
                if (inkRecognizeButton == null)
                {
                    FindName("inkRecognizeButton");
                }
                return inkRecognizeButton;
            }
        }

        public RadioButton ChooseTextAutomatically { get { return chooseTextAutomatically; } }

        public RadioButton ChooseTextManually { get { return chooseTextManually; } }

        public ToggleSwitch InkToTextEnable
        {
            get
            {
                if (isInkToTextEnable == null)
                    FindName("isInkToTextEnable");
                return isInkToTextEnable;
            }
        }
        
        public FrameworkElement TextRecognizeResultView
        {
            get
            {
                if (textRecognizeResultView == null)
                {                    
                    FindName("inkToTextResultContentPresenter");                    
                    FindName("textRecognizeResultView");
                }
                return textRecognizeResultView;
            }
        }

        private Button ChooseResultFinished
        {
            get
            {
                if (chooseResultFinished == null)
                    FindName("chooseResultFinished");
                return chooseResultFinished;
            }
        }

        public AppBarButton TextToSpeechToggleButton
        {
            get
            {
                if (textToSpeechToggle == null)
                {
                    FindName("textToSpeechToggle");
                }
                return textToSpeechToggle;
            }
        }

        public AppBarButton OneHandButton
        {
            get
            {
                if (oneHandButton == null)
                {
                    FindName("oneHandButton");
                }
                return oneHandButton;
            }
        }

        public Flyout ExportFlyout { get { return exportFlyout; } }

        public CommandBar CommanBar { get { return commandBar; } }
        #endregion

        private bool isInkToTextResultFlyoutClosed = true;
        public bool IsUserClosedInkToTextResultUI { get { return isInkToTextResultFlyoutClosed; } }

        private bool isUserHitChooseResultOkButton = false;
        public bool IsUserFinishedChoosingResults
        {
            get { return isUserHitChooseResultOkButton; }
            set { isUserHitChooseResultOkButton = value; }
        }

        public bool IsChooseTextAutomatically { get { return (bool)ChooseTextAutomatically.IsChecked; } }

        public bool IsCanNavigateBack { get; set; } = true;

        public INightReadMode[] readModeView = null;
        public IZoom zoomView = null;
        public bool IsAutoSwitchZoomButtonToSecondary { get; set; } = true;

        public MainPage()
        {
            SetMinWindowSupported();
            SetPreferLauchSize();

            this.InitializeComponent();
            currentDispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            primaryAppButtonStyle = Application.Current.Resources["PrimaryAppButton"] as Style;
            secondaryAppButtonStyle = Application.Current.Resources["SecondaryAppButton"] as Style;            
            try
            {
                NavigationSetup();
   
                InitCollectionFinished += InitCollectionFinishedHandler;
                SyncButton.Click += SyncButtonClickHandler;

                //Default startup position is always narrow, but user may change win size in last used time
                RepositionCommanBar(WINSIZE_NARROW);
            }
            catch (Exception ex)
            {
                var task = currentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await UIHelper.ShowMessageDialog(ex.Message, "Failed to init MainPage");
                });
            }
        }
        
        private static void SetPreferLauchSize()
        {
            ApplicationView.PreferredLaunchViewSize = new Size { Height = 600, Width = 500 };
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
        }

        private async void InitCollectionFinishedHandler()
        {
            if (!UserPrefs.IsFirstTimeOpenApp)
            {
                await NavigateToDeckSelectPage();
                if (UserPrefs.IsSyncOnOpen)
                    await StartSync();
            }

            await HookProtocolEvent();
            FireAppTileEventIfNeeded();
        }

        private async Task HookProtocolEvent()
        {
            await Task.Delay(250); //Avoid race condition on startup
            var currentApp = Application.Current as App;
            if (currentApp != null)
                currentApp.ProtocolActivateEvent += OnProtocolActivateEvent;

            if (currentApp.ProtocolActivatedArgs != null)
                OnProtocolActivateEvent(currentApp.ProtocolActivatedArgs);
        }

        private void FireAppTileEventIfNeeded()
        {
            var app = App.Current as App;
            if (app != null)
                app.FireAppLaunchFromtTileIfNeeded();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                base.OnNavigatedTo(e);
                await RetrieveUserPreference();
                if (UserPrefs.IsFirstTimeOpenApp)
                {
                    SetupStatusAndTitle();
                    commandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Hidden;
                    contentFrame.Navigate(typeof(FirstSetupPage), this);
                }
                else
                    ChangeStatusAndCommanBarColorMode();

                await InitDefaultImagesFolderIfneeded();
                await BackupIfNeeded();
                ChangeReadModeButtonTextAndSymbol();
                InitCollection();

                Window.Current.VisibilityChanged += VisibilityChangedHandler;

                BackgroundTaskHelper = new AnkiDeckBackgroundRegisterHelper();
                if(MainPage.APP_VER != UserPrefs.LastAppVer)
                {
                    UserPrefs.LastAppVer = MainPage.APP_VER;
                    BackgroundTaskHelper.UnRegisterBackgroundTasks();
                }
                //await BackgroundTaskHelper.RegisterBackgroundTasks(); // TODO: DISABLED FOR TESTING PURPOSES ONLY!
                ToastHelper.MarkAlreadyShown();
            }
            catch (Exception ex)
            {
                await UIHelper.ShowMessageDialog(ex.Message, "Failed to init MainPage navigation");
            }
        }

        private void InitCollection()
        {
            var task = CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);
                if(Collection == null)
                {
                    await UIHelper.ShowMessageDialog("Unable to open or create collection!");
                    bool isRestoreSuccess = await TryRestoreCollectionFromBackup();
                    if (!isRestoreSuccess)
                        await ForceCreateNewDBFiles();

                    //Reset sync time so user can choose to download or upload to OneDrive
                    ResetSyncTime();
                }

                isFinishInitation = true;
                InitCollectionFinished?.Invoke();                
            });
        }       

        private async Task<bool> TryRestoreCollectionFromBackup()
        {
            try
            {
                StorageFolder backupFolder = await TryGetBackupFolder();
                if (backupFolder == null)
                    return false;

                var files = await backupFolder.GetFilesAsync();
                if (files.Count == 0)
                    return false;

                await UIHelper.ShowMessageDialog("Please choose a backup file to restore your data.");
                BackupFlyout backupFlyout = new BackupFlyout(null, false);
                while (!backupFlyout.IsRestoreFinished)
                {
                    if (backupFlyout.IsUserCancel)
                        return false;

                    if (backupFlyout.IsFlyoutClosed)
                        backupFlyout.ShowFlyout(commandBar, FlyoutPlacementMode.Full);

                    await Task.Delay(500);
                }
                backupFlyout.HideFlyout();
                backupFlyout = null;
                Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ForceCreateNewDBFiles()
        {
            ProgressRing progress = new ProgressRing();
            progress.Width = 80;
            progress.Height = 80;
            UIHelper.AddToGridInFull(mainGrid, progress);
            progress.IsActive = true;

            await DeleteCorruptedDBIfNeeded();
            await CreateNewCollectionFiles();

            progress.IsActive = false;
            mainGrid.Children.Remove(progress);
            progress = null;
        }

        private static async Task DeleteCorruptedDBIfNeeded()
        {
            var file = await Storage.AppLocalFolder.TryGetItemAsync(Constant.COLLECTION_NAME);
            if (file != null)
                await file.DeleteAsync();
        }

        private async Task CreateNewCollectionFiles()
        {
            Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);
        }

        private void ResetSyncTime()
        {
            UserPrefs.LastSyncTime = 0;
            UpdateUserPreference();
        }

        public async Task NavigateToDeckSelectPage()
        {
            await CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var isModified = Collection.IsModified();                
                commandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Compact;                
                contentFrame.Navigate(typeof(DeckSelectPage), this);

                //Make sure we don't accidentially bump this up
                if (!isModified)
                    Collection.ClearIsModified();
            });
        }        

        private static void SetMinWindowSupported()
        {
            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetPreferredMinSize
                         (new Size { Width = 360, Height = 600 });
        }
        private void NavigationSetup()
        {
            SystemNavigationManager currentView = SystemNavigationManager.GetForCurrentView();
            currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;            

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                if(statusBar != null)
                {
                    statusBar.BackgroundOpacity = 1;                    
                }
            }

            currentView.BackRequested += (s, a) =>
            {
                if (IsCanNavigateBack && contentFrame.CanGoBack)
                {
                    contentFrame.GoBack();
                    a.Handled = true;
                }                
            };
        }        

        private async Task BackupIfNeeded()
        {
            var hoursFromLastBackup = (DateTimeOffset.Now.ToUnixTimeSeconds() - UserPrefs.LastBackups) / (60 * 60);
            if (!IsNeedBackup(hoursFromLastBackup))
                return;
            await BackupDatabase();
        }

        public static async Task BackupDatabase()
        {
            try
            {
                StorageFile collectionFile = await Storage.AppLocalFolder.TryGetItemAsync(Constant.COLLECTION_NAME) as StorageFile;
                if (collectionFile != null)
                {
                    StorageFolder backup = await TryGetBackupFolder();
                    if (backup == null)
                        backup = await Storage.AppLocalFolder.CreateFolderAsync(Constant.BACKUP_FOLDER_NAME, CreationCollisionOption.OpenIfExists);
                    var copyCollectionFile = await collectionFile.CopyAsync(backup, collectionFile.Name, NameCollisionOption.ReplaceExisting);
                    StorageFile copyMediaDBFile = await CopyMediaDBFileToBackup(backup);

                    var task = Task.Run(async () =>
                    {
                        BackUpCollection(backup, copyCollectionFile, copyMediaDBFile);

                        await copyCollectionFile.DeleteAsync();
                        if (copyMediaDBFile != null)
                            await copyMediaDBFile.DeleteAsync();

                        UserPrefs.LastBackups = DateTimeOffset.Now.ToUnixTimeSeconds();
                        await DeleteOldBackupIfNeeded(backup);
                    });
                }
            }
            catch
            {//No database yet or something prevent us to access database -> backup at another time

            }
        }

        private static async Task<StorageFolder> TryGetBackupFolder()
        {
            return await Storage.AppLocalFolder.TryGetItemAsync(Constant.BACKUP_FOLDER_NAME) as StorageFolder;
        }

        private static async Task<StorageFile> CopyMediaDBFileToBackup(StorageFolder backup)
        {
            StorageFile copyMediaDBFile = null;
            var mediaDBFile = await Storage.AppLocalFolder.TryGetItemAsync(Constant.MEDIA_DB_NAME) as StorageFile;
            if (mediaDBFile != null)
                copyMediaDBFile = await mediaDBFile.CopyAsync(backup, mediaDBFile.Name, NameCollisionOption.ReplaceExisting);
            return copyMediaDBFile;
        }

        private static async Task DeleteOldBackupIfNeeded(StorageFolder backup)
        {            
            var files = await backup.GetFilesAsync();
            if (files.Count > UserPrefs.NumberOfBackups)
            {
                var fileList = files.ToList();
                fileList.Sort((x, y) => 
                {
                    return x.DateCreated.ToUnixTimeSeconds().CompareTo(y.DateCreated.ToUnixTimeSeconds());
                });

                int numberOfFileTodelete = files.Count - UserPrefs.NumberOfBackups;
                for (int i = 0; i < numberOfFileTodelete; i++)                
                    await fileList[i].DeleteAsync();                
            }
        }

        private bool IsNeedBackup(long hoursFromLastBackup)
        {
            return hoursFromLastBackup >= UserPrefs.BackupsMinTime;
        }

        private static void BackUpCollection(StorageFolder backupFolder, StorageFile collectionFile, StorageFile copyMediaDBFile)
        {
            StringBuilder fileName = UIHelper.GetDateTimeStringForName();
            fileName.Append(Constant.BACKUP_AFFIX);
            string absolutePath = backupFolder.Path + "\\" + fileName;
            using (FileStream fileStream = new FileStream(absolutePath, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(collectionFile.Path, collectionFile.Name);
                if (copyMediaDBFile != null)
                    archive.CreateEntryFromFile(copyMediaDBFile.Path, copyMediaDBFile.Name);
            }
        }

        private async Task InitDefaultImagesFolderIfneeded()
        {
            var item = await Storage.AppLocalFolder.TryGetItemAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_NAME);
            if (item == null)
            {
                StorageFolder folder = await Storage.AppLocalFolder.CreateFolderAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_NAME);
                StorageFile f = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/Default.png"));
                await f.CopyAsync(folder, DeckInformation.DEFAULT_IMAGE_NAME);
            }
        }

        private async void VisibilityChangedHandler(object sender, VisibilityChangedEventArgs e)
        {
            isVisible = e.Visible;
            if (isVisible == false)
            {
                UpdateUserPreference();
                UpdateDeckPrefs();

                await UpdateTilesAsync();
            }
            else 
            {
                if (isFinishInitation)
                    await BackupIfNeeded();
            }
        }

        private async Task UpdateTilesAsync()
        {
            //Using shared for more light weight code and avoid collision + unneccessary changes to database
            using (var collection = await Shared.AnkiCore.Storage.OpenCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME))
            {
                Shared.ViewModels.DeckListViewModel viewModel = new Shared.ViewModels.DeckListViewModel(collection);
                viewModel.GetAllDeckInformation();
                await viewModel.UpdateAllSecondaryTilesIfHas();
            }
        }

        public void UpdateUserPreference()
        {
            if (DeckInkPrefs.IsEmpty())
                UserPrefs.IsHasInkDeckPreference = false;
            else
                UserPrefs.IsHasInkDeckPreference = true;

            if (DeckTextSynthPrefs.IsEmpty())
                UserPrefs.IsHasTextSynthDeckPreference = false;
            else
                UserPrefs.IsHasTextSynthDeckPreference = true;

            try
            {
                if (UserPrefs.IsModified)
                {
                    UserPrefDatabase.Update(UserPrefs);
                    UserPrefDatabase.IsModified = false;
                }
            }
            catch
            {//If we can't update -> something wrong -> create new table
                UserPrefDatabase.DropTable<GeneralPreference>();
                UserPrefDatabase.CreateTable<GeneralPreference>();
                UserPrefDatabase.Insert(UserPrefs);
            }
        }
        private void UpdateDeckPrefs()
        {
            DeckInkPrefs.SaveToDatabase(UserPrefDatabase);
            DeckTextSynthPrefs.SaveToDatabase(UserPrefDatabase);
        }
        private async Task RetrieveUserPreference()
        {
            StorageFile file = await Storage.AppLocalFolder.TryGetItemAsync(Constant.USER_PREF) as StorageFile;
            if (file != null)
            {
                try
                {
                    UserPrefDatabase = new DB(USER_PREF_FILE_PATH);
                    UserPrefs = UserPrefDatabase.GetTable<GeneralPreference>().First();
                    RetrieveDeckPrefs();
                }
                catch
                { //Any exception mean file is corrupted -> create default
                    if(UserPrefDatabase != null)
                        UserPrefDatabase.Close();
                    await file.DeleteAsync();
                    CreateDefaultPreference();
                }
            }
            else
            {
                CreateDefaultPreference();
            }
        }
        private void RetrieveDeckPrefs()
        {
            RetrieveInkDeckPrefIfNeeded();
            RetrieveTextSynthDeckPrefIfNeeded();
        }
        private void RetrieveInkDeckPrefIfNeeded()
        {
            try
            {
                if (UserPrefs.IsHasInkDeckPreference)
                {
                    var deckInkList = UserPrefDatabase.GetTable<InkPreference>().ToList();
                    DeckInkPrefs = new DeckInkPreferences(deckInkList);
                }
                else
                    DeckInkPrefs = new DeckInkPreferences(new List<InkPreference>());
            }
            catch
            {
                DeckInkPrefs = new DeckInkPreferences(new List<InkPreference>());
            }
        }
        private void RetrieveTextSynthDeckPrefIfNeeded()
        {
            try
            {
                if (UserPrefs.IsHasTextSynthDeckPreference)
                {
                    var deckTextSynthList = UserPrefDatabase.GetTable<TextSynthPreference>().ToList();
                    DeckTextSynthPrefs = new DeckTextSynthPreferences(deckTextSynthList);
                }
                else
                    DeckTextSynthPrefs = new DeckTextSynthPreferences(new List<TextSynthPreference>());
            }
            catch
            {
                DeckTextSynthPrefs = new DeckTextSynthPreferences(new List<TextSynthPreference>());
            }
        }
        private void CreateDefaultPreference()
        {
            UserPrefDatabase = new DB(USER_PREF_FILE_PATH);
            UserPrefs = GeneralPreference.GetDefaultPreference();
            UserPrefDatabase.CreateTable<GeneralPreference>();
            UserPrefDatabase.Insert(UserPrefs);
            UserPrefDatabase.CreateTable<InkPreference>();
            UserPrefDatabase.CreateTable<TextSynthPreference>();
            DeckInkPrefs = new DeckInkPreferences(new List<InkPreference>());
            DeckTextSynthPrefs = new DeckTextSynthPreferences(new List<TextSynthPreference>());
        }     
        public static double GetDefaultZoomLevel()
        {
            //WARNING: This maybe is an error in webview API
            //Webview will automatically scale with EPI
            //only if we change its zoom level so the 
            //default zoom level on all devices will always be 1
            return 1;
        }

        private void SplitPaneToggleClickHandler(object sender, RoutedEventArgs e)
        {            
            splitView.IsPaneOpen = !splitView.IsPaneOpen;
        }

        private async Task ImportPackage()
        {
            var fileToImport = await UIHelper.OpenFilePicker("ImportFolderToken", ".apkg");
            if (fileToImport == null)
                return;
            
            IsCanNavigateBack = false;
            progressDialog = new ProgressDialog();
            progressDialog.ProgressBarLabel = "This may take a little long if package is large...";
            progressDialog.ShowInDeterminateStateNoStopAsync("Importing");

            await BackupDatabase();

            Task task = Task.Run(async () =>
            {
                var importer = new AnkiPackageImporter(Collection, fileToImport);
                importer.AnkiPackageImporterFinishedEvent += AnkiPackageImporterFinishedEventHandler;
                importer.PackageImportStateChangeEvent += ImporterPackageImportStateChangeEventHandler;
                importer.DuplicateDeckEvent += ImporterDuplicateDeckEventHandler;
                importer.DuplicateNoteEvent += ImporterDuplicateNoteEventHandler;
                await importer.Run();
            });
        }

        private async void ImporterPackageImportStateChangeEventHandler(string message)
        {
            await CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                progressDialog.ProgressBarLabel = message;
            });
        }

        private async Task<bool> ImporterDuplicateDeckEventHandler(string name)
        {
            string message = String.Format("The imported package has a deck named \"{0}\".\n" +
                                            "Do you want to create an independent deck or merge it with your exising deck?\n" + 
                                            "(If in doubt choose \"Create\")", name);
            var dialog = new MessageDialog(message, "Conflicting Deck!");

            bool isRename = false;
            bool isUserChoose = false;
            dialog.Commands.Add(new UICommand("Merge", (command) =>
            {
                isUserChoose = true;
                isRename = false;
            }));
            dialog.Commands.Add(new UICommand("Create", (command) =>
            {
                isUserChoose = true;
                isRename = true;
            }));
            dialog.DefaultCommandIndex = 1;
            await currentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await dialog.ShowAsync();
            });
            while (!isUserChoose)
                await Task.Delay(100);

            return isRename;
        }

        private async Task<DuplicateNoteUpdate> ImporterDuplicateNoteEventHandler()
        {
            var dialog = new MessageDialog("This package has some notes that conflict with your current ones. Do you want to import these notes?\n"
                                           + "If you choose yes, conflicting notes will be resolved based on their last modified time.", "Conflicting Notes!");

            bool isUserChoose = false;
            DuplicateNoteUpdate isAllowed = new DuplicateNoteUpdate();            
            dialog.Commands.Add(new UICommand("Yes to all", (command) =>
            {
                isUserChoose = true;
                isAllowed.isAllow = true;
                isAllowed.isNotAskAgain = true;
            }));
            dialog.Commands.Add(new UICommand("No to all", (command) =>
            {
                isUserChoose = true;
                isAllowed.isAllow = false;
                isAllowed.isNotAskAgain = true;
            }));


            dialog.DefaultCommandIndex = 0;

            await CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                await dialog.ShowAsync();                                
            });
            while (!isUserChoose)
                await Task.Delay(100);

            return isAllowed;
        }

        private async void AnkiPackageImporterFinishedEventHandler(AnkiImportFinishCode code, string message)
        {
            await currentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (progressDialog != null)
                    progressDialog.Hide();

                MessageDialog dialog = null;
                switch (code)
                {
                    case AnkiImportFinishCode.Success:
                        dialog = new MessageDialog("The pakage has been imported successfully.\n" + message + " note(s) imported.", "Successed!");
                        break;
                    case AnkiImportFinishCode.MediaFileIsCorrupted:
                        dialog = new MessageDialog("Not found media files of imported deck(s).", "Importing Media");
                        break;
                    case AnkiImportFinishCode.NotFoundCollection:
                        dialog = new MessageDialog("Not found collection.", "Error!");
                        break;
                    case AnkiImportFinishCode.NotFoundValidDecks:
                        dialog = new MessageDialog("Not found any valid decks to import.", "Error!");
                        break;
                    case AnkiImportFinishCode.NotFoundMediaFile:
                        dialog = new MessageDialog("Not found media mapping file.", "Error!");
                        break;
                    case AnkiImportFinishCode.UnableToUnzip:
                        dialog = new MessageDialog("Can't extract the package.\n" + 
                                                    "This may happen if the app is not allowed to access your package or the package is corrupted.",
                                                    "Error!");
                        break;
                    case AnkiImportFinishCode.UnknownExpception:
                        dialog = new MessageDialog("Unexpeceted error.", "Error!");
                        break;
                }
                IsCanNavigateBack = true;
                await dialog.ShowAsync();                
                ReloadDeckPage();
            });
        }

        public static async Task<string> LoadStringFromPackageFileAsync(string name)
        {
            StorageFile f = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx://{name}"));
            return await FileIO.ReadTextAsync(f);
        }

        private void AdaptiveTriggerCurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            RepositionCommanBar(e.OldState.Name);
        }
        private void RepositionCommanBar(string oldStateName)
        {
            int lastPrimary;

            switch (WindowSizeStates.CurrentState.Name)
            {
                case (WINSIZE_NARROW):
                    if (IsAutoSwitchZoomButtonToSecondary)
                        MoveZoomButtonToSecondary();
                    if (oldStateName != WINSIZE_MEDIUM)
                    {
                        MoveSeparatorFromPrimaryToSecondary(ReadModeButtonSeparator);
                        MoveButtonFromPrimaryToSecondary(ReadModeButton);
                    }
                    break;

                case (WINSIZE_MEDIUM):
                    if (oldStateName == WINSIZE_NARROW)
                    {
                        MoveZoomButtonToPrimary();
                    }
                    else
                    {
                        MoveSeparatorFromPrimaryToSecondary(ReadModeButtonSeparator);
                        MoveButtonFromPrimaryToSecondary(ReadModeButton);
                    }

                    break;

                case (WINSIZE_WIDE):
                    lastPrimary = commandBar.PrimaryCommands.Count;
                    if (oldStateName != WINSIZE_MEDIUM && IsAutoSwitchZoomButtonToSecondary)
                        MoveZoomButtonToPrimary();

                    MoveSeparatorFromSecondaryToPrimary(ReadModeButtonSeparator);
                    MoveButtonFromSecondaryToPrimary(ReadModeButton);
                    break;
            }
        }

        public void MoveZoomButtonToSecondary()
        {
            //If Ink in Off State every zoom button shift up by 1
            int shift = -1;
            if (commandBar.SecondaryCommands.Contains(InkOnOffButton))
                shift = 0;
            
            MoveSeparatorFromPrimaryToSecondary(ZoomButtonsSeparator, (int)SecondaryButtons.ZoomSeparator + shift);
            MoveButtonFromPrimaryToSecondary(ZoomInButton, (int)SecondaryButtons.ZoomIn + shift);
            MoveButtonFromPrimaryToSecondary(ZoomResetButton, (int)SecondaryButtons.ZoonReset + shift);
            MoveButtonFromPrimaryToSecondary(ZoomOutButton, (int)SecondaryButtons.ZoomOut + shift);            
        }

        public void MoveZoomButtonToPrimary()
        {
            MoveSeparatorFromSecondaryToPrimary(ZoomButtonsSeparator);
            MoveButtonFromSecondaryToPrimary(ZoomInButton);
            MoveButtonFromSecondaryToPrimary(ZoomResetButton);
            MoveButtonFromSecondaryToPrimary(ZoomOutButton);
        }

        //WARNING: Should run in low priority so that if app button is being pressed it can
        //have time to release the PointerOver or other visual effects
        //This maybe is a BUG in the framework 

        public async Task MoveButtonFromPrimaryToSecondaryAsync(AppBarButton button)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                MoveButtonFromPrimaryToSecondary(button);
            });
        }
        public void MoveButtonFromPrimaryToSecondary(AppBarButton button)
        {
            if (commandBar.PrimaryCommands.Contains(button))
            {
                commandBar.PrimaryCommands.Remove(button);
                button.Style = secondaryAppButtonStyle;
                commandBar.SecondaryCommands.Add(button);                                
            }
        }

        public async Task MoveButtonFromPrimaryToSecondaryAsync(AppBarButton button, int index)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                MoveButtonFromPrimaryToSecondary(button, index);
            });
        }
        public void MoveButtonFromPrimaryToSecondary(AppBarButton button, int index)
        {
            if (commandBar.PrimaryCommands.Contains(button))
            {
                commandBar.PrimaryCommands.Remove(button);
                button.Style = secondaryAppButtonStyle;
                commandBar.SecondaryCommands.Insert(index, button);
            }
        }

        public async Task MoveButtonFromSecondaryToPrimaryAsync(AppBarButton button)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                MoveButtonFromSecondaryToPrimary(button);
            });
        }
        public void MoveButtonFromSecondaryToPrimary(AppBarButton button)
        {
            if (commandBar.SecondaryCommands.Contains(button))
            {
                commandBar.SecondaryCommands.Remove(button);
                button.Style = primaryAppButtonStyle;
                commandBar.PrimaryCommands.Add(button);                                
            }
        }

        public async Task MoveButtonFromSecondaryToPrimaryAsync(AppBarButton button, int index)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                MoveButtonFromSecondaryToPrimary(button, index);
            });
        }
        public void MoveButtonFromSecondaryToPrimary(AppBarButton button, int index)
        {
            if (commandBar.SecondaryCommands.Contains(button))
            {
                commandBar.SecondaryCommands.Remove(button);
                button.Style = primaryAppButtonStyle;
                commandBar.PrimaryCommands.Insert(index, button);                
            }
        }

        public void MoveSeparatorFromPrimaryToSecondary(AppBarSeparator sep, int index)
        {
            if (commandBar.PrimaryCommands.Contains(sep))
            {
                commandBar.PrimaryCommands.Remove(sep);
                commandBar.SecondaryCommands.Insert(index, sep);
            }
        }
        public void MoveSeparatorFromPrimaryToSecondary(AppBarSeparator sep)
        {
            if (commandBar.PrimaryCommands.Contains(sep))
            {
                commandBar.PrimaryCommands.Remove(sep);
                commandBar.SecondaryCommands.Add(sep);             
            }
        }
        public void MoveSeparatorFromSecondaryToPrimary(AppBarSeparator sep, int index)
        {
            if (commandBar.SecondaryCommands.Contains(sep))
            {
                commandBar.SecondaryCommands.Remove(sep);
                commandBar.PrimaryCommands.Insert(index, sep);
            }
        }
        public void MoveSeparatorFromSecondaryToPrimary(AppBarSeparator sep)
        {
            if (commandBar.SecondaryCommands.Contains(sep))
            {
                commandBar.SecondaryCommands.Remove(sep);
                commandBar.PrimaryCommands.Add(sep);
            }
        }

        private async void InkRecognizeButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (UIHelper.IsMobileDevice())
            {
                var task = UIHelper.ShowMessageDialog("Currently not available on Windows Phone 10.");                
                return;
            }

            try
            {
                await InitInkRecognizeIfNeeded();
            }
            catch(Exception ex)
            {
                InkRecognizerContainer = null;
                InkToTextEnable.IsOn = false;
                HideInkToTextFlyoutToggleOnContent();
                var task = UIHelper.ShowMessageDialog("Unable to init ink recognizer.");
                UIHelper.ShowDebugException(ex);
                return;
            }
        }

        public async Task InitInkRecognizeIfNeeded()
        {
            if (InkRecognizerContainer == null)
                await InitInkRecognizeButton();
        }
        private async Task InitInkRecognizeButton()
        {            
            InkRecognizerContainer = new InkRecognizerContainer();
            installedLanguagesList = InkRecognizerContainer.GetRecognizers();
            languagesViewModel = new AvailableRecoginitionLanguageViewModel();
            AvailableRecoginitionLanguage language;
            if (installedLanguagesList.Count > 0)
            {
                foreach (InkRecognizer recognizer in installedLanguagesList)
                {
                    language = new AvailableRecoginitionLanguage(recognizer.Name);
                    languagesViewModel.Availables.Add(language);
                }
            }
            else
            {
                string title = "No Handwriting Recognition Engine Installed!";
                string message = "Please Go to Window Settings> Time & language> Region & language to add a Handwriting recognition engine.";                
                var dialog = new MessageDialog(message, title);
                await dialog.ShowAsync();
                language = new AvailableRecoginitionLanguage("No Recognizer Available");
                languagesViewModel.Availables.Add(language);
            }

            InkRecognizeButton.Flyout = inkToTextFlyout;
            InkRecognizeButton.DataContext = languagesViewModel.Availables;

            // Set the text services so we can query when language changes
            textServiceManager = CoreTextServicesManager.GetForCurrentView();            
            textServiceManager.InputLanguageChanged += TextServiceManagerInputLanguageChangedHandler;
        }
        private void languageSelectComboBoxLoadedHandler(object sender, RoutedEventArgs e)
        {
            if(previousInputLanguage == null)
                SetDefaultRecognizerByCurrentInputMethodLanguageTag();
        }
        private void TextServiceManagerInputLanguageChangedHandler(CoreTextServicesManager sender, object args)
        {
            SetDefaultRecognizerByCurrentInputMethodLanguageTag();
        }
        private void SetDefaultRecognizerByCurrentInputMethodLanguageTag()
        {
            try
            {
                // Query recognizer name based on current input method language tag (bcp47 tag)
                Language currentInputLanguage = textServiceManager.InputLanguage;

                if (currentInputLanguage != previousInputLanguage)
                {
                    // try query with the full BCP47 name
                    string recognizerName = RecognizerHelper.LanguageTagToRecognizerName(currentInputLanguage.LanguageTag);

                    if (recognizerName != string.Empty)
                    {
                        for (int index = 0; index < installedLanguagesList.Count; index++)
                        {
                            if (installedLanguagesList[index].Name == recognizerName)
                            {
                                InkRecognizerContainer.SetDefaultRecognizer(installedLanguagesList[index]);
                                languageSelectComboBox.SelectedIndex = index;
                                previousInputLanguage = currentInputLanguage;
                                break;
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                var task = UIHelper.ShowMessageDialog(ex.Message);
            }
        }

        private void InkToTextLanguageSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            if (comboBox == null)
                throw new Exception("Not a comboBox!");
            SetDefaultRecognize(comboBox);
        }

        private void SetDefaultRecognize(ComboBox comboBox)
        {
            if(installedLanguagesList.Count > 0)
                InkRecognizerContainer.SetDefaultRecognizer(installedLanguagesList[comboBox.SelectedIndex]);
        }

        public static void RemoveDeckPrefsIfNeeded(long deckId)
        {
            if (DeckInkPrefs.HasId(deckId))
                DeckInkPrefs.RemoveDeckSynthPref(deckId);

            if (DeckTextSynthPrefs.HasId(deckId))
                DeckTextSynthPrefs.RemoveDeckSynthPref(deckId);
        }

        public void HideInkToTextFlyoutToggleOnContent()
        {
            languageSelectSymbol.Visibility = Visibility.Collapsed;
            languageSelectComboBox.Visibility = Visibility.Collapsed;
            chooseTextAutomatically.Visibility = Visibility.Collapsed;
            chooseTextManually.Visibility = Visibility.Collapsed;
        }

        public void ShowInkToTextFlyoutToggleOnContent()
        {
            languageSelectSymbol.Visibility = Visibility.Visible;
            languageSelectComboBox.Visibility = Visibility.Visible;
            chooseTextAutomatically.Visibility = Visibility.Visible;
            chooseTextManually.Visibility = Visibility.Visible;
        }

        private void ChooseResultFinishedClickHandler(object sender, RoutedEventArgs e)
        {
            isUserHitChooseResultOkButton = true;
        }

        public void ShowInkToTextResultUI(FrameworkElement placeToShow = null)
        {           
            //Prevents user from navigating back to other pages
            IsCanNavigateBack = false;
            if(placeToShow == null)
                inkToTextResultFlyout.ShowAt(InkRecognizeButton);
            else
                inkToTextResultFlyout.ShowAt(placeToShow);
        }

        public void CloseInkToTextResultUI()
        {
            inkToTextResultFlyout.Hide();
            IsCanNavigateBack = true;
        }

        private void InkToTextResultComboBoxSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            InkToTextSelectedItemChanged?.Invoke(sender, e);
        }
        
        private void InkToTextResultComboBoxLoadedHandler(object sender, RoutedEventArgs e)
        {            
            InkToTextSelectorLoaded?.Invoke(sender, e);
        }

        private void IsInkToTextEnableToggledHanlder(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggle = sender as ToggleSwitch;
            if (toggle == null)
                throw new Exception("Wrong object type!");
          
            InkToTextEnableToggled?.Invoke();
        }

        private void InkToTextResultFlyoutClosedHandler(object sender, object e)
        {
            isInkToTextResultFlyoutClosed = true;
        }

        private void InkToTextResultFlyoutOpenedHandler(object sender, object e)
        {
            isInkToTextResultFlyoutClosed = false;
        }

        public void ShowOnlyInkOnOffButton()
        {
            InkOnOffButton.Visibility = Visibility.Visible;
            InkOnOffButton.Label = "Ink On";
            
            //use return value to suppress warning since we don't need to wait for this to finish
            var tsk = MoveButtonFromSecondaryToPrimaryAsync(InkOnOffButton, (int)PrimaryButtons.InkOnOff);

            HideAllInkButtonsExceptOnOffIfNeeded();
            ChangeCusorToArrow();
        }

        private void HideAllInkButtonsExceptOnOffIfNeeded()
        {
            if (InkOnOffButton.Visibility == Visibility.Visible)
            {
                inkOnSymbol.Visibility = Visibility.Visible;
                InkOffSymbol.Visibility = Visibility.Collapsed;
                InkRecognizeButton.Visibility = Visibility.Collapsed;
                InkClearButton.Visibility = Visibility.Collapsed;
                InkHideToggleButton.Visibility = Visibility.Collapsed;
                InkEraserToggleButton.Visibility = Visibility.Collapsed;
            }
        }

        public void RevertToInkOffStateIfNeeded()
        {
            if (InkOffSymbol.Visibility == Visibility.Visible)
                ShowOnlyInkOnOffButton();            
        }

        public void ShowAllInkButtons()
        {
            InkOnOffButton.Visibility = Visibility.Visible;
            InkOnSymbol.Visibility = Visibility.Collapsed;
            InkOffSymbol.Visibility = Visibility.Visible;
            InkOnOffButton.Label = "Ink Off";

            //Use return value to suppress warning since we don't need to wait for this to finish
            var tsk = MoveButtonFromPrimaryToSecondaryAsync(InkOnOffButton, (int)SecondaryButtons.InkOnOff);

            InkRecognizeButton.Visibility = Visibility.Visible;
            if (inkToTextContentPresenter == null)
                FindName("inkToTextContentPresenter");

            InkClearButton.Visibility = Visibility.Visible;
            InkHideToggleButton.Visibility = Visibility.Visible;
            InkEraserToggleButton.Visibility = Visibility.Visible;
            InkNotHideStateSymbol();
            InkPenStateSymbol();

            TryChangeCursorIfNeeded();
        }

        private void InkEraserToggleButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (IsInkEraserState())
                InkPenStateSymbol();
            else
                InkEraserStateSymbol();
            InkEraserToggleButtonClick?.Invoke(sender, e);
        }

        private void InkEraserStateSymbol()
        {
            InkEraserToggleButton.Label = "Pen";
            inkEraserToggleButtonPenSymbol.Visibility = Visibility.Visible;
            inkEraserToggleButtonEraserSymbol.Visibility = Visibility.Collapsed;
        }

        private void InkPenStateSymbol()
        {
            InkEraserToggleButton.Label = "Eraser";
            inkEraserToggleButtonPenSymbol.Visibility = Visibility.Collapsed;
            inkEraserToggleButtonEraserSymbol.Visibility = Visibility.Visible;
        }

        public bool IsInkEraserState()
        {
            return inkEraserToggleButtonPenSymbol.Visibility == Visibility.Visible;
        }

        private void InkHideToggleButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (IsInkHideState())
            {
                InkNotHideStateSymbol();
                TryChangeCursorIfNeeded();
            }
            else
            {
                InkHideStateSymbol();
                ChangeCusorToArrow();
            }
            InkHideToggleButtonClick?.Invoke(sender, e);
        }

        private void InkHideStateSymbol()
        {
            InkHideToggleButton.Label = "Show Ink";
            InkHideSymbol.Visibility = Visibility.Collapsed;
            InkShowSymbol.Visibility = Visibility.Visible;
        }

        public bool IsInkHideState()
        {
            return (InkShowSymbol.Visibility == Visibility.Visible);
        }

        private void InkNotHideStateSymbol()
        {
            InkHideToggleButton.Label = "Hide Ink";
            InkHideSymbol.Visibility = Visibility.Visible;
            InkShowSymbol.Visibility = Visibility.Collapsed;
        }

        public bool IsInkOnState()
        {
            return InkOffSymbol.Visibility == Visibility.Visible;
        }

        /// <summary>
        /// Return true if ink canvas is enabled
        /// This function is used when we need to check whether user use ink input or not
        /// </summary>
        /// <returns></returns>
        public bool IsInkOn(long deckId)
        {
            return DeckInkPrefs.HasId(deckId);
        }

        private bool isCusorNotArrow = false;
        public void TryChangeCursorIfNeeded()
        {
            try
            {
                if (UIHelper.IsHasPen())
                {
                    cursor = new CoreCursor(CoreCursorType.Custom, MainPage.CUSTOM_CURSOR_CIRCLE_ID);
                    Window.Current.CoreWindow.PointerCursor = cursor;
                    isCusorNotArrow = true;
                }
            }
            catch
            {
                ChangeCusorToArrow();
            }
        }

        public void ChangeCusorToArrow()
        {
            if (isCusorNotArrow)
            {
                cursor = new CoreCursor(CoreCursorType.Arrow, 0);
                Window.Current.CoreWindow.PointerCursor = cursor;
                isCusorNotArrow = false;
            }
        }

        public void HideCommanBar()
        {
            commandBar.Visibility = Visibility.Collapsed;
        }

        public void ShowCommanBar()
        {
            commandBar.Visibility = Visibility.Visible;
        }

        public void BindToCommandBarForeGround(FrameworkElement element)
        {
            Binding b = new Binding();
            b.Source = commandBar;
            b.Path = new PropertyPath("Foreground");
            b.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            b.Mode = BindingMode.OneWay;

            element.SetBinding(ForegroundProperty, b);
        }

        private void ShowExportFlyout()
        {
            splitView.IsPaneOpen = false;
            exportFlyout.Placement = FlyoutPlacementMode.Bottom;
            exportFlyout.ShowAt(commandBar);
        }

        private async void ExportFolderPickerButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await ChooseExportFolder();
        }

        private async Task ChooseExportFolder()
        {
            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                folderPicker.FileTypeFilter.Add(".apkg");
                exportFolder = await folderPicker.PickSingleFolderAsync();
                if (exportFolder != null)
                {
                    Windows.Storage.AccessCache.StorageApplicationPermissions.
                    FutureAccessList.AddOrReplace("ExportFolderToken", exportFolder);
                    exportFlyoutTextBox.Text = exportFolder.Path;
                }
                ShowExportFlyout();
            }
            catch
            {
                await UIHelper.ShowMessageDialog("Can't open the specified folder. Please choose another folder or run in administrator mode.");
            }
        }

        private async void ExportOkButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (exportFolder == null)
                return;
            IsCanNavigateBack = false;
            exportFlyout.Hide();
            ShowExportProgessDialog();
            await ExportPackageAsync();
        }

        private void ExportFlyoutCancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            exportFlyout.Hide();
        }

        private void ShowExportProgessDialog()
        {
            progressDialog = new ProgressDialog();
            progressDialog.ProgressBarLabel = "This may take a while...";
            progressDialog.ShowInDeterminateStateNoStopAsync("Exporting all decks");            
        }

        public Task ExportPackageAsync()
        {            
            var exporter = new AnkiPackageExporter(Collection);
            if (exportMediaCheckBox.IsChecked == null || exportMediaCheckBox.IsChecked == false)
                exporter.IncludeMedia = false;
            else
                exporter.IncludeMedia = true;

            if (exportScheduleCheckBox.IsChecked == null || exportScheduleCheckBox.IsChecked == false)
                exporter.IncludeSched = false;
            else
                exporter.IncludeSched = true;
            exporter.ExportFinishedEvent += ExporterExportFinishedEventHandler;

            string fileName ="collection.apkg";

            Task task = Task.Run(async () =>
            {
                await exporter.ExportInto(exportFolder, fileName);
            });
            return task;
        }

        private async void ExporterExportFinishedEventHandler(string message)
        {
            await CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (progressDialog != null)
                    progressDialog.Hide();
                MessageDialog dialog;
                if (message == "Successed")
                    dialog = new MessageDialog("Your deck has been exported successfully.", "Successed!");
                else
                    dialog = new MessageDialog(UIConst.EXPORT_FAILED, "Error!");

                await Collection.ReOpen();
                IsCanNavigateBack = true;
                await dialog.ShowAsync();                
            });
        }

        private void ReadModeButtonClickHandler(object sender, RoutedEventArgs e)
        {
            UserPrefs.IsReadNightMode = !UserPrefs.IsReadNightMode;
            ChangeReadModeButtonTextAndSymbol();
            ChangeStatusAndCommanBarColorMode();
            ToggleReadMode();            
        }

        public void EnableChangingReadMode(params INightReadMode[] readModeView)
        {
            this.readModeView = readModeView;                        
            if (UserPrefs.IsReadNightMode)            
                ToggleReadMode();            
        }

        private void ToggleReadMode()
        {          
            if(readModeView != null)
                foreach (var read in readModeView)
                    read.ToggleReadMode();
        }

        private void SetupStatusAndTitle()
        {
            var defaultBrush = Application.Current.Resources["ButtonBackGroundNormal"] as SolidColorBrush;
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = false;                
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;                
                if (titleBar != null)
                {
                    titleBar.ButtonBackgroundColor = Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                }
            }

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                if (statusBar != null)
                {
                    statusBar.BackgroundColor = defaultBrush.Color;
                    statusBar.ForegroundColor = Colors.White;
                }
            }
        }

        public void ChangeStatusAndCommanBarColorMode()
        {
            if (UserPrefs.IsReadNightMode)
            {
                ChangeTitleBarToNightMode();
                ChangeStatusBarToNightMode();
                commandBarBindBackground.Background = UIHelper.CommandBarAcrylicDarkBrush;
                commandBar.Background = UIHelper.DarkerBrush;
                commandBar.Foreground = UIHelper.ForeGroundLight;
                RootSplitView.Foreground = UIHelper.ForeGroundLight;
            }
            else
            {
                ChangeTitleBarToDayMode();
                ChangeStatusBarToDayMode();
                commandBarBindBackground.Background = UIHelper.CommandBarAcrylicLightBrush;
                commandBar.Background = new SolidColorBrush(Colors.LightGray);
                commandBar.Foreground = new SolidColorBrush(Colors.Black);
                RootSplitView.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void ChangeTitleBarToDayMode()
        {
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {                
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {                    
                    titleBar.BackgroundColor = Colors.LightGray;
                    titleBar.ForegroundColor = Colors.Black;
                    titleBar.ButtonBackgroundColor = Colors.LightGray;
                    titleBar.ButtonForegroundColor = Colors.Black;
                }
            }
        }

        private void ChangeTitleBarToNightMode()
        {
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    titleBar.BackgroundColor = UIHelper.DarkerBrush.Color;
                    titleBar.ForegroundColor = Colors.White;
                    titleBar.ButtonBackgroundColor = UIHelper.DarkerBrush.Color;
                    titleBar.ButtonForegroundColor = Colors.White;                    
                }
            }
        }

        private void ChangeStatusBarToNightMode()
        {
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                if (statusBar != null)
                {
                    statusBar.BackgroundColor = Colors.Black;
                    statusBar.ForegroundColor = Colors.White;
                }
            }
        }

        private void ChangeStatusBarToDayMode()
        {
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                if (statusBar != null)
                {
                    statusBar.BackgroundColor = Colors.White;
                    statusBar.ForegroundColor = Colors.Black;
                }
            }
        }

        public void DisableChangingReadMode()
        {            
            readModeView = null;
        }

        private void ChangeReadModeButtonTextAndSymbol()
        {
            if (UserPrefs.IsReadNightMode)
            {
                ReadModeButtonSymbol.Style = Application.Current.Resources["SunPathIcon"] as Style;
                ReadModeButton.Label = "Day";
            }
            else
            {
                ReadModeButtonSymbol.Style = Application.Current.Resources["MoonPathIcon"] as Style;
                ReadModeButton.Label = "Night";
            }
        }

        public void HookZooming(IZoom zoomView)
        {
            ZoomInButton.Visibility = Visibility.Visible;
            ZoomOutButton.Visibility = Visibility.Visible;
            ZoomResetButton.Visibility = Visibility.Visible;
            this.zoomView = zoomView;
        }

        public void UnhookZooming()
        {
            ZoomInButton.Visibility = Visibility.Collapsed;
            ZoomOutButton.Visibility = Visibility.Collapsed;
            ZoomResetButton.Visibility = Visibility.Collapsed;
            this.zoomView = null;
        }

        private async void ZoomOutButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await ChangeZoomLevel(-ZOOM_STEP);
        }
        private async void ZoomInButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await ChangeZoomLevel(ZOOM_STEP);
        }
        private async void ZoomResetButtonClickHandler(object sender, RoutedEventArgs e)
        {
            zoomView.ZoomLevel = MainPage.GetDefaultZoomLevel();
            if (zoomView.IsSave)
                UserPrefs.ZoomLevel = zoomView.ZoomLevel;
            await zoomView.ChangeZoomLevel(zoomView.ZoomLevel);
        }
        private async Task ChangeZoomLevel(double delta)
        {
            var zoom = zoomView.ZoomLevel + delta;
            if (zoom < MIN_ZOOM)
                zoom = MIN_ZOOM;
            else if (zoom > MAX_ZOOM)
                zoom = MAX_ZOOM;

            zoomView.ZoomLevel = zoom;
            if (zoomView.IsSave)
                UserPrefs.ZoomLevel = zoom;

            await zoomView.ChangeZoomLevel(zoom);
        }

        private object lockObj = new object();
        public void SaveAndStartNewDatabaseSessionAsync()
        {
            var task = Task.Run(() =>
            {
                lock (lockObj)
                {
                    Collection.SaveAndCommit();
                    Collection.Database.SaveTransactionPoint();
                }
            });
        }

        public void SaveButtonClickAnimateAsync()
        {
            var task = currentDispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                saveButton.Opacity = 0.5;
                await Task.Delay(200);
                saveButton.Opacity = 1;
            });

        }       
       
        private void ReloadDeckPage()
        {
            contentFrame.Navigate(typeof(DeckSelectPage), this);
            contentFrame.BackStack.RemoveAt(0);
        }

        public static async Task<bool> WarnFullSyncIfNeeded()
        {
            if (UserPrefs.IsFullSyncRequire)
                return true;

            var isContinue = await UIHelper.AskUserConfirmation(UIConst.WARN_FULLSYNC);
            if(isContinue)
                UserPrefs.IsFullSyncRequire = true;
            return isContinue;
        }        

        private void HelpSplitViewPaneClosedHandler(SplitView sender, object args)
        {            
            helpSplitView.IsHitTestVisible = false;
        }

        private async void SyncButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await StartSync();
        }

        private async Task StartSync()
        {
            if (UserPrefs.SyncService == SettingPage.SYNC_ONEDRIVE)
            {
                if (sync == null || !(sync is FullSync))
                    sync = new FullSync(this, new OneDriveSync());

                await sync.StartSync();
            }
            else
            {                
                var ankiWebSync = new AnkiWebSync(this);
                await ankiWebSync.StartSync();
            }         
        }

        public void DeckImageChangedEventFire(StorageFile fileToChange, long deckId, long modifiedTime)
        {
            DeckImageChangedEvent(fileToChange, deckId, modifiedTime);
        }      

        private void OnDragAndHoldButtonClick(object sender, RoutedEventArgs e)
        {
            if (!UserPrefs.IsHelpAlreadyShown(AnkiU.Views.AllHelps.HELP_SUBDECK))
            {
                AllHelps.ShowSubDeckHelp();
            }

            DragAndDropButton.Click -= OnDragAndHoldButtonClick;
        }

        private void OnTextToSpeechToggleClick(object sender, RoutedEventArgs e)
        {
            if(textToSpeechOffSymbol.Visibility == Visibility.Collapsed)
            {
                SwitchToDisableTextToSpeechSymbol();
            }
            else
            {
                SwitchToEnableTextToSpeechSymbol();
            }

            TextToSpeechToggleButtonClick?.Invoke(sender, e);
        }

        public void SwitchToDisableTextToSpeechSymbol()
        {
            textToSpeechOffSymbol.Visibility = Visibility.Visible;
            textToSpeechOnSymbol.Visibility = Visibility.Collapsed;
            textToSpeechToggle.Label = "Disable Text to Speech";
        }

        public void SwitchToEnableTextToSpeechSymbol()
        {
            textToSpeechOffSymbol.Visibility = Visibility.Collapsed;
            textToSpeechOnSymbol.Visibility = Visibility.Visible;
            textToSpeechToggle.Label = "Enable Text to Speech";
        }

        private async void OnProtocolActivateEvent(Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs args)
        {
            if (isInProtocolActivate)
                return;

            try
            {
                isInProtocolActivate = true;
                if (args.CallerPackageFamilyName.Equals(NLP_JDICT_PKG_NAME))
                {
                    decoder = new WwwFormUrlDecoder(args.Uri.Query);
                    await AskUserConfirmation();
                    if (nlpImportDiaglog.IsRightButtonClick())
                        return;

                    if (nlpImportDiaglog.IsLeftButtonClick())
                    {
                        if (deckToImportID == 0)                        
                            deckToImportID = (long) this.Collection.Deck.AddOrResuedDeck(deckToImportName, true);
                        
                        await AddNoteFromNlpDictionary();
                        return;
                    }

                    if (nlpDeckImportFlyout == null)
                    {
                        nlpDeckImportFlyout = new DeckChooserFlyout(Collection, "Import to Deck");
                        nlpDeckImportFlyout.OkClick += OnNlpDeckImportFlyoutOkClick;
                    }
                    nlpDeckImportFlyout.GetDeckList();
                    nlpDeckImportFlyout.ShowFlyout(commandBar, FlyoutPlacementMode.Bottom);
                }
            }
            finally
            {
                isInProtocolActivate = false;
            }
        }

        private async Task AskUserConfirmation()
        {
            if (nlpImportDiaglog == null)
            {
                nlpImportDiaglog = new ThreeOptionsDialog();
                nlpImportDiaglog.Title = "";
                nlpImportDiaglog.LeftButton.Content = "Add";
                nlpImportDiaglog.MiddleButton.Content = "Deck";
                nlpImportDiaglog.NotAskAgainVisibility = Visibility.Visible;
            }            

            if (!nlpImportDiaglog.IsNotAskAgain())
            {               
                if (nlpDeckImportFlyout == null)
                    deckToImportName = NlpJdictImporter.MODEL_NAME;
                else
                {
                    var selectedDeck = nlpDeckImportFlyout.GetSelectedDeck();
                    if (selectedDeck != null)
                    {
                        deckToImportName = selectedDeck.Name;
                        deckToImportID = selectedDeck.Id;
                    }
                    else
                    {
                        deckToImportName = NlpJdictImporter.MODEL_NAME;
                        deckToImportID = 0;
                    }
                }

                nlpImportDiaglog.Message = "Add to deck: " + deckToImportName;                
                await nlpImportDiaglog.ShowAsync();
            }
        }

        private async void OnNlpDeckImportFlyoutOkClick(object sender, RoutedEventArgs e)
        {
            var deck = nlpDeckImportFlyout.GetSelectedDeck();
            if(deck == null)            
                return;
            
            deckToImportID = deck.Id;
            await AddNoteFromNlpDictionary();
        }

        private async Task AddNoteFromNlpDictionary()
        {
            if (!this.Collection.Deck.HasDeckId(deckToImportID))
                deckToImportID = (long)this.Collection.Deck.AddOrResuedDeck(NlpJdictImporter.MODEL_NAME, true);

            NlpJdictImporter importer = new NlpJdictImporter(Collection, deckToImportID);
            var success = await importer.AddNote(decoder);

            if (success)
                DeckChanged?.Invoke(importer.DeckId);           

            await ActivateNLPJDictAgain();
        }

        private static async Task ActivateNLPJDictAgain()
        {          
            var options = new Windows.System.LauncherOptions();
            options.TargetApplicationPackageFamilyName = NLP_JDICT_PKG_NAME;
            Uri uri = new Uri("com.ankiuniversal.nlpjdict:");
            await Windows.System.Launcher.LaunchUriAsync(uri, options);
        }

        private bool OnCollectionConfirmModSchemaEvent()
        {
            if (UserPrefs.IsFullSyncRequire)
                return true;

            throw new Exception("Faild to notify full sync.");         
        }

        private void OnNavigationViewItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                OnSettingClick();
            }
            else
            {
                switch (args.InvokedItem)
                {
                    case "Download Decks":
                        OnDownloadDeckButtonClick();
                        break;

                    case "Import Decks":
                        OnImportButtonClick();
                        break;

                    case "Export Decks":
                        OnExportAllButtonClick();
                        break;

                    case "Collection Statistics":
                        OnStatsButtonClick();
                        break;

                    case "Check Collection":
                        OnCheckButtonClick();
                        break;

                    case "Manage Note Types":
                        OnManageNotetypeClickHandler();
                        break;

                    case "Manage Tags":
                        OnManageTagsClickHandler();
                        break;

                    case "Back up Media Files":
                        OnBackupMediaClick();
                        break;

                    case "Check Media Files":
                        OnCheckMediaClick();
                        break;

                    case "Insert Media Files":
                        OnInsertMediaFilesClick();
                        break;

                    case "Help":
                        HelpButtonClick();
                        break;

                    case "Feedback":
                        OnSupportClick();
                        break;
                }
            }
        }

        private void OnSettingClick()
        {
            //Make sure we don't navigate to SettingPage twice
            if (contentFrame.Content is SettingPage)
                return;

            splitView.IsPaneOpen = false;
            contentFrame.Navigate(typeof(SettingPage), this);            
        }

        private async void OnDownloadDeckButtonClick()
        {
            Uri uri = new Uri("https://ankiweb.net/shared/decks/");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private async void OnImportButtonClick()
        {
            await ImportPackage();
        }

        private void OnExportAllButtonClick()
        {
            ShowExportFlyout();
        }

        private void OnStatsButtonClick()
        {
            RootSplitView.IsPaneOpen = false;
            Stats.IsWholeCollection = true;
            contentFrame.Navigate(typeof(StatsPage), this);
        }

        private void OnCheckButtonClick()
        {
            progressDialog = new ProgressDialog();
            progressDialog.ProgressBarLabel = "Check and rebuild database";
            progressDialog.ShowInDeterminateStateNoStopAsync("Optimizing collection");

            var task = Task.Run(async () =>
            {
                Collection.DeleteGraveLog();
                Collection.Optimize();
                await CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    progressDialog.Hide();
                    await UIHelper.ShowMessageDialog("Data is optimized");
                });
            });
        }

        private void OnManageNotetypeClickHandler()
        {
            splitView.IsPaneOpen = false;
            contentFrame.Navigate(typeof(ModelEditor), this);
        }

        private void OnManageTagsClickHandler()
        {
            splitView.IsPaneOpen = false;
            contentFrame.Navigate(typeof(TagManager), this);
        }

        private void OnBackupMediaClick()
        {
            MediaBackupFlyout mediaBackupFlyout = new MediaBackupFlyout(Collection);

            splitView.IsPaneOpen = false;
            mediaBackupFlyout.ShowFlyout(commandBar, FlyoutPlacementMode.Bottom);
        }

        private async void OnCheckMediaClick()
        {
            bool isContinue = await UIHelper.AskUserConfirmation("This may take a long time if you have many media files (>2000). Continue?",
                                                                  "Check Media");
            if (!isContinue)
                return;

            progressDialog = new ProgressDialog();
            progressDialog.ProgressBarLabel = "This may take a little long...";
            progressDialog.ShowInDeterminateStateNoStopAsync("Checking media folders");
            var task = Task.Run(async () =>
            {
                var results = await Collection.Media.CheckMissingAndUnusedFiles();
                await CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    progressDialog.Hide();

                    if (results.MisingFiles.Count == 0 && results.UnusedFiles.Count == 0)
                    {
                        await UIHelper.ShowMessageDialog("No unused or missing media founds");
                        return;
                    }

                    await ShowResultsToUser(results);
                });
            });
        }
        private async Task ShowResultsToUser(Media.CheckResults results)
        {
            MediaCheckContentDialog dialog = new MediaCheckContentDialog();
            StringBuilder missingMessage = new StringBuilder();
            if (results.MisingFiles.Count == 0)
                missingMessage.Append("0 file found.");
            else
                BuildMediaMapDeckList(results.MisingFiles, missingMessage);

            StringBuilder unusedMessage = new StringBuilder();
            if (results.UnusedFiles.Count == 0)
            {
                dialog.IsDeleteEnable = false;
                unusedMessage.Append("0 file found.");
            }
            else
            {
                dialog.IsDeleteEnable = true;
                BuildMediaMapDeckList(results.UnusedFiles, unusedMessage);
            }

            dialog.UnusedText = unusedMessage.ToString();
            dialog.MissingText = missingMessage.ToString();

            await dialog.ShowAsync();
            if (dialog.IsDelete)
                await DeleteMediaFiles(results.UnusedFiles);
        }
        private async Task DeleteMediaFiles(List<KeyValuePair<string, long>> results)
        {
            progressDialog = new ProgressDialog();
            progressDialog.ProgressBarLabel = "Deleting files...";
            progressDialog.ShowInDeterminateStateNoStopAsync("Delete unused media");
            await Collection.Media.DeleteMediaFiles(results);
            progressDialog.Hide();
            await UIHelper.ShowMessageDialog("Unused files have been deleted.");
        }
        private void BuildMediaMapDeckList(List<KeyValuePair<string, long>> results, StringBuilder message)
        {
            foreach (var r in results)
            {
                string deckName = Collection.Deck.GetDeckName(r.Value);
                message.Append(r.Key);
                message.Append(" in ");
                message.Append(deckName);
                message.Append(".\n\n");
            }
        }

        private void OnInsertMediaFilesClick()
        {
            InsertMediaFlyout flyout = new InsertMediaFlyout(Collection);

            splitView.IsPaneOpen = false;
            flyout.ShowFlyout(commandBar, FlyoutPlacementMode.Bottom);
        }

        private void HelpButtonClick()
        {
            InitAllHelpsIfNeeded();

            splitView.IsPaneOpen = false;

            allHelps.Foreground = commandBar.Foreground;
            helpSplitView.IsHitTestVisible = true;
            helpSplitView.IsPaneOpen = true;
        }

        public void InitAllHelpsIfNeeded()
        {
            if (allHelps == null)
            {
                allHelps = new AllHelps(ContentFrame, this, helpSplitView);
                allHelps.Background = new SolidColorBrush(Windows.UI.Colors.Transparent);  
                UIHelper.AddToGridInFull(allHelpsRootGrid, allHelps);
            }
        }

        private async void OnSupportClick()
        {
            string message = "Anki Universal " + GetAppVersion() + "\n"
                            + "For bugs: Please describe the steps needed to reproduce them.\n"
                            + "For feature requests: Please mention briefly why you need them.\n"
                            + "We'll reply to your email in one business day.\n";
            await UIHelper.LaunchEmailApp("ankiuniversal@gmail.com", message);
        }

        public static string GetAppVersion()
        {

            var version = Windows.ApplicationModel.Package.Current.Id.Version;            
            return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);

        }
    }   

}

