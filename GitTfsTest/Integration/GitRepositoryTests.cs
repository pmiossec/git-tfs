﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Sep.Git.Tfs.Core;
using Sep.Git.Tfs.Core.TfsInterop;
using StructureMap;
using Xunit;
using LibGit2Sharp;

namespace Sep.Git.Tfs.Test.Integration
{
    public class GitRepositoryTests : IDisposable
    {
        IntegrationHelper h = new IntegrationHelper();

        public GitRepositoryTests()
        {
            h.SetupFake(_ => { });
        }

        public void Dispose()
        {
            h.Dispose();
        }

        [Fact]
        public void GetLastParentTfsCommits_WhenThereIsMoreThanTfsChangeset_ThenReturnTheLast()
        {
            h.SetupFake(r =>
            {
                r.Changeset(42, "UseLess! Just to have the same changeset Id that the commit already in repo (and fetch nothing)", DateTime.Parse("2012-01-01 12:12:12 -05:00"))
                 .Change(TfsChangeType.Add, TfsItemType.Folder, "$/MyProject");
            });

            string c1 = null;
            string c2 = null;
            string c3 = null;
            h.SetupGitRepo("repo", g =>
            {
                c1 = g.Commit("A sample commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/trunk;C1");
                c2 = g.Commit("A sample commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/trunk;C2");
                c3 = g.Commit("A sample commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/trunk;C3");
            });

            using (var repo = h.Repository("repo"))
            {
                var gitRepository = new GitRepository(new StringWriter(), repo.Info.WorkingDirectory, new Container(), null, new RemoteConfigConverter());
                var changesets = gitRepository.GetLastParentTfsCommits("HEAD");
                Assert.Equal(1, changesets.Count());
                Assert.Equal(c3, changesets.First().GitCommit);
            }
        }

        [Fact]
        public void GetLastParentTfsCommits_WhenTheMergeCommitIsNotFromTfs_ThenReturnTheParentsFoundWithMainParentFromMasterFirst()
        {
            int ChangesetIdToTrickFetch = 1;
            h.SetupFake(r =>
            {
                r.Changeset(ChangesetIdToTrickFetch, "UseLess! Just to have the same changeset Id that the commit already in repo (and fetch nothing)", DateTime.Parse("2012-01-01 12:12:12 -05:00"))
                 .Change(TfsChangeType.Add, TfsItemType.Folder, "$/MyProject");
            });

            string c1 = null;
            string c2 = null;
            string c3 = null;
            h.SetupGitRepo("repo", g =>
            {
                c1 = g.Commit("A sample commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/trunk;C"+ ChangesetIdToTrickFetch);
                g.CreateBranch("branch");
                c2 = g.Commit("A sample commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/branch;C" + ChangesetIdToTrickFetch);
                g.Checkout("master");
                c3 = g.Commit("A merge commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/trunk;C" + ChangesetIdToTrickFetch);
                g.Merge("branch");
            });

            using (var repo = h.Repository("repo"))
            {
                var gitRepository = new GitRepository(new StringWriter(), repo.Info.WorkingDirectory, new Container(), null, new RemoteConfigConverter());
                var changesets = gitRepository.GetLastParentTfsCommits("HEAD");
                Assert.Equal(2, changesets.Count());
                //C3 must be returned first because that's the parent commit of the master branch where the other branch is merged
                Assert.Equal(c3, changesets.First().GitCommit);
                Assert.Equal(c2, changesets.ElementAt(1).GitCommit);
            }
        }

        [Fact]
        public void GetLastParentTfsCommits_WhenTheMergeCommitIsFromTfs_ThenReturnThisCommit()
        {
            int ChangesetIdToTrickFetch = 1;
            h.SetupFake(r =>
            {
                r.Changeset(1, "UseLess! Just to have the same changeset Id that the commit already in repo (and fetch nothing)", DateTime.Parse("2012-01-01 12:12:12 -05:00"))
                 .Change(TfsChangeType.Add, TfsItemType.Folder, "$/MyProject");
            });

            string c1 = null;
            string c2 = null;
            string c3 = null;
            string c4 = null;
            h.SetupGitRepo("repo", g =>
            {
                c1 = g.Commit("A sample commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/trunk;C" + ChangesetIdToTrickFetch);
                g.CreateBranch("branch");
                c2 = g.Commit("A sample commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/branch;C" + ChangesetIdToTrickFetch);
                g.Checkout("master");
                c3 = g.Commit("A sample commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/trunk;C" + ChangesetIdToTrickFetch);
                g.Merge("branch");
                //Trick to create a merge commit similar to one fetched from TFS
                c4 = g.Amend("A merge commit from TFS.\n\ngit-tfs-id: [http://server/tfs]$/MyProject/trunk;C" + ChangesetIdToTrickFetch);
            });

            using (var repo = h.Repository("repo"))
            {
                var gitRepository = new GitRepository(new StringWriter(), repo.Info.WorkingDirectory, new Container(), null, new RemoteConfigConverter());
                var changesets = gitRepository.GetLastParentTfsCommits("HEAD");
                Assert.Equal(1, changesets.Count());
                Assert.Equal(c4, changesets.First().GitCommit);
            }
        }

        [Fact]
        public void GetLastParentTfsCommits_WhenNoCommitFromTfs_ThenReturnNothing()
        {
            h.SetupFake(r =>
            {
                r.Changeset(1, "UseLess! Just to have the same changeset Id that the commit already in repo (and fetch nothing)", DateTime.Parse("2012-01-01 12:12:12 -05:00"))
                 .Change(TfsChangeType.Add, TfsItemType.Folder, "$/MyProject");
            });

            h.SetupGitRepo("repo", g =>
            {
                g.Commit("1.A sample commit from TFS.");
                g.Commit("2.A sample commit from TFS.");
                g.Commit("3.A sample commit from TFS.");
            });

            using (var repo = h.Repository("repo"))
            {
                var gitRepository = new GitRepository(new StringWriter(), repo.Info.WorkingDirectory, new Container(), null, new RemoteConfigConverter());
                var changesets = gitRepository.GetLastParentTfsCommits("HEAD");
                Assert.Equal(0, changesets.Count());
            }
        }

        [Fact]
        public void FindParentCommits()
        {
            //History of changesets:
            //6
            //|\
            //| 5
            //| |
            //3 4
            //| /
            //2
            //|
            //1

            string c1 = null;
            string c2 = null;
            string c3 = null;
            string c4 = null;
            string c5 = null;
            string c6 = null;
            h.SetupGitRepo("repo", g =>
            {
                c1 = g.Commit("C1-Common");
                c2 = g.Commit("C2-Common");
                g.CreateBranch("branch");
                c4 = g.Commit("C4-branch");
                c5 = g.Commit("C5-branch");
                g.Checkout("master");
                c3 = g.Commit("C3-master");
                g.Merge("branch");
                //Trick to create a merge commit similar to one fetched from TFS
                c6 = g.Amend("C6-master (merge branch into)");
            });


            using (var repo = h.Repository("repo"))
            {
                var gitRepository = new GitRepository(new StringWriter(), repo.Info.WorkingDirectory, new Container(), null, new RemoteConfigConverter());
                //string revList = gitRepository.CommandOneline("rev-list", "--parents", "--ancestry-path", "--first-parent", "--reverse", c1 + ".." + c4);

                var changesets = gitRepository.FindParentCommits(c5, c1);
                Assert.Equal(3, changesets.Count());
                Assert.Equal(c2, changesets.ElementAt(0).Sha);
                Assert.Equal(c4, changesets.ElementAt(1).Sha);
                Assert.Equal(c5, changesets.ElementAt(2).Sha);

                changesets = gitRepository.FindParentCommits(c6, c1);
                Assert.Equal(3, changesets.Count());
                Assert.Equal(c2, changesets.ElementAt(0).Sha);
                Assert.Equal(c3, changesets.ElementAt(1).Sha);
                Assert.Equal(c6, changesets.ElementAt(2).Sha);

                changesets = gitRepository.FindParentCommits(c5, c3);
                Assert.Equal(0, changesets.Count());
            }
        }

        //[Fact]
        //public void FindChangesetWithTheSameIdOn2branches()
        //{
        //    //History of changesets:
        //    //6
        //    //|\
        //    //| 5
        //    //| |
        //    //3 3bis
        //    //| /
        //    //2
        //    //|
        //    //1

        //    string c1 = null;
        //    string c2 = null;
        //    string c3 = null;
        //    string c3bis = null;
        //    string c4 = null;
        //    string c5 = null;
        //    h.SetupGitRepo("repo", g =>
        //    {
        //        c1 = g.Commit("C1-Common\n\ngit-tfs-id: [https://tfs.server/tfs]$/trunk;C1");
        //        c2 = g.Commit("C2-Common\n\ngit-tfs-id: [https://tfs.server/tfs]$/trunk;C2");
        //        g.CreateBranch("branch");
        //        c3bis = g.Commit("C3bis-branch\n\ngit-tfs-id: [https://tfs.server/tfs]$/branch;C3");
        //        g.Checkout("master");
        //        c3 = g.Commit("C3-master\n\ngit-tfs-id: [https://tfs.server/tfs]$/trunk;C3");
        //        g.Checkout("branch");
        //        c4 = g.Commit("C4-branch\n\ngit-tfs-id: [https://tfs.server/tfs]$/branch;C4");
        //        g.Checkout("master");
        //        g.Merge("branch");
        //        //Trick to create a merge commit similar to one fetched from TFS
        //        c5 = g.Amend("C5-master (merge branch into)\n\ngit-tfs-id: [https://tfs.server/tfs]$/trunk;C5");
        //    });


        //    using (var repo = h.Repository("repo"))
        //    {
        //        var gitRepository = new GitRepository(new StringWriter(), repo.Info.WorkingDirectory, new Container(), null, new RemoteConfigConverter());

        //        //var shas = gitRepository.FindCommitsByChangesetId(3, null, null).Select(c => c.Sha);
        //        //Assert.Contains(c3, shas);
        //        //Assert.Contains(c3bis, shas);
        //        var commit3InBranch = gitRepository.FindCommitsByChangesetId(3, "$/branch", null).First();
        //        Assert.Equal(c3bis, commit3InBranch.Sha);
        //        var commit3InTrunk = gitRepository.FindCommitsByChangesetId(3, "$/trunk", null).First();
        //        Assert.Equal(c3, commit3InTrunk.Sha);
        //        var shas = gitRepository.FindCommitsByChangesetId(3, null, null).Select(c => c.Sha);
        //        Assert.Contains(c3, shas);
        //        Assert.Contains(c3bis, shas);
        //    }
        //}

    }
}
