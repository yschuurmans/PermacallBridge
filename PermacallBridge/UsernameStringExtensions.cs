using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PermacallBridge
{
    public static class UsernameStringExtensions
    {
        public static string FixNickname(this string name)
        {
            var tempName = name;
            tempName = Regex.Replace(tempName, "[^a-zA-Z0-9, ]", "*");
            while (tempName.Contains("**"))
            {
                tempName = tempName.Replace("**", "*");
            }

            tempName = tempName.Length > 26 ? tempName.Substring(0, 26) + "..." : tempName;

            return tempName;
        }
    }
}
