﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Szmyk.Utils.StringHelper;

namespace OutputUnitsUpdater
{
    public class SrcFile
    {
        string _srcPath;

        public SrcFile (string srcPath)
        {
            _srcPath = srcPath;
        }

        public List<string> GetScripts()
        {
            var scripts = getScripts(_srcPath);

            return scripts;
        }

        private List<string> getScripts(string srcFilePath)
        {
            var baseDirectory = Path.GetDirectoryName(srcFilePath);

            List<string> toReturn = new List<string>();

            var allLines = File.ReadAllLines(srcFilePath);

            var lines = allLines
                       .Select(x => StringHelper.RemoveComments(x))                                                   
                       .Where(x => String.IsNullOrWhiteSpace(x) == false);

            foreach (var line in lines)
            {
                var fullPath = Path.Combine(baseDirectory, line).Trim();

                var extension = getExtension(fullPath);

                if (extension == ".d")
                {
                    if (fullPath.Contains("*"))
                    {
                        var scripts = resolveWildcard(fullPath);

                        if (scripts.Count > 0)
                        {
                            toReturn.AddRange(scripts);
                        }
                        else
                        {
                            throw new MatchingFilesNotFoundException(line, Array.IndexOf(allLines, line) + 1);
                        }                    
                    }
                    else
                    {
                        if (File.Exists(fullPath))
                        {
                            toReturn.Add(fullPath);
                        }
                        else
                        {
                            throw new MatchingFilesNotFoundException(line, Array.IndexOf(allLines, line) + 1);
                        }                       
                    }
                }
                else if (extension == ".src")
                {
                    toReturn.AddRange(getScripts(fullPath));
                }             
            }

            return toReturn.Union(toReturn).ToList();
        }

        private string getExtension(string path)
        {
            return Path.GetExtension(path).ToLower();
        }

        private List<string> resolveWildcard(string path)
        {
            var wildcard = path.Substring(0, path.Length - 3);

            var directoryPath = Path.GetDirectoryName(path);

            var directoryFiles = Directory.GetFiles(directoryPath);
          
            return directoryFiles.Where(filePath => getExtension(path) == ".d" 
                                                 && filePath.IndexOf(wildcard, StringComparison.OrdinalIgnoreCase) == 0).ToList();
        }
    }
}
