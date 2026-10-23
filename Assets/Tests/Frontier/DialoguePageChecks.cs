using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void DialogueLastPageUsesVisibleChoiceFootprint(Campaign data)
        {
            // Seven and thirteen entry conversations occur in shipped story
            // menus. The last entry is a single full-width button, not a
            // phantom 409px half-width focus target.
            Check(DialogueLayout.VisibleChoiceCount(7, 0, 6) == 6 &&
                  DialogueLayout.VisibleChoiceCount(7, 1, 6) == 1 &&
                  DialogueLayout.VisibleChoiceCount(13, 2, 6) == 1,
                  "Incomplete story choice pages keep only their actual entries");
            Check(DialogueLayout.ChoiceColumns(1) == 1 &&
                  DialogueLayout.ChoiceWidth(1, 832f, 14f) == 832f,
                  "Single final choice occupies the same full width for drawing and controller focus");
            Check(DialogueLayout.VisibleChoiceCount(8, 1, 6) == 2 &&
                  DialogueLayout.ChoiceColumns(2) == 2 &&
                  Math.Abs(DialogueLayout.ChoiceWidth(2, 832f, 14f) - 409f) < .001f &&
                  DialogueLayout.VisibleChoiceCount(9, 1, 6) == 3 &&
                  DialogueLayout.ChoiceColumns(3) == 2,
                  "Two and three last-page choices retain the original two-column interaction");
            Check(DialogueLayout.VisibleChoiceCount(6, 1, 6) == 0 &&
                  DialogueLayout.VisibleChoiceCount(13, 3, 6) == 0 &&
                  DialogueLayout.VisibleChoiceCount(7, -1, 6) == 0 &&
                  DialogueLayout.VisibleChoiceCount(7, int.MaxValue, 6) == 0 &&
                  DialogueLayout.VisibleChoiceCount(7, 0, 0) == 0,
                  "Out-of-bounds choice pages cannot produce off-screen or overflowing button geometry");
        }
    }
}
