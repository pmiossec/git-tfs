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
                    new object[] { "git-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C123", true, 123 },
                    new object[] { "git-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C" + uint.MaxValue, true, uint.MaxValue },
                    new object[] { "foo-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C123", false, 0 },
                    new object[] { "\ngit-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C234\n", true, 234 },
                    new object[] { "\r\ngit-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C345\r\n", true, 345 },
                    new object[] { "commit message\n4567\ngit-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C1234\nee\n4567", true, 1234 },
                    new object[] { "\r\ngit-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C888", true, 888 },
                    new object[] { "commit message\r\n4567\r\ngit-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C12345\r\nee\r\n4567", true, 12345 },
                    new object[] { "commit message\r\ngit-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C1\r\ngit-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;C2\r\nee\r\n4567", true, 2 }, //if 2 are possible, choose last (but should never happen!)
                    new object[] { "commit message\r\ngit-tfs-id: [https://tfs.server/tfs/MyRepo]$/gittfs/trunk;\r\n foo;C2\r\nee\r\n4567", false, 0 }, //RegEx must not begin on one line and finish on a second one
                };
            }
        }


        [Theory]
        [PropertyData("Cases")]
        public void Run(string message, bool expectParsed, long expectId)
        {
            long id;
            string tfsPath;
            bool parsed = GitRepository.TryParseChangesetId(message, out id, out tfsPath);
            Assert.Equal(expectParsed, parsed);
            if (parsed)
            {
                Assert.Equal(id, expectId);
                Assert.Equal(tfsPath, "$/gittfs/trunk");
            }
        }
    }
}
