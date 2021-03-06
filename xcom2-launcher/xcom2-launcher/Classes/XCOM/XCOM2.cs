﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Steamworks;
using XCOM2Launcher.Classes.Steam;
using XCOM2Launcher.Mod;

namespace XCOM2Launcher.XCOM
{
    public static class XCOM2
    {
        public const uint APPID = 268500;

        private static string _gameDir;

        public static string GameDir
        {
            get { return _gameDir ?? (_gameDir = DetectGameDir()); }
            set { _gameDir = value; }
        }

        public static string UserConfigDir
            => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\my games\XCOM2\XComGame\Config";

        public static string DefaultConfigDir => Path.Combine(GameDir, @"XComGame\Config");

        public static string DetectGameDir()
        {
            // try steam
            string gamedir;
            if (SteamApps.GetAppInstallDir((AppId_t) APPID, out gamedir, 100) > 0 && Directory.Exists(gamedir))
            {
                _gameDir = gamedir;
                return gamedir;
            }

            // try modding dirs
            var dirs = DetectModDirs();
            foreach (var dir in dirs)
                if (dir.ToLower().Contains("\\steamapps\\"))
                {
                    _gameDir = Path.GetFullPath(Path.Combine(dir, "../../..", "common", "XCOM 2"));
                    return _gameDir;
                }

            // abandon hope
            return "";
        }

        public static void RunGame(string gameDir, string args)
        {
            if (!SteamAPIWrapper.Init())
                MessageBox.Show("Could not connect to steam.");


            var p = new Process
            {
                StartInfo =
                {
                    Arguments = args,
                    FileName = gameDir + @"\Binaries\Win64\XCom2.exe",
                    WorkingDirectory = gameDir
                }
            };

            p.Start();

            SteamAPIWrapper.Shutdown();
        }

        internal static void ImportActiveMods(Settings settings)
        {
            // load active mods
            foreach (var internalName in GetActiveMods())
                foreach (var mod in settings.Mods.All.Where(m => m.ID == internalName))
                    mod.isActive = true;
        }

        public static string[] DetectModDirs()
        {
            var dirs = new List<string>();

            foreach (var line in File.ReadLines(Path.Combine(UserConfigDir, "XComEngine.ini")))
            {
                if (!line.StartsWith("ModRootDirs="))
                    continue;

                var dir = line.Substring(12);

                if (!Path.IsPathRooted(dir))
                    dir = Path.GetFullPath(Path.Combine(GameDir, "bin", "Win64", dir));

                if (Directory.Exists(dir))
                    dirs.Add(dir);
            }

            return dirs.ToArray();
        }

        public static string[] GetActiveMods()
        {
            return File.ReadLines(Path.Combine(UserConfigDir, "XComModOptions.ini")).Where(line => line.StartsWith("ActiveMods=")).Select(line => line.Substring(11)).ToArray();
        }

        public static void SetActiveMods(List<ModEntry> mods)
        {
            // XComModOptions
            var file = Path.Combine(UserConfigDir, "XComModOptions.ini");

            if (!File.Exists(file + ".bak"))
                // create backup
                File.Copy(file, file + ".bak");

            var modOptions = new ConfigFile("ModOptions", false);

            foreach (var m in mods.OrderBy(m => m.Index))
                modOptions.Add("Engine.XComModOptions", "ActiveMods", m.ID);

            modOptions.UpdateTimestamp();
            modOptions.Save();

            //// XComEngine
            file = Path.Combine(UserConfigDir, "XComEngine.ini");

            // Create back up
            var backup = file + ".bak";
            File.Copy(file, backup, true);

            // Write
            using (var inStream = new FileStream(backup, FileMode.Open))
            using (var outStream = new FileStream(file, FileMode.Truncate))
            using (var reader = new StreamReader(inStream))
            using (var writer = new StreamWriter(outStream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line == null || line.StartsWith("+ModClassOverrides=") || line.StartsWith("ModClassOverrides="))
                        // Skip old mod entries
                        continue;

                    writer.WriteLine(line);
                }
            }
        }
    }
}