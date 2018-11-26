using NDesk.Options;

namespace GitTfs
{
    //[PluginFamily]
    public interface GitTfsCommand
    {
        OptionSet OptionSet { get; }
    }
}
