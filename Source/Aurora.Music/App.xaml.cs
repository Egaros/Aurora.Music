﻿// Copyright (c) Aurora Studio. All rights reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
using Aurora.Music.Controls;
using Aurora.Music.Core;
using Aurora.Music.Core.Models;
using Aurora.Music.Core.Storage;
using Aurora.Music.Pages;
using Aurora.Music.Services;
using Aurora.Music.ViewModels;
using Aurora.Shared.Controls;
using Aurora.Shared.Helpers;
using Aurora.Shared.Logging;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Aurora.Music
{
    /// <summary>
    /// 提供特定于应用程序的行为，以补充默认的应用程序类。
    /// </summary>
    sealed partial class App : Application
    {
        private UISettings ui;
        private Frame rootFrame;
        private bool _isInBackgroundMode;
        private bool _previousVisualizing;

        public bool IsBlackTheme { get; set; } = false;

        /// <summary>
        /// 初始化单一实例应用程序对象。这是执行的创作代码的第一行，
        /// 已执行，逻辑上等同于 main() 或 WinMain()。
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += App_Resuming;
            this.UnhandledException += App_UnhandledException;
            this.EnteredBackground += App_EnteredBackground;
            this.LeavingBackground += App_LeavingBackground;

            // During the transition from foreground to background the
            // memory limit allowed for the application changes. The application
            // has a short time to respond by bringing its memory usage
            // under the new limit.
            MemoryManager.AppMemoryUsageLimitChanging += MemoryManager_AppMemoryUsageLimitChanging;

            // After an application is backgrounded it is expected to stay
            // under a memory target to maintain priority to keep running.
            // Subscribe to the event that informs the app of this change.
            MemoryManager.AppMemoryUsageIncreased += MemoryManager_AppMemoryUsageIncreased;
        }

        private void App_Resuming(object sender, object e)
        {
            LoggingDispatcher.Current.Resume();
        }

        /// <summary>
        /// Handle system notifications that the app has increased its
        /// memory usage level compared to its current target.
        /// </summary>
        /// <remarks>
        /// The app may have increased its usage or the app may have moved
        /// to the background and the system lowered the target for the app
        /// In either case, if the application wants to maintain its priority
        /// to avoid being suspended before other apps, it may need to reduce
        /// its memory usage.
        ///
        /// This is not a replacement for handling AppMemoryUsageLimitChanging
        /// which is critical to ensure the app immediately gets below the new
        /// limit. However, once the app is allowed to continue running and
        /// policy is applied, some apps may wish to continue monitoring
        /// usage to ensure they remain below the limit.
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MemoryManager_AppMemoryUsageIncreased(object sender, object e)
        {
            // Obtain the current usage level
            var level = MemoryManager.AppMemoryUsageLevel;

            // Check the usage level to determine whether reducing memory is necessary.
            // Memory usage may have been fine when initially entering the background but
            // the app may have increased its memory usage since then and will need to trim back.
            if (level == AppMemoryUsageLevel.OverLimit || level == AppMemoryUsageLevel.High)
            {
                ReduceMemoryUsage(MemoryManager.AppMemoryUsageLimit);
            }
        }

        /// <summary>
        /// Handle system notifications that the app has increased its
        /// memory usage level compared to its current target.
        /// </summary>
        /// <remarks>
        /// The app may have increased its usage or the app may have moved
        /// to the background and the system lowered the target for the app
        /// In either case, if the application wants to maintain its priority
        /// to avoid being suspended before other apps, it may need to reduce
        /// its memory usage.
        ///
        /// This is not a replacement for handling AppMemoryUsageLimitChanging
        /// which is critical to ensure the app immediately gets below the new
        /// limit. However, once the app is allowed to continue running and
        /// policy is applied, some apps may wish to continue monitoring
        /// usage to ensure they remain below the limit.
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MemoryManager_AppMemoryUsageLimitChanging(object sender, AppMemoryUsageLimitChangingEventArgs e)
        {
            // If app memory usage is over the limit, reduce usage within 2 seconds
            // so that the system does not suspend the app
            if (MemoryManager.AppMemoryUsage >= e.NewLimit)
            {
                ReduceMemoryUsage(e.NewLimit);
            }
        }

        /// <summary>
        /// Reduces application memory usage.
        /// </summary>
        /// <remarks>
        /// When the app enters the background, receives a memory limit changing
        /// event, or receives a memory usage increased event, it can
        /// can optionally unload cached data or even its view content in
        /// order to reduce memory usage and the chance of being suspended.
        ///
        /// This must be called from multiple event handlers because an application may already
        /// be in a high memory usage state when entering the background, or it
        /// may be in a low memory usage state with no need to unload resources yet
        /// and only enter a higher state later.
        /// </remarks>
        public void ReduceMemoryUsage(ulong limit)
        {
            // If the app has caches or other memory it can free, it should do so now.
            // << App can release memory here >>

            // Additionally, if the application is currently
            // in background mode and still has a view with content
            // then the view can be released to save memory and
            // can be recreated again later when leaving the background.
            if (_isInBackgroundMode && Window.Current.Content != null)
            {
                // Some apps may wish to use this helper to explicitly disconnect
                // child references.
                // VisualTreeHelper.DisconnectChildrenRecursive(Window.Current.Content);

                // Clear the view content. Note that views should rely on
                // events like Page.Unloaded to further release resources.
                // Release event handlers in views since references can
                // prevent objects from being collected.
                Window.Current.Content = null;
            }

            // Run the GC to collect released resources.
            GC.Collect();
        }

        private void App_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            // Mark the transition out of the background state
            _isInBackgroundMode = false;

            // Restore view content if it was previously unloaded
            if (Window.Current.Content == null)
            {
                CreateRootFrame(ApplicationExecutionState.Running);
            }

            if (MainPageViewModel.Current != null)
            {
                MainPageViewModel.Current.IsVisualizing = _previousVisualizing;
            }
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            if (args.Kind == ActivationKind.Protocol)
            {
                ProtocolActivatedEventArgs eventArgs = args as ProtocolActivatedEventArgs;
                // TODO: Handle URI activation
                // The received URI is eventArgs.Uri.AbsoluteUri
                if (eventArgs.Uri.Segments.Length == 0)
                {
                    if (Window.Current.Content == null)
                    {
                        CreateRootFrame(ApplicationExecutionState.NotRunning);
                        if (rootFrame.Content == null)
                        {
                            // When the navigation stack isn't restored navigate to the first page,
                            // configuring the new page by passing required information as a navigation
                            // parameter
                            if (Settings.Current.WelcomeFinished)
                                rootFrame.Navigate(typeof(MainPage));
                            else
                                rootFrame.Navigate(typeof(WelcomePage));
                        }
                        // 确保当前窗口处于活动状态
                        Window.Current.Activate();
                    }
                    else
                    {
                        // TODO:
                        // seems like nothing to do.
                    }
                }
                else
                {
                    var segments = eventArgs.Uri.Segments.Select(x => x.TrimEnd('/')).Skip(1).ToArray();
                    switch (segments[0])
                    {
                        case "settings":
                            if (Window.Current.Content == null)
                            {
                                // 将框架放在当前窗口中
                                Window.Current.Content = new ExtSettings();
                                Window.Current.Activate();
                            }
                            else
                            {
                                CoreApplicationView newView = CoreApplication.CreateNewView();
                                int newViewId = 0;
                                await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    Frame frame = new Frame();
                                    frame.Navigate(typeof(ExtSettings));
                                    Window.Current.Content = frame;
                                    // You have to activate the window in order to show it later.
                                    Window.Current.Activate();

                                    newViewId = ApplicationView.GetForCurrentView().Id;
                                });
                                bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            else if (args.Kind == ActivationKind.ToastNotification)
            {
                ToastNotificationActivatedEventArgs a = args as ToastNotificationActivatedEventArgs;
                var query = HttpUtility.ParseQueryString(a.Argument);
                if (MainPage.Current == null)
                {
                    CreateRootFrame(ApplicationExecutionState.NotRunning);
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    if (Settings.Current.WelcomeFinished)
                        rootFrame.Navigate(typeof(MainPage), query);
                    else
                        rootFrame.Navigate(typeof(WelcomePage), query);
                }
                else if (query["Action"] == "ShowPodcast")
                {
                    MainPage.Current.ShowPodcast(query["ID"]);
                }
            }
        }

        private async void CreateRootFrame(ApplicationExecutionState previousExecutionState)
        {
            rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame
                {
                    // Set the default language
                    // Language = Windows.Globalization.ApplicationLanguages.Languages[0]
                };

                rootFrame.NavigationFailed += OnNavigationFailed;
                if (previousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }


            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x33, 0x00, 0x00, 0x00);
            titleBar.ButtonForegroundColor = Colors.Black;
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = Color.FromArgb(0x55, 0x00, 0x00, 0x00);

            if (ui != null) ui.ColorValuesChanged -= Ui_ColorValuesChanged;
            ui = new UISettings();
            ui.ColorValuesChanged += Ui_ColorValuesChanged;
            ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);


            var s = Settings.Current;
            SQLOperator.Current();
            ImageCache.Instance.CacheDuration = TimeSpan.MaxValue;
            ImageCache.Instance.RetryCount = 1;
            await ImageCache.Instance.InitializeAsync(ApplicationData.Current.LocalFolder, "Cache");
        }

        private void App_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            _isInBackgroundMode = true;

            // An application may wish to release views and view data
            // here since the UI is no longer visible.
            //
            // As a performance optimization, here we note instead that
            // the app has entered background mode with _isInBackgroundMode and
            // defer unloading views until AppMemoryUsageLimitChanging or
            // AppMemoryUsageIncreased is raised with an indication that
            // the application is under memory pressure.



            if (MainPageViewModel.Current != null)
            {
                _previousVisualizing = MainPageViewModel.Current.IsVisualizing;
                MainPageViewModel.Current.IsVisualizing = false;
            }
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            Tools.Logging(e);

            if (MainPage.Current is MainPage p && e.Exception is NotImplementedException)
            {
                p.ThrowException(e);
            }

            try
            {
                if (Window.Current.Content is Frame f)
                {
                    if (f.Content is WelcomePage)
                    {
                        Settings.Current.WelcomeFinished = true;
                        Settings.Current.Save();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        protected override async void OnShareTargetActivated(ShareTargetActivatedEventArgs args)
        {
            base.OnShareTargetActivated(args);
            args.ShareOperation.ReportStarted();
            if (args.ShareOperation.Data.Contains(StandardDataFormats.StorageItems))
            {

                var items = await args.ShareOperation.Data.GetStorageItemsAsync();
                if (MainPage.Current != null)
                    await FileReceived(items);
                else
                {
                    var list = new List<StorageFile>();
                    if (items.Count > 0)
                    {
                        list.AddRange(await MainPage.ReadFilesAsync(items));
                    }
                    var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Music", CreationCollisionOption.OpenIfExists);
                    foreach (var item in list)
                    {
                        try
                        {
                            await item.CopyAsync(folder, item.Name, NameCollisionOption.ReplaceExisting);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            args.ShareOperation.ReportCompleted();
        }

        protected override async void OnFileActivated(FileActivatedEventArgs args)
        {
            base.OnFileActivated(args);
            await FileReceived(args.Files);
        }

        private async Task FileReceived(IReadOnlyList<IStorageItem> files)
        {
            if (MainPage.Current is MainPage p)
            {
                p.FileActivated(files);
            }
            else
            {
                OnLaunched(null);

                while (!(MainPage.Current is MainPage)) await Task.Delay(10);

                MainPage.Current.FileActivated(files);
            }
        }

        /// <summary>
        /// Encapsulates the call to CoreApplication.EnablePrelaunch() so that the JIT
        /// won't encounter that call (and prevent the app from running when it doesn't
        /// find it), unless this method gets called. This method should only
        /// be called when the caller determines that we are running on a system that
        /// supports CoreApplication.EnablePrelaunch().
        /// </summary>
        private void TryEnablePrelaunch()
        {
            CoreApplication.EnablePrelaunch(true);
        }

        /// <summary>
        /// 在应用程序由最终用户正常启动时进行调用。
        /// 将在启动应用程序以打开特定文件等情况下使用。
        /// </summary>
        /// <param name="e">有关启动请求和过程的详细信息。</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (e.PrelaunchActivated == false)
            {
                TryEnablePrelaunch();
                if (Window.Current.Content == null)
                {
                    CreateRootFrame(e.PreviousExecutionState);
                }
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    if (Settings.Current.WelcomeFinished)
                        rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    else
                        rootFrame.Navigate(typeof(WelcomePage), e.Arguments);
                }
                // 确保当前窗口处于活动状态
                Window.Current.Activate();
            }
            else
            {
                CreateRootFrame(e.PreviousExecutionState);
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    if (Settings.Current.WelcomeFinished)
                        rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    else
                        rootFrame.Navigate(typeof(WelcomePage), e.Arguments);
                }
            }

            var t = Task.Run(async () =>
            {
                if (BackgroundTaskHelper.IsBackgroundTaskRegistered(Consts.PodcastTaskName))
                {
                    // Background task already registered.
                    //Unregister
                    BackgroundTaskHelper.Unregister(Consts.PodcastTaskName);
                }
                // Check for background access (optional)
                await BackgroundExecutionManager.RequestAccessAsync();

                // Register (Multi Process) w/ Conditions.
                BackgroundTaskHelper.Register(Consts.PodcastTaskName, typeof(PodcastsFetcher).FullName, new TimeTrigger(Settings.Current.FetchInterval, false), true, true, new SystemCondition(SystemConditionType.InternetAvailable));
            });
        }

        private async void Ui_ColorValuesChanged(UISettings sender, object args)
        {
            await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (rootFrame != null)
                {
                    if (rootFrame.Content is IChangeTheme iT)
                    {
                        iT.ChangeTheme();
                    }
                }
            });
        }

        /// <summary>
        /// 导航到特定页失败时调用
        /// </summary>
        ///<param name="sender">导航失败的框架</param>
        ///<param name="e">有关导航失败的详细信息</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// 在将要挂起应用程序执行时调用。  在不知道应用程序
        /// 无需知道应用程序会被终止还是会恢复，
        /// 并让内存内容保持不变。
        /// </summary>
        /// <param name="sender">挂起的请求的源。</param>
        /// <param name="e">有关挂起请求的详细信息。</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: 保存应用程序状态并停止任何后台活动
            await LoggingDispatcher.Current.Suspend();
            if (MainPageViewModel.Current != null)
            {
                await MainPageViewModel.Current.SavePointAsync();
            }

            Settings.Current.Save();

            deferral.Complete();
        }
    }
}
