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
	[Description("[$/Repository/path|all]\n ex : git tfs init-branch $/Repository/ProjectBranch\n      git tfs init-branch all\n")]
	[RequiresValidGitRepository]
	public class InitBranch : GitTfsCommand
	{
		private readonly TextWriter _stdout;
		private readonly Fetch _fetch;
		private readonly Init _init;
		private readonly Globals _globals;
		private readonly RemoteOptions _remoteOptions;

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
			get { return _init.OptionSet.Merge(_fetch.OptionSet); }
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

			if (argument.Trim() != "all")
			{
				argument.AssertValidTfsPath();
				return CreateBranch(defaultRemote, argument);
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
				var result = CreateBranch(defaultRemote, tfsBranch);
				if (result != 0)
					return result;
			}
			return 0;
		}

		public int CreateBranch(IGitTfsRemote defaultRemote, string tfsRepositoryPath)
		{
			Trace.WriteLine("=> Working on TFS branch : " + tfsRepositoryPath);

			var gitBranchName = ExtractGitBranchNameFromTfsRepositoryPath(tfsRepositoryPath);
			Trace.WriteLine("Git local branche will be :" + gitBranchName);

			var rootChangeSetId = defaultRemote.Tfs.GetRootChangesetForBranch(tfsRepositoryPath);
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
