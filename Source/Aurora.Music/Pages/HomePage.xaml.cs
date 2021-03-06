﻿// Copyright (c) Aurora Studio. All rights reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
using Aurora.Music.Core;
using Aurora.Music.ViewModels;
using Aurora.Shared.Extensions;
using ExpressionBuilder;
using System;
using System.Linq;
using Windows.System.Threading;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using EF = ExpressionBuilder.ExpressionFunctions;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Aurora.Music.Pages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class HomePage : Page
    {
        private CompositionPropertySet _scrollerPropertySet;
        private Compositor _compositor;
        private CompositionPropertySet _props;

        public HomePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            MainPageViewModel.Current.NeedShowTitle = true;
            MainPageViewModel.Current.Title = Context.WelcomeTitle;
            MainPageViewModel.Current.LeftTopColor = Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
            AppViewBackButtonVisibility.Collapsed;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Current.Navigate(typeof(LibraryPage));
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ((sender as Grid).Resources["PointerOver"] as Storyboard).Begin();
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ((sender as Grid).Resources["Normal"] as Storyboard).Begin();
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ((sender as Grid).Resources["Pressed"] as Storyboard).Begin();
        }

        private void Grid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            ((sender as Grid).Resources["PointerOver"] as Storyboard).Begin();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ContentPanel.Width = this.ActualWidth;
        }

        private void Header_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollviewer = MainScroller;
            _scrollerPropertySet = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollviewer);
            _compositor = _scrollerPropertySet.Compositor;

            _props = _compositor.CreatePropertySet();
            _props.InsertScalar("progress", 0);

            // Get references to our property sets for use with ExpressionNodes
            var scrollingProperties = _scrollerPropertySet.GetSpecializedReference<ManipulationPropertySetReferenceNode>();
            var props = _props.GetReference();
            var progressNode = props.GetScalarProperty("progress");

            // Create and start an ExpressionAnimation to track scroll progress over the desired distance
            ExpressionNode progressAnimation = EF.Clamp(-scrollingProperties.Translation.Y / ((float)HeaderBG.Height), 0, 1);
            _props.StartAnimation("progress", progressAnimation);

            var headerbgVisual = ElementCompositionPreview.GetElementVisual(HeaderBG);
            var bgblurOpacityAnimation = EF.Clamp(progressNode, 0, 1);
            headerbgVisual.StartAnimation("Opacity", bgblurOpacityAnimation);
        }

        private async void FavList_ItemClick(object sender, ItemClickEventArgs e)
        {
            await MainPageViewModel.Current.InstantPlay(await (e.ClickedItem as GenericMusicItemViewModel).GetSongsAsync());
        }

        private void HeroGrid_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
        {
            // Walk up the tree to find the ListViewItem.
            // There may not be one if the click wasn't on an item.
            var requestedElement = (FrameworkElement)e.OriginalSource;
            while ((requestedElement != sender) && !(requestedElement is SelectorItem))
            {
                requestedElement = (FrameworkElement)VisualTreeHelper.GetParent(requestedElement);
            }
            var model = (sender as ListViewBase).ItemFromContainer(requestedElement) as GenericMusicItemViewModel;
            if (requestedElement != sender)
            {
                var albumMenu = MainPage.Current.SongFlyout.Items.First(x => x.Name == "AlbumMenu") as MenuFlyoutItem;

                switch (model.InnerType)
                {
                    case Core.Models.MediaType.Song:
                        albumMenu.Text = model.Description;
                        albumMenu.Visibility = Visibility.Visible;
                        break;
                    case Core.Models.MediaType.Album:
                        albumMenu.Text = model.Title;
                        albumMenu.Visibility = Visibility.Visible;
                        break;
                    case Core.Models.MediaType.PlayList:
                        albumMenu.Visibility = Visibility.Collapsed;
                        break;
                    case Core.Models.MediaType.Artist:
                        albumMenu.Text = model.Description;
                        albumMenu.Visibility = Visibility.Visible;
                        break;
                    default:
                        break;
                }

                // remove performers in flyout
                var index = MainPage.Current.SongFlyout.Items.IndexOf(albumMenu);
                while (!(MainPage.Current.SongFlyout.Items[index + 1] is MenuFlyoutSeparator))
                {
                    MainPage.Current.SongFlyout.Items.RemoveAt(index + 1);
                }

                if (!model.Addtional.IsNullorEmpty())
                {
                    var artists = model.Addtional.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

                    // add song's performers to flyout
                    if (!artists.IsNullorEmpty())
                    {
                        if (artists.Length == 1)
                        {
                            var menuItem = new MenuFlyoutItem()
                            {
                                Text = $"{artists[0]}",
                                Icon = new FontIcon()
                                {
                                    Glyph = "\uE136"
                                }
                            };
                            menuItem.Click += MainPage.Current.MenuFlyoutArtist_Click;
                            MainPage.Current.SongFlyout.Items.Insert(index + 1, menuItem);
                        }
                        else
                        {
                            var sub = new MenuFlyoutSubItem()
                            {
                                Text = $"{Consts.Localizer.GetString("PerformersText")}:",
                                Icon = new FontIcon()
                                {
                                    Glyph = "\uE136"
                                }
                            };
                            foreach (var item in artists)
                            {
                                var menuItem = new MenuFlyoutItem()
                                {
                                    Text = item
                                };
                                menuItem.Click += MainPage.Current.MenuFlyoutArtist_Click;
                                sub.Items.Add(menuItem);
                            }
                            MainPage.Current.SongFlyout.Items.Insert(index + 1, sub);
                        }
                    }
                }


                if (e.TryGetPosition(requestedElement, out var point))
                {
                    MainPage.Current.SongFlyout.ShowAt(requestedElement, point);
                }
                else
                {
                    MainPage.Current.SongFlyout.ShowAt(requestedElement);
                }

                e.Handled = true;
            }
        }

        private void HeroGrid_ContextCanceled(UIElement sender, RoutedEventArgs args)
        {
            MainPage.Current.SongFlyout.Hide();
        }

        private async void HeroGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if ((e.ClickedItem as GenericMusicItemViewModel).IDs == null)
            {
                await Context.RestorePlayerStatus();
            }
            else
            {
                await MainPageViewModel.Current.InstantPlay(await (e.ClickedItem as GenericMusicItemViewModel).GetSongsAsync());
            }
        }
    }
}
