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
		private readonly Fetch fetch;
		private readonly Init init;
		private readonly Globals globals;
		private readonly RemoteOptions remoteOptions;

		public InitBranch(RemoteOptions remoteOptions, Globals globals, Fetch fetch, Init init)
		{
			this.fetch = fetch;
			this.init = init;
			this.globals = globals;
			this.remoteOptions = remoteOptions;
		}

		public OptionSet OptionSet
		{
			get { return init.OptionSet.Merge(fetch.OptionSet); }
		}

		public int Run(string argument)
		{
			var defaultRemote = globals.Repository.ReadTfsRemote(GitTfsConstants.DefaultRepositoryId);
			if (defaultRemote == null)
			{
				Trace.WriteLine("No git-tfs repository found. Please try to clone first...\n");
				return -1;
			}
			this.remoteOptions.Username = defaultRemote.TfsUsername;
			this.remoteOptions.Password = defaultRemote.TfsPassword;

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
						Trace.WriteLine("this option works only if git tfs clone was done on the trunk!!! Please clone again from the trunk...");
						return -1;
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
			var gitBranchName = ExtractGitBranchNameFromTfsRepositoryPath(tfsRepositoryPath);

			var rootChangeSetId = defaultRemote.Tfs.GetRootChangesetForBranch(tfsRepositoryPath);
			if (rootChangeSetId == -1)
			{
				Trace.WriteLine("No root changeset found :( \n");
				return -1;
			}
			var sha1RootCommit = globals.Repository.CommandOneline("log", "--grep=\";C" + rootChangeSetId + "[^0-9]\"", "--pretty=format:%H", "--all");
			if (string.IsNullOrWhiteSpace(sha1RootCommit))
			{
				Trace.WriteLine("The root changeset " + rootChangeSetId + " have not be found in the Git repository. The branch containing the changeset should have not been created. Please do it!!\n");
				return -1;
			}
			globals.Repository.CreateTfsRemote(gitBranchName, defaultRemote.TfsUrl, tfsRepositoryPath, remoteOptions);
			var tfsRemote = globals.Repository.ReadTfsRemote(gitBranchName);

			var remoteFileToCreate = globals.Repository.GitDir + "/" + tfsRemote.RemoteRef;
			if (File.Exists(remoteFileToCreate))
			{
				Trace.WriteLine("The file " + remoteFileToCreate + " already exists...\n");
				return -1;
			}
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(remoteFileToCreate));
				File.WriteAllText(remoteFileToCreate, sha1RootCommit + "\n", System.Text.Encoding.ASCII);
			}
			catch (Exception)
			{
				Trace.WriteLine("Error creating refs/remotes directory or file :(");
				return -2;
			}
			//Trace.WriteLine(sha1RootCommit);
			fetch.Run(tfsRemote.Id);
			globals.Repository.CommandOneline("branch", gitBranchName, "tfs/" + gitBranchName);

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

			var gitBranchName = globals.Repository.AssertValidBranchName(gitBranchNameExpected);
			Trace.WriteLine("The name of the local branch will be : " + gitBranchName);
			return gitBranchName;
		}
	}
}
