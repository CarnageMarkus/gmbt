﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using VdfsSharp;

using Szmyk.Utils.Paths;

namespace GMBT
{
    internal enum TestMode
    {
        Quick,
        Full
    }

    /// <summary>
    /// Implements the test mode.
    /// </summary>
    internal class Test : Mode
    {
        public TestMode Mode { get; private set; }

        private bool assetsCompiled;

        public Test(Gothic gothic, TestMode testMode) : base(gothic)
        {
            Mode = testMode;

            compilingAssetsWatcher.OnFileCompile = (compilingFile) =>
            {              
                if (compilingFile.Contains("MENU.DAT"))
                {
                    if ((Mode == TestMode.Full
                    && (assetsCompiled == false)))
                    {
                        gothic.EndProcess();
                    }
                }
            };
        }

        protected override void runHooks(HookType hookType, HookEvent hookEvent)
        {
            Program.HooksManager.RunHooks(HookMode.Common, hookType, hookEvent);
            Program.HooksManager.RunHooks(HookMode.Test, hookType, hookEvent);

            if (Mode == TestMode.Full)
            {
                Program.HooksManager.RunHooks(HookMode.FullTest, hookType, hookEvent);
            }
            else if (Mode == TestMode.Quick)
            {
                Program.HooksManager.RunHooks(HookMode.QuickTest, hookType, hookEvent);
            }
        }

        public void DetectIfWorldIsNotExists()
        {
            if (File.Exists(Path.Combine(gothic.GetGameDirectory(Gothic.GameDirectory.Data), "Worlds.vdf")) == false)
            {
                Logger.Fatal("Test.Error.RequireReinstall".Translate("Worlds.vdf"));
            }

            var worlds = new VdfsReader(gothic.GetGameDirectory(Gothic.GameDirectory.Data) + "Worlds.vdf")
                .ReadEntries(false)
                .Where(x => x.Name.EndsWith(".ZEN", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name).ToList();

            if (gothic.Version == Gothic.GameVersion.Gothic2)
            {
                if (File.Exists(Path.Combine(gothic.GetGameDirectory(Gothic.GameDirectory.Data), "Worlds_Addon.vdf")) == false)
                {
                    Logger.Fatal("Test.Error.RequireReinstall".Translate("Worlds_Addon.vdf"));
                }

                worlds.AddRange(new VdfsReader(gothic.GetGameDirectory(Gothic.GameDirectory.Data) + "Worlds_Addon.vdf")
                    .ReadEntries(false)
                    .Where(x => x.Name.EndsWith(".ZEN", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Name).ToList());
            }
           
            foreach (var dir in Program.Config.ModFiles.Assets)
            {
                var worldsDir = Path.Combine(dir, "Worlds");

                if (Directory.Exists(worldsDir))
                {
                    worlds.AddRange(Directory.GetFiles(worldsDir, "*.ZEN", SearchOption.AllDirectories).ToList());
                }              
            }

            var world = Program.Options.TestVerb.World ?? Program.Config.ModFiles.DefaultWorld;

            if (worlds.Where(x => Path.GetFileName(x) == Path.GetFileName(world)).Count() < 1)
            {
                Logger.Fatal("Config.Error.FileDidNotFound".Translate(world));
            }
        }

        List<string> disabledVdfs = new List<string>();

        public void EnableVdfs()
        {
            foreach (var vdf in disabledVdfs)
            {
                File.Move(vdf, PathsUtils.ChangeExtension(vdf, ".vdf"));
            }
        }

        public void DisableVdfs()
        {
            foreach (var vdf in Directory.GetFiles(gothic.GetGameDirectory(Gothic.GameDirectory.Data)))
            {
                var reader = new VdfsReader(vdf);

                var hasAnims = reader
                    .ReadEntries(false)
                    .Where(x => x.Name.Equals("ANIMS", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Name).Count() > 0;

                reader.Dispose();

                if (hasAnims)
                {
                    var newPath = PathsUtils.ChangeExtension(vdf, ".disabled");

                    disabledVdfs.Add(newPath);

                    File.Move(vdf, newPath);
                }    
            }        
        }

        /// <summary>
        /// Starts test.
        /// </summary>
        public override void Start()
        {
            DetectIfWorldIsNotExists();

            if (Program.Options.TestVerb.Merge != Merge.MergeOptions.None)
            {
                runHooks(HookType.Pre, HookEvent.AssetsMerge);

                Merge.MergeAssets(gothic, Program.Options.TestVerb.Merge);

                runHooks(HookType.Post, HookEvent.AssetsMerge);
            }

            if ((Program.Options.TestVerb.Merge == Merge.MergeOptions.Scripts)
            || (Program.Options.TestVerb.Merge == Merge.MergeOptions.All))
            {
                if (Program.Options.CommonTestBuild.NoUpdateSubtitles == false)
                {
                    runHooks(HookType.Pre, HookEvent.SubtitlesUpdate);

                    UpdateDialogs();

                    runHooks(HookType.Post, HookEvent.SubtitlesUpdate);
                }
            }

            if (Mode == TestMode.Full)
            {
                DisableVdfs();
            }

            compilingAssetsWatcher.Start();

            gothic.Start(GetGothicArguments()).WaitForExit();

            assetsCompiled = true;

            if (Mode == TestMode.Full)
            {
                EnableVdfs();

                gothic.Start(GetGothicArguments()).WaitForExit();             
            }

            compilingAssetsWatcher.Stop();
        }

        /// <summary>
        /// Returns arguments list that will be passed to game process.
        /// </summary>
        public override GothicArguments GetGothicArguments()
        {
            GothicArguments parameters = new GothicArguments();

            parameters.Add("zreparse");

            if (Program.Options.TestVerb.RunGothicWindowed
            || (Mode == TestMode.Full && assetsCompiled == false))
            {
                parameters.Add("zwindow"); 
            }

            if (Program.Options.TestVerb.InGameTime != null)
            {
                parameters.Add("time", Program.Options.TestVerb.InGameTime);
            }
       
            parameters.Add("vdfs", "physicalfirst");

            if (Program.Options.TestVerb.DevMode)
            {
                parameters.Add("devmode");
            }
          
            if (Mode == TestMode.Full && assetsCompiled == false)
            {
                parameters.Add("3d", "none");
                parameters.Add("zconvertall");
                parameters.Add("ztexconvert");              
                parameters.Add("nomenu");
                parameters.Add("zautoconvertdata");
            }
            else
            {
                parameters.Add("3d", Program.Options.TestVerb.World ?? Program.Config.ModFiles.DefaultWorld);
            }

            if (Program.Options.TestVerb.NoAudio)
            {
                if (File.Exists(gothic.GetGameDirectory(Gothic.GameDirectory.ScriptsCompiled) + "MUSIC.DAT"))
                {
                    parameters.Add("znomusic");
                }

                if (File.Exists(gothic.GetGameDirectory(Gothic.GameDirectory.ScriptsCompiled) + "SFX.DAT"))
                {
                    parameters.Add("znosound");
                }
            }

            if (Program.Options.TestVerb.NoMenu)
            {
                parameters.Add("nomenu");
            }

            return parameters;
        }
    }
}
