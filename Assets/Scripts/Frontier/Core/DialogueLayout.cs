using System;
using System.Collections.Generic;
namespace Starfall
{
    public static class DialogueLayout
    {
        // Choice geometry is shared by the drawn page and its gamepad focus.
        // The last page often has fewer choices than the full conversation:
        // e.g. 7 choices -> 6 on page one and ONE full-width control on page two.
        public static int VisibleChoiceCount(int total, int page, int pageSize)
        {
            if (total <= 0 || page < 0 || pageSize <= 0) return 0;
            long start = (long)page * pageSize;
            return start >= total ? 0 : Math.Min(pageSize, total - (int)start);
        }
        public static int ChoiceColumns(int visible)
        { return visible > 2 ? 2 : Math.Max(1, visible); }
        public static float ChoiceWidth(int visible, float totalWidth, float gap)
        { int cols = ChoiceColumns(visible); return (totalWidth - (cols - 1) * gap) / cols; }

        // Weighted visual line budgets used by the *shipped* dialogue card.
        // Keep the source text byte-for-byte readable across successive pages:
        // English words, Chinese sentence stops, CRLF and astral characters
        // must not be broken merely because a line's estimated width expired.
        private static bool DialogueSentenceStop(char c, string text, int next)
        {
            if(c=='.' && next<text.Length && char.IsDigit(text[next])) return false;
            return "。！？!?；;.".IndexOf(c)>=0;
        }
        private static bool DialogueClosing(char c)
        { return "，。、！？：；）】”’」』,.!?;:%)]".IndexOf(c)>=0; }
        private static bool DialogueOpening(char c)
        { return "（【“‘「『(".IndexOf(c)>=0; }
        private static bool DialogueCombining(char c)
        {
            System.Globalization.UnicodeCategory type=char.GetUnicodeCategory(c);
            return type==System.Globalization.UnicodeCategory.NonSpacingMark ||
                type==System.Globalization.UnicodeCategory.SpacingCombiningMark ||
                type==System.Globalization.UnicodeCategory.EnclosingMark;
        }
        private static int PreviousDialogueScalar(string text,int end)
        {
            int previous=end-1;
            return previous>0 && char.IsLowSurrogate(text[previous]) &&
                char.IsHighSurrogate(text[previous-1]) ? previous-1 : previous;
        }
        public static List<string> Pages(string text, bool largeText = false)
        {
            var pages=new List<string>();
            if(string.IsNullOrEmpty(text)){pages.Add("");return pages;}
            float width=largeText?73f:78f;
            int maxLines=largeText?7:8;
            int start=0;
            while(start<text.Length)
            {
                int i=start,lines=1,lastSentence=-1,lastWord=-1;
                float units=0f;
                bool paragraphLimit=false;
                while(i<text.Length)
                {
                    char c=text[i];
                    if(c=='\r'||c=='\n')
                    {
                        // A Windows line ending is one paragraph boundary,
                        // never two lines or two different reading pages.
                        i+=(c=='\r' && i+1<text.Length && text[i+1]=='\n')?2:1;
                        lines++;units=0f;lastSentence=lastWord=i;
                        if(lines>maxLines){paragraphLimit=true;break;}
                        continue;
                    }
                    int size=char.IsHighSurrogate(c) && i+1<text.Length &&
                        char.IsLowSurrogate(text[i+1])?2:1;
                    float add=DialogueCombining(c)?0f:
                        size==2 || c>=0x2e80?2f:char.IsUpper(c)?1.2f:.93f;
                    if(units+add>width)
                    {
                        if(lines>=maxLines)break;
                        lines++;units=0f;
                    }
                    units+=add;
                    i+=size;
                    if(char.IsWhiteSpace(c))lastWord=i;
                    if(DialogueSentenceStop(c,text,i))lastSentence=i;
                }
                if(i>=text.Length){pages.Add(text.Substring(start));break;}
                int end=i;
                if(!paragraphLimit)
                {
                    // A nearby authored sentence stop is preferable to a
                    // mid-phrase break; otherwise finish the last whole word.
                    // Do not turn a remote full stop into a tiny page.
                    if(lastSentence>start && i-lastSentence<=54)end=lastSentence;
                    else if(lastWord>start && i-lastWord<=32)end=lastWord;
                    // Never strand a closing Chinese mark or its quotation
                    // at the beginning of the following dialogue page.
                    while(end<text.Length &&
                        (DialogueClosing(text[end])||DialogueCombining(text[end])))
                    {
                        // The punctuation closes the PRECEDING sentence. Move
                        // the page boundary right, keeping the full cluster
                        // with that sentence; moving left stranded even its
                        // period at the start of the next page.
                        end+=char.IsHighSurrogate(text[end]) && end+1<text.Length &&
                            char.IsLowSurrogate(text[end+1])?2:1;
                    }
                    // Similarly keep an opening quotation with its phrase.
                    if(end>start+1 && DialogueOpening(text[end-1]))
                        end=PreviousDialogueScalar(text,end);
                }
                if(end<=start)
                    end=start+(char.IsHighSurrogate(text[start]) &&
                        start+1<text.Length && char.IsLowSurrogate(text[start+1])?2:1);
                pages.Add(text.Substring(start,end-start));
                start=end;
            }
            return pages;
        }
    }
}
