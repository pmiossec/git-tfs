using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace Sep.Git.Tfs.Core
{
    public class GitCommit
    {
        private readonly Commit _commit;

        public GitCommit(Commit commit)
        {
            _commit = commit;
        }

        public IEnumerable<GitTreeEntry> GetTree()
        {
            var treesToDescend = new Queue<Tree>(new[] {_commit.Tree});
            while(treesToDescend.Any())
            {
                var currentTree = treesToDescend.Dequeue();
                foreach(var entry in currentTree)
                {
                    if (entry.TargetType == TreeEntryTargetType.Tree)
                    {
                        treesToDescend.Enqueue((Tree)entry.Target);
                    }
                    else if (entry.TargetType == TreeEntryTargetType.Blob)
                    {
                        yield return new GitTreeEntry(entry);
                    }
                    else
                    {
                        Trace.WriteLine("Not including " + entry.Name + ": type is " + entry.GetType().Name);
                    }
                }
            }
        }

        public Tuple<string,string> AuthorAndEmail
        {
            get
            {
                return new Tuple<string,string>(_commit.Author.Name, _commit.Author.Email);
            }
        }

        public DateTimeOffset When
        {
            get
            {
                return _commit.Author.When;
            }
        }

        public string Sha
        {
            get
            {
                return _commit.Sha;
            }
        }

        public string Message
        {
            get
            {
                return _commit.Message;
            }
        }

        public IEnumerable<GitCommit> Parents
        {
            get { return _commit.Parents.Select(c => new GitCommit(c)); }
        }

        private bool? _isTfsChangeset;
        public bool IsTfsChangeset
        {
            get
            {
                if (_isTfsChangeset.HasValue)
                    return _isTfsChangeset.Value;
                TryParseChangesetId();
                return _isTfsChangeset.Value;
            }
        }

        private long _changesetId;
        public long ChangesetId
        {
            get
            {
                if (_isTfsChangeset.HasValue)
                    return _changesetId;
                TryParseChangesetId();
                return _changesetId;
            }
        }

        private string _tfsPath;
        public string TfsPath
        {
            get
            {
                if (_isTfsChangeset.HasValue)
                    return _tfsPath;
                TryParseChangesetId();
                return _tfsPath;
            }
        }

        private string _tfsServer;
        public string TfsServer
        {
            get
            {
                if (_isTfsChangeset.HasValue)
                    return _tfsServer;
                TryParseChangesetId();
                return _tfsServer;
            }
        }

        private static readonly Regex tfsIdRegex = new Regex(@"^git-tfs-id: \[(.*)\](.*);C([0-9]+)\r?$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.RightToLeft);

        public static bool TryParseChangesetId(string message, out long changesetId, out string tfsPath, out string tfsServer)
        {
            var match = tfsIdRegex.Match(message);
            if (match.Success)
            {
                tfsServer = match.Groups[1].Value;
                tfsPath = match.Groups[2].Value;
                changesetId = long.Parse(match.Groups[3].Value);
                return true;
            }

            tfsServer = null;
            tfsPath = null;
            changesetId = 0;
            return false;
        }

        private void TryParseChangesetId()
        {
            _isTfsChangeset = TryParseChangesetId(Message, out _changesetId, out _tfsPath, out _tfsPath);
        }
    }
}

