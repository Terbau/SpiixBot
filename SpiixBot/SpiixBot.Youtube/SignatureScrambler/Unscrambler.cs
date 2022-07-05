using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SpiixBot.Youtube.SignatureScrambler
{
    internal static class StringExtensions
    {
        public static string SwapChars(this string s, int firstCharIndex, int secondCharIndex) => new StringBuilder(s)
        {
            [firstCharIndex] = s[secondCharIndex],
            [secondCharIndex] = s[firstCharIndex]
        }.ToString();

        public static string Reverse(this string s)
        {
            var buffer = new StringBuilder(s.Length);

            for (var i = s.Length - 1; i >= 0; i--)
                buffer.Append(s[i]);

            return buffer.ToString();
        }
    }

    internal class Unscrambler
    {
        private List<Func<string, string>> _operations = new List<Func<string, string>>();

        public Unscrambler(string body)
        {
            string scramblerBody = Regex.Match(body, @"(\w+)=function\(\w+\){(\w+)=\2\.split\(\x22{2}\);.*?return\s+\2\.join\(\x22{2}\)}")
                .Groups[0]
                .Value;
            if (string.IsNullOrWhiteSpace(scramblerBody)) throw new NotFoundException();

            string objName = Regex.Match(scramblerBody, "([\\$_\\w]+).\\w+\\(\\w+,\\d+\\);")
                .Groups[1]
                .Value;
            if (string.IsNullOrWhiteSpace(objName)) throw new NotFoundException();

            string escapedObjName = Regex.Escape(objName);
            string scramblerDefinition = Regex.Match(body, $@"var\s+{escapedObjName}=\{{(\w+:function\(\w+(,\w+)?\)\{{(.*?)\}}),?\}};", RegexOptions.Singleline)
                .Groups[0]
                .Value;
            if (string.IsNullOrWhiteSpace(scramblerDefinition)) throw new NotFoundException();

            // Actual making
            foreach (string statement in scramblerBody.Split(';'))
            {
                // Get the name of the function called in this statement
                var calledFuncName = Regex.Match(statement, @"\w+(?:.|\[)(\""?\w+(?:\"")?)\]?\(").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(calledFuncName))
                    continue;

                // Slice
                if (Regex.IsMatch(scramblerDefinition,
                    $@"{Regex.Escape(calledFuncName)}:\bfunction\b\([a],b\).(\breturn\b)?.?\w+\."))
                {
                    var index = Int32.Parse(Regex.Match(statement, @"\(\w+,(\d+)\)").Groups[1].Value);
                    _operations.Add(input => input.Substring(index));
                }

                // Swap
                else if (Regex.IsMatch(scramblerDefinition,
                    $@"{Regex.Escape(calledFuncName)}:\bfunction\b\(\w+\,\w\).\bvar\b.\bc=a\b"))
                {
                    var index = Int32.Parse(Regex.Match(statement, @"\(\w+,(\d+)\)").Groups[1].Value);
                    _operations.Add(input => input.SwapChars(0, index));
                }

                // Reverse
                else if (Regex.IsMatch(scramblerDefinition,
                    $@"{Regex.Escape(calledFuncName)}:\bfunction\b\(\w+\)"))
                {
                    _operations.Add(input => input.Reverse());
                }
            }
        }

        public string Unscramble(string input)
        {
            foreach (var operation in _operations)
            {
                input = operation(input);
            }

            return input;
        }

    }
}
