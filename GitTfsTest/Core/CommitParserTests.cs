using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sep.Git.Tfs.Core;
using Xunit;
using Xunit.Extensions;

namespace Sep.Git.Tfs.Test.Core
{
    public class CommitParserTests
    {
        public static IEnumerable<object[]> Cases
        {
            get
            {
                return new[] {
                    new object[] { "git-tfs-id: [http://tfs.com/tfs]$/foo;C123", true, 123, "$/foo" },
                    new object[] { "git-tfs-id: [http://tfs.com/tfs]handle more than Int32;C" + uint.MaxValue, true, uint.MaxValue, "handle more than Int32" },
                    new object[] { "foo-tfs-id: [http://tfs.com/tfs]bar;C123", false, 0, null },
                    new object[] { "\ngit-tfs-id: [http://tfs.com/tfs]foo;C234\n", true, 234, "foo" },
                    new object[] { "\r\ngit-tfs-id: [http://tfs.com/tfs]foo;C345\r\n", true, 345, "foo" },
                    new object[] { "commit message\n4567\ngit-tfs-id: [http://tfs.com/tfs]foo;C1234\nee\n4567", true, 1234, "foo" },
                    new object[] { "\r\ngit-tfs-id: [http://tfs.com/tfs]foo;C888", true, 888, "foo" },
                    new object[] { "commit message\r\n4567\r\ngit-tfs-id: [http://tfs.com/tfs]foo;C12345\r\nee\r\n4567", true, 12345, "foo" },
                    new object[] { "commit message\r\ngit-tfs-id: [http://tfs.com/tfs]foo;C1\r\ngit-tfs-id: [http://tfs.com/tfs]foo;C2\r\nee\r\n4567", true, 2, "foo" }, //if 2 are possible, choose last (but should never happen!)
                    new object[] { "commit message\r\ngit-tfs-id: [http://tfs.com/tfs]foo;\r\n [http://tfs.com/tfs]foo;C2\r\nee\r\n4567", false, 0, null }, //RegEx must not begin on one line and finish on a second one
                };
            }
        }


        [Theory]
        [PropertyData("Cases")]
        public void Run(string message, bool expectParsed, long expectId, string tfsPath)
        {
            long id;
            string path;
            string server;
            bool parsed = GitCommit.TryParseChangesetId(message, out id, out path, out server);
            Assert.Equal(expectParsed, parsed);
            if (parsed)
            {
                Assert.Equal(id, expectId);
            }
        }
    }
}
