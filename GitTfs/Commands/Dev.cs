using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NDesk.Options;
using Sep.Git.Tfs.Core;
using Sep.Git.Tfs.Util;
using StructureMap;

namespace Sep.Git.Tfs.Commands
{
#if DEBUG
    [Pluggable("dev")]
    [Description("dev\nFor developpers only!!!")]
    [RequiresValidGitRepository]
    public class Dev : GitTfsCommand
    {
        private readonly TextWriter stdout;
        private readonly Globals globals;
        private readonly ConfigProperties properties;
        private readonly Help helper;

        public Dev(Globals globals, ConfigProperties properties, TextWriter stdout, Help helper)
        {
            this.globals = globals;
            this.properties = properties;
            this.stdout = stdout;
            this.helper = helper;
        }

        public virtual OptionSet OptionSet
        {
            get
            {
                return new OptionSet
                {
                };
            }
        }

        public int Run()
        {
            //Put your code to test against an existing git repository here...
            //ie: stdout.WriteLine("Changeset id:" + globals.Repository.GetTfsCommit("003ca02ad").ChangesetId);

            return GitTfsExitCodes.OK;
        }
    }
#endif
}
