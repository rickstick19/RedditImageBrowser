﻿using Newtonsoft.Json;
using RedditImageBrowser.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RedditImageBrowser.Common
{
    class Config
    {
        private static readonly string default_subreddits_subscribed = "subreddits.json";
        private static readonly string default_config = "config.json";
        private static readonly string default_config_dir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static readonly int max_pages = 2;
        private static readonly string default_img_dir = "Reddit";
        public static readonly string default_download_dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), default_img_dir);
        public static readonly string[] supporfted_file_formats = { ".jpeg", ".jpg", ".bmp", ".png" };

        public AppConfig AppConfig { get; protected set; }
        public Subreddits SubredditsSubscribed { get; set; }

        /// <summary>
        /// Using the subreddit detail add a new subscription
        /// </summary>
        /// <param name="detail"></param>
        public void AddSubreddit(SubredditDetail detail)
        {
            Subscribed newSubscription = new Subscribed();
            newSubscription.name = "/r/" + detail.data.display_name;
            newSubscription.id = detail.data.name;
            newSubscription.description = detail.data.public_description;
            newSubscription.downloaded = new List<string>();

            SubredditsSubscribed.Add(newSubscription);
        }

        public Config()
        {
            Init();
        }

        /// <summary>
        /// Initialize the configuration object for use
        /// </summary>
        public void Init()
        {
            LoadConfig();
            LoadSubreddits();
        }

        public string ConfigFilePath
        {
            get { return Path.Combine(default_config_dir, default_config); }
        }

        public string SubredditFilePath
        {
            get { return Path.Combine(default_config_dir, default_subreddits_subscribed); }
        }

        /// <summary>
        /// Saves the subreddit config file
        /// </summary>
        public void SaveSubreddits()
        {
            Save(SubredditsSubscribed, SubredditFilePath);
        }

        /// <summary>
        /// Saves the config file
        /// </summary>
        public void SaveConfig()
        {
            Save(AppConfig, ConfigFilePath);
        }

        private void Save(Object obj, string path)
        {
            using (var config_stream = new StreamWriter(File.Open(path, FileMode.Truncate, FileAccess.Write, FileShare.Read))) 
            {
                string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
                config_stream.Write(json);
                config_stream.Flush();
            }
        }

        /// <summary>
        /// Loads the default config file
        /// If missing it will be re-created with the defaults TODO
        /// </summary>
        public void LoadConfig()
        {
            AppConfig = PopulateJSON<AppConfig>(ConfigFilePath, string.Format(@"{{
    ""username"":"""",
    ""password"":"""",
    ""modhash"":"""",
    ""cookie"":"""",
    ""download_directory"": ""{0}"",
    ""reddit_pages"": {1}
}}",
              Regex.Replace(default_download_dir, @"(?<!\\)\\(?!\\)", @"\\", RegexOptions.IgnorePatternWhitespace),
              max_pages));
        }

        /// <summary>
        /// Loads the default subreddits file
        /// If missing it will be re-created with the defaults TODO 
        /// </summary>
        public void LoadSubreddits()
        {
            SubredditsSubscribed = PopulateJSON<Subreddits>(SubredditFilePath, string.Format(@"[
{{
    ""id"":""t5_2sbq3"",
    ""description"":"""",
    ""name"":""/r/EarthPorn"",
    ""downloaded"":""""
}}
]"));
        }

        /// <summary>
        /// Loads and returns the serialized json file from the system
        /// </summary>
        /// <typeparam name="T">The type of json we are deserializing</typeparam>
        /// <param name="path">The path to the file</param>
        /// <returns>A deserialized type of T</returns>
        private T PopulateJSON<T>(string path, string def="{}")
        {
            using (var config_stream = new StreamReader(File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)))
            {
                string contents = def;
                if (!ReadContents(config_stream, ref contents))
                {
                    StreamWriter sw = new StreamWriter(config_stream.BaseStream);
                    sw.Write(contents);
                    sw.Flush();
                }
                return JsonConvert.DeserializeObject<T>(contents);
            }
        }

        /// <summary>
        /// Read the contents of the file to the end and if the content is an empty string return an empty json object
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private bool ReadContents(StreamReader reader, ref string contents)
        {
            string read = reader.ReadToEnd();

            if (!read.Equals(""))
            {
                contents = read;
                return true;
            }

            return false;
        }

    }
}
