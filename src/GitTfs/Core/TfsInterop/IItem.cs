using GitTfs.Util;

namespace GitTfs.Core.TfsInterop
{
    public interface IItem
    {
        IVersionControlServer VersionControlServer { get; }
        int ChangesetId { get; }
        string ServerItem { get; }
        int DeletionId { get; }
        TfsItemType ItemType { get; }
        int ItemId { get; }
        long ContentLength { get; }
        bool IsExecutable { get; }
        bool IsSymlink { get; }
        TemporaryFile DownloadFile();
    }

    public interface IItemDownloadStrategy
    {
        TemporaryFile DownloadFile(IItem item);
    }
}
