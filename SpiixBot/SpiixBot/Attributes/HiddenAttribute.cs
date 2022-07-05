using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class HiddenAttribute : Attribute
    {
    }
}
