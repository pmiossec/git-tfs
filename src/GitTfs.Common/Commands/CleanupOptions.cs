using NDesk.Options;
using GitTfs.Util;
using StructureMap;

namespace GitTfs.Commands
{
    [Singleton]
    public class CleanupOptions
    {
        private readonly Globals _globals;

        public CleanupOptions(Globals globals)
        {
            _globals = globals;
        }

        public OptionSet OptionSet
        {
            get
            {
                return new OptionSet
                {
                    { "v|verbose", v => IsVerbose = v != null },
                };
            }
        }

        private bool IsVerbose { get; set; }

        public void Init()
        {
            if (IsVerbose)
                _globals.DebugOutput = true;
        }
    }
}
