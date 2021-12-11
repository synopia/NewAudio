using System.Text.RegularExpressions;

namespace VL.NewAudio.Core
{
    internal static class Helper
    {
        private static readonly Regex FSpaceAndCharRegex = new(" [a-zA-Z]", RegexOptions.Compiled);
        private static readonly Regex FLowerAndUpperRegex = new("[a-z0-9][A-Z0-9]", RegexOptions.Compiled);

        public static string UpperCaseAfterSpace(this string name)
        {
            return FSpaceAndCharRegex.Replace(name, m => $" {char.ToUpper(m.Value[1])}");
        }

        public static string InsertSpaces(this string name)
        {
            return FLowerAndUpperRegex.Replace(name, m => $"{m.Value[0]} {m.Value[1]}");
        }
    }
}