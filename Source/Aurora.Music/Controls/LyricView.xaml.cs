﻿using Aurora.Music.Core.Models;
using Aurora.Music.PlaybackEngine;
using Aurora.Music.ViewModels;
using Aurora.Shared.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Aurora.Music.Controls
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class LyricView : Page, INotifyPropertyChanged
    {
        private SongViewModel model;

        private Extension extension;

        public LyricView()
        {
            this.InitializeComponent();
        }

        internal SongViewModel Model
        {
            get => model; set
            {
                SetProperty(ref model, value);
            }
        }

        internal LyricViewModel Lyric = new LyricViewModel();

        public event PropertyChangedEventHandler PropertyChanged;


        private void RaisePropertyChanged(string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T backingField, T Value, [CallerMemberName] string propertyName = null)
        {
            var changed = !EqualityComparer<T>.Default.Equals(backingField, Value);
            if (changed)
            {
                backingField = Value;
                this.RaisePropertyChanged(propertyName);
            }
            return changed;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            Player.Current.PositionUpdated += LyricView_PositionUpdated;
            Player.Current.StatusChanged += LyricView_StatusChanged;
            base.OnNavigatedTo(e);
            if (e.Parameter is SongViewModel m)
            {
                Model = m;

                extension = MainPageViewModel.Current.LyricExtension;
                if (extension != null)
                {
                    var result = await extension.ExecuteAsync(new KeyValuePair<string, object>("q", "lyric"), new KeyValuePair<string, object>("title", model.Title), new KeyValuePair<string, object>("artist", model.Song.Performers.IsNullorEmpty() ? null : model.Song.Performers[0]), new KeyValuePair<string, object>("ID", model.IsOnline ? model.Song.OnlineID : null));
                    if (result != null)
                    {
                        var l = new Lyric(LrcParser.Parser.Parse((string)result, model.Song.Duration));
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                        {
                            Lyric.New(l);
                        });
                    }
                    else
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                        {
                            Lyric.Clear();
                        });
                    }
                }
                else
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                    {
                        Lyric.Clear();
                    });
                }
            }
        }

        private async void LyricView_StatusChanged(object sender, PlaybackEngine.StatusChangedArgs e)
        {
            if (e.CurrentSong != null && e.CurrentSong.IsIDEqual(Model.Song))
            {
                Model = new SongViewModel(e.CurrentSong);
                if (extension != null)
                {
                    var result = await extension.ExecuteAsync(new KeyValuePair<string, object>("q", "lyric"), new KeyValuePair<string, object>("title", model.Title), new KeyValuePair<string, object>("artist", model.Song.Performers.IsNullorEmpty() ? null : model.Song.Performers[0]), new KeyValuePair<string, object>("ID", model.IsOnline ? model.Song.OnlineID : null));
                    if (result != null)
                    {
                        var l = new Lyric(LrcParser.Parser.Parse((string)result, model.Song.Duration));
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                        {

                            Lyric.New(l);
                        });
                    }
                    else
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                        {
                            Lyric.Clear();
                        });
                    }
                }
                else
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                    {
                        Lyric.Clear();
                    });
                }
            }
        }

        private async void LyricView_PositionUpdated(object sender, PlaybackEngine.PositionUpdatedArgs e)
        {
            if (Lyric != null && e.Current != default(TimeSpan))
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
              {
                  Lyric.Update(e.Current);
              });
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Player.Current.PositionUpdated -= LyricView_PositionUpdated;
            Player.Current.StatusChanged -= LyricView_StatusChanged;
        }
    }
}
