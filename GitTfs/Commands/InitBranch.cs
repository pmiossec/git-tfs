#region Namespace
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NDesk.Options;
using Sep.Git.Tfs.Core;
using StructureMap;
using Sep.Git.Tfs.Util;
#endregion

namespace Sep.Git.Tfs.Commands
{
    [Pluggable("init-branch")]
    [Description("[$/Repository/path|all]\n ex : git tfs init-branch $/Repository/ProjectBranch\n      git tfs init-branch all\n      git tfs init-branch --parent-branch=$/Repository/ProjectParentBranch $/Repository/ProjectBranch")]
    [RequiresValidGitRepository]
    public class InitBranch : GitTfsCommand
    {
        private readonly TextWriter _stdout;
        private readonly Fetch _fetch;
        private readonly Init _init;
        private readonly Globals _globals;
        private readonly RemoteOptions _remoteOptions;

        public string ParentBranch { get; set; }

        public InitBranch(TextWriter stdout, RemoteOptions remoteOptions, Globals globals, Fetch fetch, Init init)
        {
            this._stdout = stdout;
            this._fetch = fetch;
            this._init = init;
            this._globals = globals;
            this._remoteOptions = remoteOptions;
        }

        public OptionSet OptionSet
        {
            get
            {
                return new OptionSet
                {
                    { "p|parent-branch=", "TFS Parent branch of the TFS branch to clone (TFS 2008 only!) ex:$/Repository/ProjectParentBranch", v => ParentBranch = v },
                };
            }
        }

        public int Run(string argument)
        {
            var defaultRemote = _globals.Repository.ReadTfsRemote(GitTfsConstants.DefaultRepositoryId);
            if (defaultRemote == null)
            {
                throw new GitTfsException("error: No git-tfs repository found. Please try to clone first...\n");
            }
            this._remoteOptions.Username = defaultRemote.TfsUsername;
            this._remoteOptions.Password = defaultRemote.TfsPassword;

            var allRemotes = _globals.Repository.ReadAllTfsRemotes();

            if (argument.Trim() != "all")
            {
                argument.AssertValidTfsPath();
                return CreateBranch(defaultRemote, argument, allRemotes, ParentBranch);
            }

            bool first = true;
            foreach (var tfsBranch in defaultRemote.Tfs.GetAllTfsBranchesOrderedByCreation())
            {
                if (first)
                {
                    if (defaultRemote.TfsRepositoryPath.ToLower() != tfsBranch.ToLower())
                    {
                        throw new GitTfsException("error: this option works only if git tfs clone was done on the trunk!!! Please clone again from the trunk...");
                    }

                    first = false;
                    continue;
                }
                var result = CreateBranch(defaultRemote, tfsBranch, allRemotes);
                if (result < 0)
                    return result;
            }
            return 0;
        }

        public int CreateBranch(IGitTfsRemote defaultRemote, string tfsRepositoryPath, IEnumerable<IGitTfsRemote> allRemotes, string tfsRepositoryPathParentBranch = null)
        {
            Trace.WriteLine("=> Working on TFS branch : " + tfsRepositoryPath);

            if (allRemotes.Count(r => r.TfsRepositoryPath.ToLower() == tfsRepositoryPath.ToLower()) != 0)
            {
                Trace.WriteLine("There is already a remote for the tfs repository. Repository ignored!");
                return 1;
            }

            var gitBranchName = ExtractGitBranchNameFromTfsRepositoryPath(tfsRepositoryPath);
            Trace.WriteLine("Git local branch will be :" + gitBranchName);

            int rootChangeSetId;
            if (tfsRepositoryPathParentBranch == null)
                rootChangeSetId = defaultRemote.Tfs.GetRootChangesetForBranch(tfsRepositoryPath);
            else
            {
                Trace.WriteLine("TFS 2008 Compatible mode!");
                var tfsRepositoryPathParentBranchFinded = allRemotes.FirstOrDefault(r => r.TfsRepositoryPath.ToLower() == tfsRepositoryPathParentBranch.ToLower());
                if(tfsRepositoryPathParentBranchFinded == null)
                    throw new GitTfsException("error: The Tfs parent branch '" + tfsRepositoryPathParentBranch + "' can not be found in the Git repository\nPlease init it before...\n");

                rootChangeSetId = defaultRemote.Tfs.GetRootChangesetForBranch(tfsRepositoryPath, tfsRepositoryPathParentBranchFinded.TfsRepositoryPath);
            }

            if (rootChangeSetId == -1)
            {
                throw new GitTfsException("error: No root changeset found :( \n");
            }
            Trace.WriteLine("Found root changeset : " + rootChangeSetId);
            Trace.WriteLine("Try to find changeset in git repository...");
            var sha1RootCommit = _globals.Repository.CommandOneline("log", "--grep=\";C" + rootChangeSetId + "[^0-9]\"", "--pretty=format:%H", "--all");
            if (string.IsNullOrWhiteSpace(sha1RootCommit))
            {
                throw new GitTfsException("error: The root changeset " + rootChangeSetId + " have not be found in the Git repository. The branch containing the changeset should have not been created. Please do it!!\n");
            }
            Trace.WriteLine("Commit found! sha1 : " + sha1RootCommit);
            Trace.WriteLine("Try creating remote...");
            _globals.Repository.CreateTfsRemote(gitBranchName, defaultRemote.TfsUrl, tfsRepositoryPath, _remoteOptions);
            var tfsRemote = _globals.Repository.ReadTfsRemote(gitBranchName);

            var remoteFileToCreate = _globals.Repository.GitDir + "/" + tfsRemote.RemoteRef;
            if (File.Exists(remoteFileToCreate))
            {
                throw new GitTfsException("error: The file " + remoteFileToCreate + " already exists...\n");
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(remoteFileToCreate));
                File.WriteAllText(remoteFileToCreate, sha1RootCommit + "\n", System.Text.Encoding.ASCII);
            }
            catch (Exception)
            {
                throw new GitTfsException("error: Error creating refs/remotes directory or file :(");
            }
            Trace.WriteLine("Remote created!");
            //Trace.WriteLine(sha1RootCommit);
            Trace.WriteLine("Try fetching changesets...");
            _fetch.Run(tfsRemote.Id);
            Trace.WriteLine("Changesets fetched!");
            Trace.WriteLine("Try creating a local branch...");
            _globals.Repository.CommandOneline("branch", gitBranchName, "tfs/" + gitBranchName);
            Trace.WriteLine("Local branch created!");
            return 0;
        }

        private string ExtractGitBranchNameFromTfsRepositoryPath(string tfsRepositoryPath)
        {
            var strings = tfsRepositoryPath.Split('/');
            string gitBranchNameExpected;
            if (string.IsNullOrWhiteSpace(strings[strings.Length - 1]))
                gitBranchNameExpected = strings[strings.Length - 2];
            else
                gitBranchNameExpected = strings[strings.Length - 1];

            var gitBranchName = _globals.Repository.AssertValidBranchName(gitBranchNameExpected);
            _stdout.WriteLine("The name of the local branch will be : " + gitBranchName);
            return gitBranchName;
        }
    }
}
