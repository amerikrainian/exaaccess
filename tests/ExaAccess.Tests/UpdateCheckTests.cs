using ExaAccess.Update;
using Xunit;

namespace ExaAccess.Tests
{
    /// <summary>
    /// The version rules behind the launch update announcement: what the release payload names, and
    /// that only a strictly newer release ever counts. Ported from Guildrun Access with the suite.
    /// </summary>
    public class UpdateCheckTests
    {
        [Fact]
        public void LatestVersion_reads_the_tag_and_strips_the_v()
        {
            string json = "{\"url\":\"x\",\"tag_name\":\"v0.2.2\",\"name\":\"ExaAccess v0.2.2\"}";
            Assert.Equal("0.2.2", UpdateCheck.LatestVersion(json));
        }

        [Fact]
        public void LatestVersion_keeps_an_unprefixed_tag()
        {
            Assert.Equal("1.4", UpdateCheck.LatestVersion("{\"tag_name\": \"1.4\"}"));
        }

        [Fact]
        public void LatestVersion_is_null_without_a_tag()
        {
            Assert.Null(UpdateCheck.LatestVersion("{\"message\":\"Not Found\"}"));
            Assert.Null(UpdateCheck.LatestVersion("{\"tag_name\":\"v\"}"));
            Assert.Null(UpdateCheck.LatestVersion(null));
        }

        [Theory]
        [InlineData("0.2.2", "0.2.1", true)]
        [InlineData("0.3", "0.2.9", true)]
        [InlineData("1.0.0", "0.9.9", true)]
        [InlineData("0.2.1.1", "0.2.1", true)]
        [InlineData("0.2.1", "0.2.1", false)]
        [InlineData("1.0", "1", false)]
        [InlineData("0.2.0", "0.2.1", false)]
        [InlineData("0.2.1", "0.3.0", false)]
        public void IsNewer_compares_numeric_components(string remote, string local, bool newer)
        {
            Assert.Equal(newer, UpdateCheck.IsNewer(remote, local));
        }

        // A suffixed component parses as zero, so a pre-release tag compares conservatively (never
        // announced over a clean local build of the same line) instead of throwing.
        [Fact]
        public void IsNewer_counts_non_numeric_components_as_zero()
        {
            Assert.False(UpdateCheck.IsNewer("0.2.1-beta", "0.2.1"));
            Assert.False(UpdateCheck.IsNewer("0.2.2-beta", "0.2.1"));
            Assert.True(UpdateCheck.IsNewer("0.3.0-beta", "0.2.1"));
        }

        // ExaAccess addition: the SDK stamps "+{commit}" onto InformationalVersion; left in, the
        // last component would parse as zero and an OLDER release would announce as an update.
        [Theory]
        [InlineData("0.1.3+4f2a9c1", "0.1.3")]
        [InlineData("0.1.3", "0.1.3")]
        [InlineData(" 1.0.0+abc ", "1.0.0")]
        public void CleanLocal_cuts_build_metadata(string informational, string expected)
        {
            Assert.Equal(expected, UpdateCheck.CleanLocal(informational));
        }

        [Fact]
        public void CleanLocal_is_null_for_nothing_usable()
        {
            Assert.Null(UpdateCheck.CleanLocal(null));
            Assert.Null(UpdateCheck.CleanLocal("  "));
            Assert.Null(UpdateCheck.CleanLocal("+abc"));
        }

        [Fact]
        public void A_cleaned_local_build_is_not_outranked_by_its_own_release()
        {
            Assert.False(UpdateCheck.IsNewer("0.1.2", UpdateCheck.CleanLocal("0.1.3+4f2a9c1")));
            Assert.False(UpdateCheck.IsNewer("0.1.3", UpdateCheck.CleanLocal("0.1.3+4f2a9c1")));
        }
    }
}
