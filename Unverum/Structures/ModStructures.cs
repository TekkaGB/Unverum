﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unverum.UI;

namespace Unverum
{
    public class Mod
    {
        public string name { get; set; }
        public bool enabled { get; set; }
    }
    public class Metadata
    {
        public Uri preview { get; set; }
        public string submitter { get; set; }
        public Uri avi { get; set; }
        public Uri upic { get; set; }
        public Uri caticon { get; set; }
        public string cat { get; set; }
        public string section { get; set; }
        public string description { get; set; }
        public Uri homepage { get; set; }
        public DateTime? lastupdate { get; set; }
    }
    public class Config
    {
        public string CurrentGame { get; set; }
        public bool KeepDownloads { get; set; }
        public Dictionary<string, GameConfig> Configs { get; set; }
        public double? LeftGridWidth { get; set; }
        public double? RightGridWidth { get; set; }
        public double? TopGridHeight { get; set; }
        public double? BottomGridHeight { get; set; }
        public double? Height { get; set; }
        public double? Width { get; set; }
        public bool Maximized { get; set; }
    }
    public class GameConfig
    {
        public string Launcher { get; set; }
        public bool LauncherOption { get; set; }
        public bool FirstOpen { get; set; }
        public string ModsFolder { get; set; }
        public long? PakLength { get; set; }
        public ObservableCollection<Mod> ModList { get; set; }
    }
}
