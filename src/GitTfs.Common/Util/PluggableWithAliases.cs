using StructureMap;
using StructureMap.Pipeline;

namespace GitTfs.Util
{
    public class PluggableWithAliases : PluggableAttribute
    {
        public readonly string[] Aliases;

        public PluggableWithAliases(string concreteKey, params string[] aliases)
            : base(concreteKey)
        {
            Aliases = aliases;
        }
    }

    public class PluggableAttribute : StructureMapAttribute
    {
        public string ConcreteKey { get; set; }

        public PluggableAttribute(string concreteKey)
        {
            ConcreteKey = concreteKey;
        }

        public override void Alter(IConfiguredInstance instance)
        {
            instance.Name = ConcreteKey;
        }
    }
}
