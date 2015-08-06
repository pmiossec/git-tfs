using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using NDesk.Options;
using Sep.Git.Tfs.Core;
using StructureMap;

namespace Sep.Git.Tfs.Commands
{
    [Pluggable("config")]
    [RequiresValidGitRepository]
    [Description("config option=value")]
    public class Config : GitTfsCommand
    {
        private readonly Globals _globals;
        private readonly TextWriter _stdout;
        private readonly Bootstrapper _bootstrapper;

        public Config(Globals globals, TextWriter stdout, Bootstrapper bootstrapper)
        {
            _globals = globals;
            _stdout = stdout;
            _bootstrapper = bootstrapper;
        }

        List<string> configs = new List<string>
        {
            GitTfsConstants.BatchSize,
            GitTfsConstants.IgnoreBranches,
            GitTfsConstants.ExportMetadatasConfigKey,
            GitTfsConstants.WorkspaceConfigKey,
            GitTfsConstants.WorkItemAssociateRegexConfigKey,
            GitTfsConstants.IgnoreNotInitBranches,
            GitTfsConstants.InitialChangeset
        };

        public OptionSet OptionSet
        {
            get
            {
                var optionSet = new OptionSet();
                foreach (var config in configs)
                {
                    optionSet.Add(config, config, v => Test = v);
                }
                //foreach (var VARIABLE in GitTfsConstants.IgnoreBranches)
                //{
                    
                //}
                return optionSet;
            }
        }

        public string Test { get; set; }

        public int Run()
        {
            foreach (var config in configs)
            {
                var configValue = _globals.Repository.GetConfig(config);
                _stdout.WriteLine(config + ":" + (string.IsNullOrEmpty(configValue) ? "<Not Set>" : configValue));
            }
            return GitTfsExitCodes.OK;
        }

        //public int Run(string config)
        //{
        //    var tfsParents = _globals.Repository.GetLastParentTfsCommits(commitish);
        //    foreach (var parent in tfsParents)
        //    {
        //        GitCommit commit = _globals.Repository.GetCommit(parent.GitCommit);
        //        _stdout.WriteLine("commit {0}\nAuthor: {1} <{2}>\nDate:   {3}\n\n    {4}",
        //            commit.Sha,
        //            commit.AuthorAndEmail.Item1, commit.AuthorAndEmail.Item2,
        //            commit.When.ToString("ddd MMM d HH:mm:ss zzz"),
        //            commit.Message.Replace("\n","\n    ").TrimEnd(' '));
        //        _bootstrapper.CreateRemote(parent);
        //        _stdout.WriteLine();
        //    }
        //    return GitTfsExitCodes.OK;
        //}
    }
}
