using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SpiixBot.Util
{
    public static class FileUtil
    {
        internal static string CleanUpPath(string path)
        {
            if (!path.StartsWith('/') && !path.StartsWith("//"))
            {
                path = "\\" + path;
            }

            path = path.Replace('\\', '/');
            return path;
        }

        public static string GetExecutionPath()
            => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static string GetBaseDirectoryPath()
        {
            string currentPath = GetExecutionPath();
            string mainDir = string.Join('\\', currentPath.Split('\\').TakeWhile(part => part != "bin"));

            return mainDir;
        }

        public static JObject ParseJsonFile(string fileName)
        {
            return JObject.Parse(File.ReadAllText(GetBaseDirectoryPath() + CleanUpPath(fileName)));
        }

        public static string ReadFile(string fileName)
            => File.ReadAllText(GetExecutionPath() + CleanUpPath(fileName));
    }
}
