﻿using RedditImageBrowser.Common;
using RedditImageBrowser.DataSource;
using RedditImageBrowser.Json;
using RedditImageBrowser.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RedditImageBrowser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Config ApplicationConfig = null;
        RedditAPI RedditAPI = null;
        DownloadManager ThumbnailDownloader = null;
        DownloadManager ImageDownloader = null;
        int scrollOffset = 35;

        /// <summary>
        /// A deferred set of thumbnails to be shown as their respective thumbnails are complete
        /// </summary>
        private ObservableCollection<Listing.Child> deferredThumbnails;

        public MainWindow()
        {
            InitializeComponent();

            ApplicationConfig = DataContext as Config;
            RedditAPI = new RedditAPI();
            {
                ImageDownloader = new DownloadManager(2, 750);
                ImageDownloader.DownloadComplete += ImageDownloader_DownloadComplete;
                ImageDownloader.DownloadProgressChanged += ImageDownloader_DownloadProgressChanged;
                ImageDownloader.Start();
            }

            {
                ThumbnailDownloader = new DownloadManager(5, 250);
                ThumbnailDownloader.DownloadComplete += ThumbDownloader_DownloadComplete;
                ThumbnailDownloader.DownloadProgressChanged += ThumbDownloader_DownloadProgressChanged;
                ThumbnailDownloader.Start();
            }
            SubredditsAvailable.SelectedIndex = 0;
        }

        /// <summary>
        /// Update the progress for the progress bar related to image downloads
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        async void ImageDownloader_DownloadProgressChanged(object sender, DownloadManager.DownloadProgressChangedArgs e)
        {
            await App.Current.Dispatcher.InvokeAsync(() => {
                CurrentStatus.Text = e.Id;
                ImageDownloadProgress.Value = e.TotalPercentCompleted;
            });
        }

        /// <summary>
        /// Update the status text for the image downloader progress bar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        async void ImageDownloader_DownloadComplete(object sender, DownloadManager.DownloadCompleteArgs e)
        {
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                CurrentStatus.Text = "Bored";
                ImageDownloadProgress.Value = 0;
            });
        }

        /// <summary>
        /// Updates the progress bar with download progress
        /// </summary>
        async void ThumbDownloader_DownloadProgressChanged(object sender, DownloadManager.DownloadProgressChangedArgs e)
        {
            await App.Current.Dispatcher.InvokeAsync(() => {
                ThumbnailProgress.Value = e.TotalPercentCompleted;
            });
        }

        /// <summary>
        /// Updates the progress bar with the download progress
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        async void ThumbDownloader_DownloadComplete(object sender, DownloadManager.DownloadCompleteArgs e)
        {
            // Due to thread affinity we are locked to the UI thread with this, maybe we can put this into another thread
            // With delegates at some point in the future, we'll see !
            await App.Current.Dispatcher.InvokeAsync(() => UpdateThumbnailCollectionAsync(e.Id, e.fileLocation));
        }


        /// <summary>
        /// Pushes new thumbnails to the UI as they are available
        /// </summary>
        /// <param name="id"></param>
        /// <param name="thumb"></param>
        /// <returns></returns>
        private async Task UpdateThumbnailCollectionAsync(string id, string thumb)
        {
            var item = deferredThumbnails.Where(child => child.data.name.Equals(id));
            if (item != null) {
                var itemSource = (ObservableCollection<Listing.Child>)ThumbnailGrid.ItemsSource;
                item.First().data.thumbnail = thumb;
                itemSource.Add(item.First());
                ThumbnailProgress.Value = 0;
            }
        }

        /// <summary>
        /// Clears and updates the listing for the given subreddit
        /// </summary>
        void UpdateListings()
        {
            ((ObservableCollection<Listing.Child>)ThumbnailGrid.ItemsSource).Clear();

            Subscribed item = (Subscribed)SubredditsAvailable.SelectedItem;
            var name = item.name;
            var pages = ((Config)DataContext).AppConfig.reddit_pages;
            Listing listings = RedditAPI.GetListing(name, pages);
            Uri downloadUrl = null;
            foreach (RedditImageBrowser.Json.Listing.Child child in listings.data.children) {
                try {
                    downloadUrl = new Uri(child.data.thumbnail);
                } catch (UriFormatException e) {
                    downloadUrl = null;
                }

                if (downloadUrl != null)
                    ThumbnailDownloader.AddDownload(child.data.name, downloadUrl, System.IO.Path.Combine(ApplicationConfig.AppConfig.thumbnail_directory, child.data.name + ".jpg") );
            }

            deferredThumbnails = listings.data.children;
        }

        void label_RemoveClicked(object sender, SubredditLabel.RemoveClickedEventArgs e)
        {
            //this.SubredditLabels.Children.Remove(((UIElement)sender));
        }


        /// <summary>
        /// The UI portion of when a subreddit is clicked on
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddSubReddit_Click(object sender, RoutedEventArgs e)
        {
            AddSubReddit dlg = new AddSubReddit();
            dlg.Owner = this;
            dlg.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            dlg.ShowDialog();
            
            if (dlg.DialogResult == true)
            {
                bool exists = false;
                foreach (Subscribed subreddit in SubredditsAvailable.ItemsSource) {
                    if (subreddit.name.ToLower().Equals(dlg.SubRedditText.Text.ToLower())) {
                        exists = true;
                        break;
                    }
                }
                if (!exists) {
                    AddSubreddit(dlg.SubRedditText.Text);
                }
            }
        }

        /// <summary>
        /// Tries to obtain the information for the subreddit and then create a new entry in the config
        /// This should probably happen in the configuration class not the ui class
        /// </summary>
        /// <param name="subreddit"></param>
        /// <returns></returns>
        private bool AddSubreddit(string subreddit)
        {
            bool valid = this.RedditAPI.ValidSubReddit(subreddit);

            if (!valid)
                return false;

            SubredditDetail detail = this.RedditAPI.SubredditDetails(subreddit);

            this.ApplicationConfig.AddSubreddit(detail);
            SubredditsAvailable.GetBindingExpression(ListView.ItemsSourceProperty).UpdateSource();
            this.ApplicationConfig.SaveSubreddits();
            return true;
        }

        /// <summary>
        /// Opens the configuration dialog and updates the view if changes were made to the config
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenConfiguration(object sender, RoutedEventArgs e)
        {
            Configuration config = new Configuration();
            config.ShowDialog();

            if (config.DialogResult == true) {
                // Update anything we need to update
                UpdateListings();
            }
        }

        /// <summary>
        /// Updates what subreddit to show based on the selection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubredditsAvailable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ThumbnailDownloader.CancelAllDownloads();
            UpdateListings();
        }

        /// <summary>
        /// When the window is closing we should gracefully close
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ThumbnailDownloader.Stop();
            ThumbnailDownloader.CancelAllDownloads(false);
            ThumbnailDownloader.Dispose();

            ImageDownloader.Stop();
            ImageDownloader.CancelAllDownloads(false);
            ImageDownloader.Dispose();
        }

        /// <summary>
        /// Refresh the subreddit selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RefreshSubreddits(object sender, RoutedEventArgs e)
        {
            ThumbnailDownloader.CancelAllDownloads();
            UpdateListings();
        }

        /// <summary>
        /// For every selected image lets download that mofo
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach (Listing.Child item in ThumbnailGrid.SelectedItems) {
                Uri downloadUrl = new Uri(item.data.url);
                // For imgur we can't downoad it yet, so skip it
                // TODO - we need API support for flickr / imgur / photobucket...etc 
                if (downloadUrl.AbsolutePath.LastIndexOf(".") > 0)
                    ImageDownloader.AddDownload(item.data.name, downloadUrl, System.IO.Path.Combine(((Config)DataContext).AppConfig.download_directory, item.data.name + downloadUrl.AbsolutePath.Substring(downloadUrl.AbsolutePath.LastIndexOf("."))));
            }
        }

        /// <summary>
        /// Scroll the reddits available down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollDown_MouseDown(object sender, RoutedEventArgs e)
        {
            SubredditScroller.ScrollToVerticalOffset(SubredditScroller.VerticalOffset + scrollOffset);
        }

        /// <summary>
        /// Scroll the reddits available up
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollUp_MouseDown(object sender, RoutedEventArgs e)
        {
            SubredditScroller.ScrollToVerticalOffset(SubredditScroller.VerticalOffset - scrollOffset);
        }
    }
}