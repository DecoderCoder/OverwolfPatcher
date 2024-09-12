using Bluscream;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OverwolfPatcher.Classes
{
    public class Overwolf
    {
        private const string ProcessName = "overwolf";

        public DirectoryInfo ProgramFolder {  get; set; }
        public DirectoryInfo DataFolder {  get; set; }

        public DirectoryInfo ExtensionsFolder => DataFolder.Combine("Extensions");
        public List<DirectoryInfo> ProgramVersionFolders => ProgramFolder.GetDirectories(searchPattern: "*.*.*.*").ToList(); // C:\Program Files (x86)\Overwolf\0.258.1.7\
        public List<Process> Processes => Process.GetProcessesByName(ProcessName).ToList();
    }
}
