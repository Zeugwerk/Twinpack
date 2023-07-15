using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack.Extensions
{
    public class DirectoryExtension
    {
        public static void RecreateDirectory(string directoryName, int retries = 3)
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
    }
}
