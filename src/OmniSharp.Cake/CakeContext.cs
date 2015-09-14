using System.Collections.Generic;
using System.Composition;

namespace OmniSharp.Cake
{
    [Export, Shared]
    public class CakeContext
    {
        public HashSet<string> CakeFiles { get; } = new HashSet<string>();
        public HashSet<string> References { get; } = new HashSet<string>();
        public HashSet<string> Usings { get; } = new HashSet<string>();

        public string Path { get; set; }
    }
}
