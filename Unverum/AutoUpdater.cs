using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Onova;
using Onova.Services;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using Unverum.UI;
using System.Windows.Media.Imaging;

namespace Unverum
{
    public class AutoUpdater
    {
        private static ProgressBox progressBox;
        private static HttpClient client = new HttpClient();

        public static async Task<bool> CheckForUnverumUpdate(CancellationTokenSource cancellationToken)
        {
            // Get Version Number
            var localVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
            try
            {
                var requestUrl = $"https://api.gamebanana.com/Core/Item/Data?itemtype=Tool&itemid=7162&fields=Updates().bSubmissionHasUpdates()," +
                    $"Updates().aGetLatestUpdates(),Files().aFiles()&return_keys=1";
                GameBananaItem response = JsonSerializer.Deserialize<GameBananaItem>(await client.GetStringAsync(requestUrl));
                if (response == null)
                {
                   MessageBox.Show("Error whilst checking for Unverum update: No response from GameBanana API");
                    return false;
                }
                if (response.HasUpdates)
                {
                    GameBananaItemUpdate[] updates = response.Updates;
                    string updateTitle = updates[0].Title;
                    Match onlineVersionMatch = Regex.Match(updateTitle, @"(?<version>([1-9]+\.?)+)[^a-zA-Z]");
                    string onlineVersion = null;
                    if (onlineVersionMatch.Success)
                    {
                        onlineVersion = onlineVersionMatch.Value;
                    }
                    if (UpdateAvailable(onlineVersion, localVersion))
                    {
                        ChangelogBox notification = new ChangelogBox(updates[0], "Unverum", $"A new version of Unverum is available (v{onlineVersion})!", null);
                        notification.ShowDialog();
                        notification.Activate();
                        if (notification.YesNo)
                        {
                            Dictionary<String, GameBananaItemFile> files = response.Files;
                            string downloadUrl = files.ElementAt(0).Value.DownloadUrl;
                            string fileName = files.ElementAt(0).Value.FileName;
                            // Download the update
                            await DownloadUnverum(downloadUrl, fileName, onlineVersion, new Progress<DownloadProgress>(ReportUpdateProgress), cancellationToken);
                            // Notify that the update is about to happen
                            MessageBox.Show($"Finished downloading {fileName}!\nUnverum will now restart.", "Notification", MessageBoxButton.OK);
                            // Update Unverum
                            UpdateManager updateManager = new UpdateManager(new LocalPackageResolver($"{Global.assemblyLocation}{Global.s}Downloads{Global.s}UnverumUpdate"), new ZipExtractor());
                            if (!Version.TryParse(onlineVersion, out Version version))
                            {
                                MessageBox.Show($"Error parsing {onlineVersion}!\nCancelling update.", "Notification", MessageBoxButton.OK);
                                return false;
                            }
                            // Updates and restarts Unverum
                            await updateManager.PrepareUpdateAsync(version);
                            updateManager.LaunchUpdater(version);
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
            return false;
        }
        private static async Task DownloadUnverum(string uri, string fileName, string version, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken)
        {
            try
            {
                // Create the downloads folder if necessary
                if (!Directory.Exists(@$"{Global.assemblyLocation}{Global.s}Downloads"))
                {
                    Directory.CreateDirectory(@$"{Global.assemblyLocation}{Global.s}Downloads");
                }
                // Create the downloads folder if necessary
                if (!Directory.Exists(@$"{Global.assemblyLocation}{Global.s}Downloads{Global.s}UnverumUpdate"))
                {
                    Directory.CreateDirectory(@$"{Global.assemblyLocation}{Global.s}Downloads{Global.s}UnverumUpdate");
                }
                progressBox = new ProgressBox(cancellationToken);
                progressBox.progressBar.Value = 0;
                progressBox.progressText.Text = $"Downloading {fileName}";
                progressBox.Title = "Unverum Update Progress";
                progressBox.finished = false;
                progressBox.Show();
                progressBox.Activate();
                // Write and download the file
                using (var fs = new FileStream(
                    $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}UnverumUpdate/{fileName}", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await client.DownloadAsync(uri, fs, fileName, progress, cancellationToken.Token);
                }
                // Rename the file
                if (!File.Exists($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}UnverumUpdate{Global.s}{version}.7z"))
                {
                    File.Move($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}UnverumUpdate{Global.s}{fileName}", $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}UnverumUpdate{Global.s}{version}.7z");
                }
                progressBox.Close();
            }
            catch (OperationCanceledException)
            {
                // Remove the file is it will be a partially downloaded one and close up
                File.Delete(@$"{Global.assemblyLocation}{Global.s}Downloads{Global.s}UnverumUpdate{Global.s}{fileName}");
                if (progressBox != null)
                {
                    progressBox.finished = true;
                    progressBox.Close();
                }
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Error whilst downloading {fileName} {e.Message}");
                if (progressBox != null)
                {
                    progressBox.finished = true;
                    progressBox.Close();
                }
            }
        }
        private static void ReportUpdateProgress(DownloadProgress progress)
        {
            if (progress.Percentage == 1)
            {
                progressBox.finished = true;
            }
            progressBox.progressBar.Value = progress.Percentage * 100;
            progressBox.taskBarItem.ProgressValue = progress.Percentage;
            progressBox.progressTitle.Text = $"Downloading {progress.FileName}...";
            progressBox.progressText.Text = $"{Math.Round(progress.Percentage * 100, 2)}% " +
                $"({StringConverters.FormatSize(progress.DownloadedBytes)} of {StringConverters.FormatSize(progress.TotalBytes)})";
        }
        private static bool UpdateAvailable(string onlineVersion, string localVersion)
        {
            if (onlineVersion is null || localVersion is null)
            {
                return false;
            }
            string[] onlineVersionParts = onlineVersion.Split('.');
            string[] localVersionParts = localVersion.Split('.');
            // Pad the version if one has more parts than another (e.g. 1.2.1 and 1.2)
            if (onlineVersionParts.Length > localVersionParts.Length)
            {
                for (int i = localVersionParts.Length; i < onlineVersionParts.Length; i++)
                {
                    localVersionParts = localVersionParts.Append("0").ToArray();
                }
            }
            else if (localVersionParts.Length > onlineVersionParts.Length)
            {
                for (int i = onlineVersionParts.Length; i < localVersionParts.Length; i++)
                {
                    onlineVersionParts = onlineVersionParts.Append("0").ToArray();
                }
            }
            // Decide whether the online version is new than local
            for (int i = 0; i < onlineVersionParts.Length; i++)
            {
                if (!int.TryParse(onlineVersionParts[i], out _))
                {
                    MessageBox.Show($"Couldn't parse {onlineVersion}");
                    return false;
                }
                if (!int.TryParse(localVersionParts[i], out _))
                {
                    MessageBox.Show($"Couldn't parse {localVersion}");
                    return false;
                }
                if (int.Parse(onlineVersionParts[i]) > int.Parse(localVersionParts[i]))
                {
                    return true;
                }
                else if (int.Parse(onlineVersionParts[i]) != int.Parse(localVersionParts[i]))
                {
                    return false;
                }
            }
            return false;
        }
    }
}
