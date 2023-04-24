using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace cron_yaml
{
    public static class Extensions
    {
        public static string ReplaceInvalidFilenameChars(this string filename)
        {
            string regexString = string.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars())));
            Regex regex = new Regex(regexString);
            return regex.Replace(filename, "_");
        }

        public static string ReplaceInvalidPathChars(this string directory)
        {
            string regexString = string.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidPathChars())));
            Regex regex = new Regex(regexString);
            return regex.Replace(directory, "_");
        }
    }
}
