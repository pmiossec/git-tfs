using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.Win32;
using Sep.Git.Tfs.Core;
using Sep.Git.Tfs.Core.TfsInterop;
using Sep.Git.Tfs.Util;
using Sep.Git.Tfs.VsCommon;
using StructureMap;

namespace Sep.Git.Tfs.Vs2010
{
    using System.Net;

    using Microsoft.TeamFoundation.Framework.Client;

    public class TfsHelper : TfsHelperBase
    {
        private readonly TfsApiBridge _bridge;
        private TfsTeamProjectCollection _server;

        public TfsHelper(TextWriter stdout, TfsApiBridge bridge, IContainer container) : base(stdout, bridge, container)
        {
            _bridge = bridge;
        }

        public override string TfsClientLibraryVersion
        {
            get { return typeof(TfsTeamProjectCollection).Assembly.GetName().Version + " (MS)"; }
        }

        public override void EnsureAuthenticated()
        {
            if (string.IsNullOrEmpty(Url))
            {
                _server = null;
            }
            else
            {
                Uri uri;
                if (!Uri.IsWellFormedUriString(Url, UriKind.Absolute))
                {
                    // maybe it is not an Uri but instance name
                    var servers = RegisteredTfsConnections.GetConfigurationServers();
                    var registered = servers.FirstOrDefault(s => String.Compare(s.Name, Url, StringComparison.OrdinalIgnoreCase) == 0);
                    if (registered == null)
                        throw new GitTfsException("Given tfs name is not correct URI and not found as a registered TFS instance");
                    uri = registered.Uri;
                }
                else
                {
                    uri = new Uri(Url);
                }

                _server = HasCredentials ?
                    new TfsTeamProjectCollection(uri, GetCredential(), new UICredentialsProvider()) :
                    new TfsTeamProjectCollection(uri, new UICredentialsProvider());

                _server.EnsureAuthenticated();
            }
        }

        protected override T GetService<T>()
        {
            return (T) _server.GetService(typeof (T));
        }

        protected override string GetAuthenticatedUser()
        {
            return VersionControl.AuthorizedUser;
        }

        public override bool CanShowCheckinDialog { get { return true; } }

        public override long ShowCheckinDialog(IWorkspace workspace, IPendingChange[] pendingChanges, IEnumerable<IWorkItemCheckedInfo> checkedInfos, string checkinComment)
        {
            return ShowCheckinDialog(_bridge.Unwrap<Workspace>(workspace),
                                     pendingChanges.Select(p => _bridge.Unwrap<PendingChange>(p)).ToArray(),
                                     checkedInfos.Select(c => _bridge.Unwrap<WorkItemCheckedInfo>(c)).ToArray(),
                                     checkinComment);
        }

        private long ShowCheckinDialog(Workspace workspace, PendingChange[] pendingChanges, 
            WorkItemCheckedInfo[] checkedInfos, string checkinComment)
        {
            using (var parentForm = new ParentForm())
            {
                parentForm.Show();

                var dialog = Activator.CreateInstance(GetCheckinDialogType(), new object[] {workspace.VersionControlServer});

                return dialog.Call<int>("Show", parentForm.Handle, workspace, pendingChanges, pendingChanges,
                                        checkinComment, null, null, checkedInfos);
            }
        }

        private const string DialogAssemblyName = "Microsoft.TeamFoundation.VersionControl.ControlAdapter";

        private static Type GetCheckinDialogType()
        {
            return GetDialogAssembly().GetType(DialogAssemblyName + ".CheckinDialog");
        }

        private static Assembly GetDialogAssembly()
        {
            return Assembly.LoadFrom(GetDialogAssemblyPath());
        }

        private static string GetDialogAssemblyPath()
        {
            return Path.Combine(GetVs2010InstallDir(), "PrivateAssemblies", DialogAssemblyName + ".dll");
        }

        private static string GetVs2010InstallDir()
        {
            return TryGetRegString(@"Software\Microsoft\VisualStudio\10.0", "InstallDir")
                ?? TryGetRegString(@"Software\WOW6432Node\Microsoft\VisualStudio\10.0", "InstallDir");
        }

        private static string TryGetRegString(string path, string name)
        {
            try
            {
                Trace.WriteLine("Trying to get " + path + "|" + name);
                var key = Registry.LocalMachine.OpenSubKey(path);
                if(key != null)
                {
                    return key.GetValue(name) as string;
                }
            }
            catch(Exception e)
            {
                Trace.WriteLine("Unable to get registry value " + path + "|" + name + ": " + e);
            }
            return null;
        }

        public override IEnumerable<string> GetAllTfsBranchesOrderedByCreation()
        {
            return VersionControl.QueryRootBranchObjects(RecursionType.Full).Select(b => b.Properties.RootItem.Item);
        }

        public override int GetRootChangesetForBranch(string tfsPathBranchToCreate)
        {
            Trace.WriteLine("Looking for all branches...");
            var allTfsBranches = VersionControl.QueryRootBranchObjects(RecursionType.Full);
            var tfsBranchToCreate = allTfsBranches.FirstOrDefault(b => b.Properties.RootItem.Item.ToLower() == tfsPathBranchToCreate.ToLower());
            if (tfsBranchToCreate == null)
                return -1;
            string pathFirstBranch = tfsBranchToCreate.Properties.ParentBranch.Item;
            Trace.WriteLine("Found parent branch : " + pathFirstBranch);
            var changesetIdsFirstChangesetInMainBranch = VersionControl.GetMergeCandidates(pathFirstBranch, tfsPathBranchToCreate, RecursionType.Full).Select(c => c.Changeset.ChangesetId).FirstOrDefault();

            if (changesetIdsFirstChangesetInMainBranch == 0)
            {
                Trace.WriteLine("No changeset in main branch since branch done... (need only to find the last changeset in the main branch)");
                return VersionControl.QueryHistory(pathFirstBranch, VersionSpec.Latest, 0,
                        RecursionType.Full, null, tfsBranchToCreate.Properties.ParentBranch.Version, VersionSpec.Latest,
                        1, false, false).Cast<Changeset>().First().ChangesetId;
            }

            Trace.WriteLine("First changeset in the main branch after branching : " + changesetIdsFirstChangesetInMainBranch);

            Trace.WriteLine("Try to find the previous changeset...");
            int step = 5;
            int upperBound = changesetIdsFirstChangesetInMainBranch - 1;
            int lowerBound = Math.Max(upperBound - step, 1);
            //for optimization, retrieve the lesser possible changesets... so 5 by 5
            while (true)
            {
                Trace.WriteLine("Looking for the changeset between changeset id " + lowerBound + " and " + upperBound);
                var firstBranchChangesetIds = VersionControl.QueryHistory(pathFirstBranch, VersionSpec.Latest, 0, RecursionType.Full,
                                null, new ChangesetVersionSpec(lowerBound), new ChangesetVersionSpec(upperBound), int.MaxValue, true,
                                false, false).Cast<Changeset>().Select(c => c.ChangesetId).ToList();
                if (firstBranchChangesetIds.Count != 0)
                    return firstBranchChangesetIds.First(cId => cId < changesetIdsFirstChangesetInMainBranch);
                else
                {
                    if (upperBound == 1)
                    {
                        throw new GitTfsException("There is a bug in git-tfs init-branch :(");
                    }
                    upperBound = Math.Max(upperBound - step, 1);
                    lowerBound = Math.Max(upperBound - step, 1);
                }
            }
        }
    }

    public class ItemDownloadStrategy : IItemDownloadStrategy
    {
        private readonly TfsApiBridge _bridge;

        public ItemDownloadStrategy(TfsApiBridge bridge)
        {
            _bridge = bridge;
        }

        public TemporaryFile DownloadFile(IItem item)
        {
            var temp = new TemporaryFile();
            try
            {
                _bridge.Unwrap<Item>(item).DownloadFile(temp);
                return temp;
            }
            catch (Exception e)
            {
                Trace.WriteLine(String.Format("Something went wrong downloading \"{0}\" in changeset {1}", item.ServerItem, item.ChangesetId));
                temp.Dispose();
                throw;
            }
        }
    }
}
