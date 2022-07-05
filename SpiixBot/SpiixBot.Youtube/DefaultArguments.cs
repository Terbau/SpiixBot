using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Youtube
{
    public class DefaultArguments
    {
        string Format { get; set; } = "bestaudio/best";
        string SourceAddress { get; set; } = "0.0.0.0";
        bool NoCheckCertificate { get; set; } = true;
        bool IgnoreErrors { get; set; } = true;
        bool NoWarnings { get; set; } = true;

        internal string GetAsArguments()
        {
            var arguments = new List<string>();

            arguments.Add("-f " + Format);
            arguments.Add("--source-address " + SourceAddress);

            if (NoCheckCertificate) arguments.Add("--no-check-certificate");
            if (IgnoreErrors) arguments.Add("--ignore-errors");
            if (NoWarnings) arguments.Add("--no-warnings");

            return string.Join(" ", arguments);
        }
    }
}
