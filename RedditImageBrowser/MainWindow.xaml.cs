﻿using RedditImageBrowser.Common;
using RedditImageBrowser.CustomControls;
using RedditImageBrowser.DataSource;
using RedditImageBrowser.Json;
using RedditImageBrowser.Net;
using RedditImageBrowser.Net.Api;
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
using System.Windows.Media.Effects;
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
        Reddit RedditAPI = null;
        DownloadManager ThumbnailDownloader = null;
        DownloadManager ImageDownloader = null;
        string NextPagePointer = "";
        string PrevPagePointer = "";
        

        /// <summary>
        /// The arbitrary and somewhat magical height of the reddit label
        /// </summary>
        int RedditScrollOffset = 35;

        /// <summary>
        /// Will be set after a configuration change that modifies the username and or password
        /// </summary>
        bool DoLogin = false;


        /// <summary>
        /// Gets the selected subreddit
        /// </summary>
        /// <returns></returns>
        private string SelectedSubreddit
        {
            get
            {
                Subscribed item = (Subscribed)SubredditsAvailable.SelectedItem;
                if (item != null)
                    return item.name;

                return "";
            }
        }

        /// <summary>
        /// Gets the pages to display 
        /// </summary>
        private int PagesToDisplay
        {
            get
            {
                return ((Config)DataContext).AppConfig.reddit_pages;
            }
        }

        /// <summary>
        /// A deferred set of thumbnails to be shown as their respective thumbnails are complete
        /// </summary>
        private ObservableCollection<Listing.Child> deferredThumbnails;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplicationConfig = DataContext as Config;
            RedditAPI = new Reddit();

            {
                ImageDownloader = new DownloadManager(10, 750);
                ImageDownloader.DownloadComplete += ImageDownloader_DownloadComplete;
                ImageDownloader.DownloadProgressChanged += ImageDownloader_DownloadProgressChanged;
                ImageDownloader.Start();
            }
            {
                ThumbnailDownloader = new DownloadManager(5, 250); // we set this to one so that they come in serially 
                ThumbnailDownloader.DownloadComplete += ThumbDownloader_DownloadComplete;
                ThumbnailDownloader.DownloadProgressChanged += ThumbDownloader_DownloadProgressChanged;
                ThumbnailDownloader.Start();
            }

            ((Config)DataContext).AppConfig.PropertyChanged += AppConfig_PropertyChanged;

            SubredditsAvailable.SelectedIndex = 0;

            // Try to login
            App.Current.Dispatcher.InvokeAsync(() =>
            {
                Login();
            });
        }

        /// <summary>
        /// When the properties changre we need to make specific changes to the app
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AppConfig_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var config = (AppConfig)sender;
            switch (e.PropertyName) {
            case "reddit_pages":
                ThumbnailDownloader.CancelAllDownloads();
                UpdateListings();
                break;
            case "username":
                DoLogin = true;
                break;
            case "password":
                DoLogin = true;
                break;
            default:
                break;
            }
        }

        /// <summary>
        /// Login to reddit and obtain details about myself
        /// </summary>
        void Login()
        {
            AboutMe details = null;//RedditAPI.UserInfo(ApplicationConfig.AppConfig.cookie);

            //if (!UpdateUserInfo(details)) {
                Login loginResults = null;
                //if (RedditAPI.Login(ApplicationConfig.AppConfig.username, ApplicationConfig.AppConfig.password, out loginResults)) {

                    // Store the cookie for later
                //    ApplicationConfig.AppConfig.cookie = loginResults.json.data.cookie;
                //    ApplicationConfig.SaveConfig();

                //    details = RedditAPI.UserInfo();
                //    UpdateUserInfo(details);
                //} else {
                    LoggedInAsDisplay.Text = "";
                //}
            //}
        }

        /// <summary>
        /// For the given aboutme details update the ui
        /// </summary>
        /// <param name="details"></param>
        /// <returns></returns>
        bool UpdateUserInfo(AboutMe details)
        {
            if (details.data != null) {
                LoggedInAsDisplay.Text = "/u/" + details.data.name;

                if (details.data.is_gold) {
                    LoggedInAsDisplay.Foreground = Brushes.Gold;
                }

                return true;
            }

            return false;
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
                deferredThumbnails.Remove(item.First());
                ThumbnailProgress.Value = 0;
            }
        }

        /// <summary>
        /// Clears and updates the listing for the given subreddit
        /// </summary>
        void UpdateListings()
        {
            ((ObservableCollection<Listing.Child>)ThumbnailGrid.ItemsSource).Clear();

            if (SelectedSubreddit.Equals(""))
                return;

            Listing listings = RedditAPI.GetListing(SelectedSubreddit, PagesToDisplay);
            DeferThumbnails(listings);
        }

        /// <summary>
        /// Adds the thumbnails to the deferred listings to be dispatched by the ui thread later
        /// </summary>
        /// <param name="listings"></param>
        private void DeferThumbnails(Listing listings)
        {
            Uri downloadUrl = null;
            foreach (RedditImageBrowser.Json.Listing.Child child in listings.data.children) {
                try {
                    downloadUrl = new Uri(child.data.thumbnail);
                } catch (UriFormatException e) {
                    downloadUrl = null;
                }

                if (downloadUrl != null) {
                    ThumbnailDownloader.AddDownload(child.data.name, downloadUrl, System.IO.Path.Combine(ApplicationConfig.AppConfig.thumbnail_directory, child.data.name + ".jpg"));
                }
            }

            if (deferredThumbnails != null)
                deferredThumbnails.Clear();

            deferredThumbnails = listings.data.children;
            NextPagePointer = listings.data.children.Last().data.name;
            PrevPagePointer = listings.data.children.First().data.name;
        }

        /// <summary>
        /// The UI portion of when a subreddit is clicked on
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddSubreddit_Click(object sender, RoutedEventArgs e)
        {
            AddSubreddit dlg = new AddSubreddit();
            dlg.Owner = this;
            dlg.ShowDialog();
            
            if (dlg.DialogResult == true)
            {
                bool exists = false;
                foreach (Subscribed subreddit in SubredditsAvailable.ItemsSource) {
                    if (subreddit.name.ToLower().Equals(dlg.SubredditText.Text.ToLower())) {
                        exists = true;
                        break;
                    }
                }
                if (!exists) {
                    AddSubreddit(dlg.SubredditText.Text);
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
            bool valid = this.RedditAPI.ValidSubreddit(subreddit);

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

            if (config.DialogResult == true && DoLogin) {
                App.Current.Dispatcher.InvokeAsync(() =>
                {
                    Login();
                });
                DoLogin = false;
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
            SubredditScroller.ScrollToVerticalOffset(SubredditScroller.VerticalOffset + RedditScrollOffset);
        }

        /// <summary>
        /// Scroll the reddits available up
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollUp_MouseDown(object sender, RoutedEventArgs e)
        {
            SubredditScroller.ScrollToVerticalOffset(SubredditScroller.VerticalOffset - RedditScrollOffset);
        }

        /// <summary>
        /// Navigates back a page
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            var list = (ObservableCollection<Listing.Child>)ThumbnailGrid.ItemsSource;
            if (list.Count > 0) {
                Listing listings = RedditAPI.GetListing(SelectedSubreddit, PagesToDisplay, false, PrevPagePointer);

                if (listings.data.children.Count == 0)
                    return;

                ThumbnailDownloader.CancelAllDownloads();
                list.Clear();
                DeferThumbnails(listings);
            }
        }

        /// <summary>
        /// Navigates to the next page
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            var list = (ObservableCollection<Listing.Child>)ThumbnailGrid.ItemsSource;
            if (list.Count > 0) {
                var lastItem = list.Last<Listing.Child>();
                Listing listings = RedditAPI.GetListing(SelectedSubreddit, PagesToDisplay, true, NextPagePointer);

                if (listings.data.children.Count == 0)
                    return;

                ThumbnailDownloader.CancelAllDownloads();
                list.Clear();
                DeferThumbnails(listings);
            }
        }

        /// <summary>
        /// Removes the subreddit from display and selects the first available subreddit if one would otherwise be unselected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubredditLabel_RemoveClicked(object sender, SubredditLabel.RemoveClickedEventArgs e)
        {
            Subreddits items = (Subreddits)SubredditsAvailable.ItemsSource;
            string removedSubreddit = ((SubredditLabel)sender).Text.ToLower();
            var found = items.Where(item => item.name.ToLower().Equals(removedSubreddit)).First();
            items.Remove(found);

            ApplicationConfig.SaveSubreddits();

            if (SubredditsAvailable.SelectedItem == null && SubredditsAvailable.Items.Count > 0)
                SubredditsAvailable.SelectedItem = SubredditsAvailable.Items[0];
        }
    }
}
