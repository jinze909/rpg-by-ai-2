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

        public static void FieldGuidePagesPreserveLongBilingualInstructions(Campaign data)
        {
            var guide=new System.Text.StringBuilder();
            for(int i=0;i<24;i++)
                guide.Append("Defend the receiver, return to the inn, and check the real route.")
                    .Append('\n');
            string english=guide.ToString();
            string chinese=new string('路',550)+"。\n"+new string('灯',500);
            for(int mode=0;mode<2;mode++)
            {
                var enPages=DialogueLayout.HelpPages(english,mode==1);
                var cnPages=DialogueLayout.HelpPages(chinese,mode==1);
                Check(enPages.Count>1&&cnPages.Count>1 &&
                    string.Concat(enPages.ToArray())==english &&
                    string.Concat(cnPages.ToArray())==chinese,
                    "Every long guide instruction remains readable over real help pages");
                foreach(string page in enPages)
                    Check(page.Length>0,"Help pagination cannot introduce blank phantom cards");
                foreach(string page in cnPages)
                    Check(page.Length>0,"Chinese help pagination cannot create blank cards");
            }
            Check(DialogueLayout.HelpPages(null).Count==1 &&
                DialogueLayout.HelpPages(null)[0]=="",
                "Empty help topics render as one safe reader card");
        }

        public static void DialoguePagesRespectParagraphLinesAndCompleteEllipses(Campaign data)
        {
            // The shipped 228px card cannot show a ninth blank line caused
            // by the CRLF that separated page eight from page nine.
            for(int mode=0;mode<2;mode++)
            {
                bool large=mode==1;
                var lines=new string[10];
                for(int i=0;i<lines.Length;i++)
                    lines[i]="Medicine reached the station. Record "+i+".";
                string report=string.Join("\r\n",lines);
                var reportPages=DialogueLayout.Pages(report,large);
                Check(reportPages.Count>=2 &&
                    reportPages[0].EndsWith("\r\n") &&
                    DialogueLayout.VisiblePageText(reportPages[0])
                        .Split(new[]{"\r\n"},StringSplitOptions.None).Length==
                            (large?7:8) &&
                    !DialogueLayout.VisiblePageText(reportPages[0]).EndsWith("\r") &&
                    string.Concat(reportPages.ToArray())==report,
                    "Original paragraph breaks remain exact but never draw a phantom ninth line");

                string western=new string('a',large?542:660)+
                    "...”)" +new string('b',100);
                var westernPages=DialogueLayout.Pages(western,large);
                Check(westernPages.Count>1 &&
                    westernPages[0].EndsWith("...”)") &&
                    string.Concat(westernPages.ToArray())==western,
                    "An English ellipsis and closing quotation travel together between card pages");
                for(int p=1;p<westernPages.Count;p++)
                    Check(!westernPages[p].StartsWith(".") &&
                        !westernPages[p].StartsWith("”)"),
                        "A continued English card cannot start with orphaned dots or the old quote");

                string chinese=new string('甲',large?244:304)+
                    "……”"+new string('乙',80);
                var chinesePages=DialogueLayout.Pages(chinese,large);
                Check(chinesePages.Count>1 &&
                    chinesePages[0].EndsWith("……”") &&
                    string.Concat(chinesePages.ToArray())==chinese,
                    "A full-width Chinese ellipsis and dialogue quote remain joined");
            }
            // A full-body emoji can sit exactly on the final glyph-width
            // budget. Its ZWJ-linked tool or skin tone belongs to that same
            // glyph even when the scalar itself would exceed the page width.
            for(int mode=0;mode<2;mode++)
            {
                bool large=mode==1;
                string prefix=new string('甲',large?251:311);
                string joined=prefix+"👩\u200d🔬"+new string('乙',90);
                var joinedPages=DialogueLayout.Pages(joined,large);
                Check(joinedPages.Count>1 &&
                    joinedPages[0].EndsWith("👩\u200d🔬") &&
                    string.Concat(joinedPages.ToArray())==joined,
                    "Joined emoji remains whole on its original dialogue card");
                string toned=prefix+"👋🏽"+new string('丙',90);
                var tonedPages=DialogueLayout.Pages(toned,large);
                Check(tonedPages.Count>1 &&
                    tonedPages[0].EndsWith("👋🏽") &&
                    string.Concat(tonedPages.ToArray())==toned,
                    "Emoji skin-tone modifiers stay with the base glyph");
            }
            Check(DialogueLayout.VisiblePageText(null)=="" &&
                DialogueLayout.VisiblePageText("a\r\nb\r\n")=="a\r\nb" &&
                DialogueLayout.VisiblePageText("a\nb")=="a\nb",
                "Visible dialogue strips only the trailing visual page separator");
        }

    }
}
