using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GitTfs.Core;
using Mode = LibGit2Sharp.Mode;

namespace GitTfs.Util
{
    public class PathResolver
    {
        private readonly IGitTfsRemote _remote;
        private readonly string _relativePath;
        private readonly IDictionary<string, GitObject> _initialTree;

        public PathResolver(IGitTfsRemote remote, string relativePath, IDictionary<string, GitObject> initialTree)
        {
            _remote = remote;
            _relativePath = relativePath;
            _initialTree = initialTree;
        }

        public string GetPathInGitRepo(string tfsPath)
        {
            return GetGitObject(tfsPath, Mode.NonExecutableFile).Try(x => x.Path);
        }

        public GitObject GetGitObject(string tfsPath, Mode mode)
        {
            var pathInGitRepo = _remote.GetPathInGitRepo(tfsPath);
            if (pathInGitRepo == null)
                return null;
            if (!string.IsNullOrEmpty(_relativePath))
                pathInGitRepo = _relativePath + "/" + pathInGitRepo;
            return Lookup(pathInGitRepo, mode);
        }

        public bool IsIgnored(string path)
        {
            return _remote.IsIgnored(path);
        }

        public bool IsInDotGit(string path)
        {
            return _remote.IsInDotGit(path);
        }

        public bool Contains(string pathInGitRepo)
        {
            if (pathInGitRepo != null)
            {
                GitObject result;
                if (_initialTree.TryGetValue(pathInGitRepo, out result))
                    return result.Commit != null;
            }
            return false;
        }

        private static readonly Regex SplitDirnameFilename = new Regex(@"(?<dir>.*)[/\\](?<file>[^/\\]+)", RegexOptions.Compiled);

        private GitObject Lookup(string pathInGitRepo, Mode mode)
        {
            GitObject result;
            if (_initialTree.TryGetValue(pathInGitRepo, out result))
                return result;

            var fullPath = pathInGitRepo;
            var splitResult = SplitDirnameFilename.Match(pathInGitRepo);
            if (splitResult.Success)
            {
                var dirName = splitResult.Groups["dir"].Value;
                var fileName = splitResult.Groups["file"].Value;
                fullPath = Lookup(dirName, mode).Path + "/" + fileName;
            }
            result = new GitObject { Path = fullPath, Mode = mode};
            _initialTree[fullPath] = result;
            return result;
        }
    }
}
