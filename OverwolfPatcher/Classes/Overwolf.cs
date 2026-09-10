using Bluscream;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OverwolfPatcher.Classes
{
    public class Overwolf
    {
        private const string ProcessName = "overwolf";
        internal const string BaseRegKey = @"SOFTWARE\WOW6432Node\Overwolf";
        internal const string MainRegKey = @"Software\Overwolf\Overwolf";
        internal static Uri UrlProtocol => new Uri("overwolfstore://");
        internal static Uri DownloadUrl => new Uri("https://download.overwolf.com/install/Download?utm_source=web_app_store");

        public DirectoryInfo ProgramFolder { get; set; }
        public DirectoryInfo DataFolder { get; set; }

        public DirectoryInfo ExtensionsFolder => DataFolder.Combine("Extensions");
        public List<DirectoryInfo> ProgramVersionFolders => ProgramFolder == null || !ProgramFolder.Exists
            ? new List<DirectoryInfo>()
            : ProgramFolder.GetDirectories()
                .Where(folder => File.Exists(Path.Combine(folder.FullName, "OverWolf.Client.Core.dll")))
                .OrderBy(folder => folder.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        // public DirectoryInfo WindowsDesktopApp => new DirectoryInfo(@"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.11");
        public List<Process> Processes => Process.GetProcessesByName(ProcessName).ToList();

        /// <summary>
        /// Gets the installed Overwolf extension directories. Each directory
        /// is named with the extension's 40-character store ID. Version
        /// directories and non-extension metadata are intentionally left
        /// below that level so callers can choose how to handle them.
        /// </summary>
        public List<DirectoryInfo> GetInstalledExtensions()
        {
            if (DataFolder == null || !ExtensionsFolder.Exists)
                return new List<DirectoryInfo>();

            return ExtensionsFolder.GetDirectories()
                .Where(directory => IsExtensionId(directory.Name))
                .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Gets the IDs for all installed Overwolf extensions in stable order.
        /// </summary>
        public List<string> GetInstalledExtensionIds() =>
            GetInstalledExtensions().Select(directory => directory.Name).ToList();

        /// <summary>
        /// Gets managed/native binary files below all installed extension
        /// directories. This is discovery only; callers still decide whether
        /// a particular file is safe and supported to modify.
        /// </summary>
        public List<FileInfo> GetInstalledExtensionBinaries()
        {
            return GetInstalledExtensions()
                .SelectMany(directory => directory.GetFiles("*", SearchOption.AllDirectories))
                .Where(file => string.Equals(file.Extension, ".dll", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(file.Extension, ".exe", StringComparison.OrdinalIgnoreCase))
                .GroupBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static bool IsExtensionId(string value)
        {
            return value != null && value.Length == 40 &&
                value.All(character => character >= 'a' && character <= 'p');
        }
    }
}
