using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twinpack.Configuration;

namespace Twinpack.Configuration
{
    public class ConfigUtils
    {

        public static List<T> ProcessModules<T>(Config config, Func<Config, T> action)
        {
            var results = new List<T>();
            
            if (config.Modules == null || config.Modules.Count == 0)
                return results;

            var paths = new Stack<string>();
            paths.Push(Environment.CurrentDirectory);

            foreach (var module in config.Modules)
            {
                paths.Push(Path.Combine(Environment.CurrentDirectory, module));
                Environment.CurrentDirectory = paths.Peek();

                try
                {
                    var moduleConfig = ConfigFactory.Load();

                    if (moduleConfig != null)
                    {
                        var result = action(moduleConfig);
                        if (result != null)
                            results.Add(result);
                    }
                }
                finally
                {
                    paths.Pop();
                    Environment.CurrentDirectory = paths.Peek();
                }
            }

            return results;
        }

        public static async Task ProcessModulesAsync(Config config, Func<Config, Task> action)
        {
            if (config.Modules == null || config.Modules.Count == 0)
                return;

            var paths = new Stack<string>();
            paths.Push(Environment.CurrentDirectory);

            foreach (var module in config.Modules)
            {
                paths.Push(Path.Combine(Environment.CurrentDirectory, module));
                Environment.CurrentDirectory = paths.Peek();

                try
                {
                    var moduleConfig = ConfigFactory.Load();

                    if (moduleConfig != null)
                    {
                        await action(moduleConfig);
                    }
                }
                finally
                {
                    paths.Pop();
                    Environment.CurrentDirectory = paths.Peek();
                }
            }
        }

        public static async Task<T> ProcessModulesAsync<T>(Config config, Func<Config, Task<T>> action)
        {
            if (config.Modules == null || config.Modules.Count == 0)
                return default(T);

            var paths = new Stack<string>();
            paths.Push(Environment.CurrentDirectory);

            foreach (var module in config.Modules)
            {
                paths.Push(Path.Combine(Environment.CurrentDirectory, module));
                Environment.CurrentDirectory = paths.Peek();

                try
                {
                    var moduleConfig = ConfigFactory.Load();

                    if (moduleConfig != null)
                    {
                        var result = await action(moduleConfig);
                        if (result != null)
                            return result;
                    }
                }
                finally
                {
                    paths.Pop();
                    Environment.CurrentDirectory = paths.Peek();
                }
            }

            return default(T);
        } 

        public static async Task<List<T>> ProcessModulesCollectAsync<T>(Config config, Func<Config, Task<T>> action)
        {
            var results = new List<T>();
            
            if (config.Modules == null || config.Modules.Count == 0)
                return results;

            var paths = new Stack<string>();
            paths.Push(Environment.CurrentDirectory);

            foreach (var module in config.Modules)
            {
                paths.Push(Path.Combine(Environment.CurrentDirectory, module));
                Environment.CurrentDirectory = paths.Peek();

                try
                {
                    var moduleConfig = ConfigFactory.Load();

                    if (moduleConfig != null)
                    {
                        var result = await action(moduleConfig);
                        if (result != null)
                            results.Add(result);
                    }
                }
                finally
                {
                    paths.Pop();
                    Environment.CurrentDirectory = paths.Peek();
                }
            }

            return results;
        }        
    }
}
