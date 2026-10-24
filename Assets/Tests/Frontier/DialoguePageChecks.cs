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
        public static void DialoguePagesKeepClosingPunctuationWithItsSentence(Campaign data)
        {
            // A period near the visual line budget must not push its following
            // quote/bracket/combining mark onto the next dialogue card.
            // These are the ACTUAL normal and large-text page limits.
            for(int mode=0;mode<2;mode++)
            {
                bool large=mode==1;
                string chinese=new string('甲',large?244:304)+"。】”\u0301"+
                    new string('乙',120);
                var pages=DialogueLayout.Pages(chinese,large);
                Check(pages.Count>1 && pages[0].EndsWith("。】”\u0301") &&
                    string.Concat(pages.ToArray())==chinese,
                    "Chinese closing quote and combining mark stay with their sentence, byte-for-byte");
                for(int i=1;i<pages.Count;i++)
                    Check(pages[i].Length>0 &&
                        "。】”\u0301".IndexOf(pages[i][0])<0,
                        "No continued Chinese page starts with the previous sentence's punctuation");

                string english=new string('a',large?542:660)+".)]"+
                    new string('b',140);
                var enPages=DialogueLayout.Pages(english,large);
                Check(enPages.Count>1 && enPages[0].EndsWith(". )]".Replace(" ","")) &&
                    string.Concat(enPages.ToArray())==english,
                    "English sentence-ending brackets remain on the same page without dropping prose");
                for(int i=1;i<enPages.Count;i++)
                    Check(enPages[i].Length>0 && ".)]".IndexOf(enPages[i][0])<0,
                        "No English continuation card starts with a dangling closing bracket");
            }
        }

    }
}
