﻿using System;
using System.IO;
using System.Text;

namespace Twinpack.Extensions
{
    public class DirectoryExtension
    {
        public static void RecreateDirectory(string directoryName, int retries=3)
        {
            int retry = 0;
            if (Directory.Exists(directoryName))
            {
                Directory.Delete(directoryName, true);

                // wait until the folder is actually removed, yep we have to do this in C#
                while (Directory.Exists(directoryName))
                {
                    if (retry > retries)
                        throw new IOException($"Directory {directoryName} could not be deleted");

                    System.Threading.Thread.Sleep(500);
                    retry++;
                }
            }
            Directory.CreateDirectory(directoryName);
        }

        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true, string searchPattern="*")
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles(searchPattern))
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, overwrite: true);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, recursive: true, searchPattern: searchPattern);
                }
            }
        }

        public static string RelativePath(string absPath, string relTo)
        { 
            string[] absDirs = absPath.Split('\\'); 
            string[] relDirs = relTo.Split('\\');        
            // Get the shortest of the two paths
            int len = absDirs.Length < relDirs.Length ? absDirs.Length : relDirs.Length;
            // Use to determine where in the loop we exited
            int lastCommonRoot = -1;
            int index;
            // Find common root
            for (index = 0; index < len; index++)
            {            
                if (absDirs[index] == relDirs[index])
                    lastCommonRoot = index;
                else
                    break;
            }        
            
            // If we didn't find a common prefix then throw
            if (lastCommonRoot == -1)
            {            
                throw new ArgumentException($"Path is not located in {absPath}");
            }        
            // Build up the relative path
            StringBuilder relativePath = new StringBuilder();
            // Add on the ..
            for (index = lastCommonRoot + 1; index < absDirs.Length; index++)   
            {            
                if (absDirs[index].Length > 0) 
                    relativePath.Append("..\\");
            }
            // Add on the folders
            for (index = lastCommonRoot + 1; index < relDirs.Length - 1; index++)
            {            
                relativePath.Append(relDirs[index] + "\\");
            }       
            relativePath.Append(relDirs[relDirs.Length - 1]);        
            return relativePath.ToString();
        }
    }
}
