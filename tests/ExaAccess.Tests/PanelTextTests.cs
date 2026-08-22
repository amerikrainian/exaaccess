using System.Collections.Generic;
using ExaAccess.UI;
using Xunit;

namespace ExaAccess.Tests
{
    /// <summary>
    /// The panel-text assembler over captured draw calls. Geometry mirrors the game's special-puzzle
    /// panels: a title line on top, a header row, then table rows stepping 41.5 down (y-up
    /// coordinates), columns at fixed x offsets.
    /// </summary>
    public class PanelTextTests
    {
        [Fact]
        public void TwoColumnTableReadsTopDownRowMajor()
        {
            // The HDI-10 I/O LOG shape, deliberately shuffled — assembly must not depend on draw order.
            var cells = new List<PanelCell>
            {
                new PanelCell("-73", 3329f, 2031f),
                new PanelCell("IN (CNS)", 3329f, 2081f),
                new PanelCell("HDI-10 I/O LOG", 3227f, 2155f),
                new PanelCell("50", 3535f, 1989.5f),
                new PanelCell("OUT (ARM)", 3535f, 2081f),
                new PanelCell("-73", 3535f, 2031f),
                new PanelCell("477", 3329f, 1989.5f),
            };
            Assert.Equal(new[]
            {
                "HDI-10 I/O LOG",
                "IN (CNS), OUT (ARM)",
                "-73, -73",
                "477, 50",
            }, PanelText.Assemble(cells));
        }

        [Fact]
        public void MultiLineStringBecomesOneRowPerLine()
        {
            // The UPLINK STATUS panel draws its whole readout as a single '\n'-joined string.
            var cells = new List<PanelCell> { new PanelCell("AZIM: 23°\nELEV: 45°\nLINK: LOCKED", 100f, 500f) };
            Assert.Equal(new[] { "AZIM: 23°", "ELEV: 45°", "LINK: LOCKED" }, PanelText.Assemble(cells));
        }

        [Fact]
        public void ExactRepeatsCollapse()
        {
            // A panel drawn twice between two of our ticks must not double its rows.
            var cells = new List<PanelCell>
            {
                new PanelCell("300", 10f, 50f),
                new PanelCell("300", 10f, 50f),
                new PanelCell("300", 10f, 8.5f), // same VALUE on the next row stays
            };
            Assert.Equal(new[] { "300", "300" }, PanelText.Assemble(cells));
        }

        [Fact]
        public void NearYSharesARowFarYDoesNot()
        {
            var cells = new List<PanelCell>
            {
                new PanelCell("header", 200f, 96f), // headers sit 8 above their column, same row
                new PanelCell("label", 10f, 100f),
                new PanelCell("next row", 10f, 58.5f),
            };
            Assert.Equal(new[] { "label, header", "next row" }, PanelText.Assemble(cells));
        }

        [Fact]
        public void BlankCellsAndBlankLinesDrop()
        {
            // Merged header rows carry empty LocStrings (the DISC log's unnamed columns).
            var cells = new List<PanelCell>
            {
                new PanelCell("", 10f, 100f),
                new PanelCell("  ", 50f, 100f),
                new PanelCell("TRACK REQUESTS", 90f, 100f),
                new PanelCell(" \n ", 10f, 50f),
            };
            Assert.Equal(new[] { "TRACK REQUESTS" }, PanelText.Assemble(cells));
        }

        [Fact]
        public void UnequalColumnsLeaveShortRowsSingleCelled()
        {
            // The live (unforced) view: IN revealed further than OUT has been written.
            var cells = new List<PanelCell>
            {
                new PanelCell("-73", 0f, 100f),
                new PanelCell("-73", 206f, 100f),
                new PanelCell("477", 0f, 58.5f),
            };
            Assert.Equal(new[] { "-73, -73", "477" }, PanelText.Assemble(cells));
        }
    }
}
