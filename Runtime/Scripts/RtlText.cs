using System.Collections.Generic;
using System.Text;
using TMPro;

namespace VRLicensing
{
    /// <summary>
    /// v1.12.0-pre.17 - Arabic-script support for the license UI (Arabic, Urdu).
    /// TextMeshPro draws one glyph per character and does no Arabic shaping, so a logical string
    /// would come out as disconnected letters in the wrong direction. This class turns a logical
    /// string into what a TMP label in right-to-left mode (<c>isRightToLeftText</c>) needs:
    /// <list type="bullet">
    /// <item>every letter replaced by its contextual presentation form (isolated / final /
    /// initial / medial), lam + alef joined into their ligature;</item>
    /// <item>the order kept logical — TMP lays the characters out from the right and wraps
    /// lines correctly that way — except Latin / digit runs ("Meta", "15 min",
    /// "XXXX-XXXX-XXXX-XXXX"), which are reversed so they still read left to right;</item>
    /// <item>brackets mirrored; rich-text tags left alone.</item>
    /// </list>
    /// Text that somebody else already shaped into visual order (a host app running its own
    /// Arabic fixer over every label) is recognised — presentation forms, no base letters — and
    /// only turned around, so it is drawn the same way.
    /// No dependency on any shaping plugin; the tables below are Unicode's Arabic Presentation
    /// Forms-A/-B.
    /// </summary>
    public static class RtlText
    {
        // { isolated, final, initial, medial }; '\0' = the letter has no such form (it does not
        // join to the letter after it).
        private static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>
        {
            { 'ء', F(0xFE80, 0, 0, 0) },                       // hamza
            { 'آ', F(0xFE81, 0xFE82, 0, 0) },                  // alef madda
            { 'أ', F(0xFE83, 0xFE84, 0, 0) },                  // alef hamza above
            { 'ؤ', F(0xFE85, 0xFE86, 0, 0) },                  // waw hamza
            { 'إ', F(0xFE87, 0xFE88, 0, 0) },                  // alef hamza below
            { 'ئ', F(0xFE89, 0xFE8A, 0xFE8B, 0xFE8C) },        // yeh hamza
            { 'ا', F(0xFE8D, 0xFE8E, 0, 0) },                  // alef
            { 'ب', F(0xFE8F, 0xFE90, 0xFE91, 0xFE92) },        // beh
            { 'ة', F(0xFE93, 0xFE94, 0, 0) },                  // teh marbuta
            { 'ت', F(0xFE95, 0xFE96, 0xFE97, 0xFE98) },        // teh
            { 'ث', F(0xFE99, 0xFE9A, 0xFE9B, 0xFE9C) },        // theh
            { 'ج', F(0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0) },        // jeem
            { 'ح', F(0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4) },        // hah
            { 'خ', F(0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8) },        // khah
            { 'د', F(0xFEA9, 0xFEAA, 0, 0) },                  // dal
            { 'ذ', F(0xFEAB, 0xFEAC, 0, 0) },                  // thal
            { 'ر', F(0xFEAD, 0xFEAE, 0, 0) },                  // reh
            { 'ز', F(0xFEAF, 0xFEB0, 0, 0) },                  // zain
            { 'س', F(0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4) },        // seen
            { 'ش', F(0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8) },        // sheen
            { 'ص', F(0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC) },        // sad
            { 'ض', F(0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0) },        // dad
            { 'ط', F(0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4) },        // tah
            { 'ظ', F(0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8) },        // zah
            { 'ع', F(0xFEC9, 0xFECA, 0xFECB, 0xFECC) },        // ain
            { 'غ', F(0xFECD, 0xFECE, 0xFECF, 0xFED0) },        // ghain
            { 'ف', F(0xFED1, 0xFED2, 0xFED3, 0xFED4) },        // feh
            { 'ق', F(0xFED5, 0xFED6, 0xFED7, 0xFED8) },        // qaf
            { 'ك', F(0xFED9, 0xFEDA, 0xFEDB, 0xFEDC) },        // kaf
            { 'ل', F(0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0) },        // lam
            { 'م', F(0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4) },        // meem
            { 'ن', F(0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8) },        // noon
            { 'ه', F(0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC) },        // heh
            { 'و', F(0xFEED, 0xFEEE, 0, 0) },                  // waw
            { 'ى', F(0xFEEF, 0xFEF0, 0, 0) },                  // alef maksura
            { 'ي', F(0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4) },        // yeh
            // Urdu / Persian
            { 'ٹ', F(0xFB66, 0xFB67, 0xFB68, 0xFB69) },        // tteh
            { 'پ', F(0xFB56, 0xFB57, 0xFB58, 0xFB59) },        // peh
            { 'چ', F(0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D) },        // tcheh
            { 'ڈ', F(0xFB88, 0xFB89, 0, 0) },                  // ddal
            { 'ڑ', F(0xFB8C, 0xFB8D, 0, 0) },                  // rreh
            { 'ژ', F(0xFB8A, 0xFB8B, 0, 0) },                  // jeh
            { 'ک', F(0xFB8E, 0xFB8F, 0xFB90, 0xFB91) },        // keheh
            { 'گ', F(0xFB92, 0xFB93, 0xFB94, 0xFB95) },        // gaf
            { 'ں', F(0xFB9E, 0xFB9F, 0, 0) },                  // noon ghunna
            { 'ھ', F(0xFBAA, 0xFBAB, 0xFBAC, 0xFBAD) },        // heh doachashmee
            { 'ہ', F(0xFBA6, 0xFBA7, 0xFBA8, 0xFBA9) },        // heh goal
            { 'ی', F(0xFBFC, 0xFBFD, 0xFBFE, 0xFBFF) },        // farsi yeh
            { 'ے', F(0xFBAE, 0xFBAF, 0, 0) },                  // yeh barree
            { 'ۓ', F(0xFBB0, 0xFBB1, 0, 0) },                  // yeh barree hamza
        };

        // lam + alef variants → { isolated, final }
        private static readonly Dictionary<char, char[]> LamAlef = new Dictionary<char, char[]>
        {
            { 'آ', new[] { 'ﻵ', 'ﻶ' } },
            { 'أ', new[] { 'ﻷ', 'ﻸ' } },
            { 'إ', new[] { 'ﻹ', 'ﻺ' } },
            { 'ا', new[] { 'ﻻ', 'ﻼ' } },
        };

        private const char Lam = 'ل';
        private const char Tatweel = 'ـ';

        private static char[] F(int isolated, int final, int initial, int medial)
        {
            return new[] { (char)isolated, (char)final, (char)initial, (char)medial };
        }

        // ───────────── classification ─────────────

        /// <summary>A base Arabic-script letter (not yet shaped).</summary>
        public static bool IsArabicLetter(char c)
        {
            return Forms.ContainsKey(c) || c == Tatweel;
        }

        /// <summary>Harakat and other marks that sit on a letter and do not break its joining.</summary>
        private static bool IsTransparent(char c)
        {
            return (c >= 'ً' && c <= 'ٟ') || c == 'ٰ' || (c >= 'ۖ' && c <= 'ۭ');
        }

        private static bool IsPresentationForm(char c)
        {
            return (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= 'ﻼ');
        }

        private static bool IsRtlChar(char c)
        {
            return (c >= '֐' && c <= 'ࣿ') || IsPresentationForm(c);
        }

        /// <summary>Strong left-to-right: Latin letters and European digits (digits stay LTR inside Arabic).</summary>
        private static bool IsLtrChar(char c)
        {
            if (c >= '0' && c <= '9') return true;
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return true;
            return c >= 'À' && c <= 'ɏ'; // accented Latin
        }

        public static bool HasArabicLetters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text) if (Forms.ContainsKey(c)) return true;
            return false;
        }

        public static bool HasPresentationForms(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text) if (IsPresentationForm(c)) return true;
            return false;
        }

        /// <summary>True when the text has Arabic-script characters, shaped or not: the label must be in RTL mode.</summary>
        public static bool IsRtl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text) if (Forms.ContainsKey(c) || IsPresentationForm(c)) return true;
            return false;
        }

        // ───────────── entry point ─────────────

        /// <summary>
        /// What a TMP label in right-to-left mode must be given to show <paramref name="text"/>.
        /// Text without Arabic script is returned as it is (same instance).
        /// </summary>
        public static string ForRtlLayout(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            bool letters = HasArabicLetters(text);
            if (!letters && !HasPresentationForms(text)) return text;

            var result = new StringBuilder(text.Length + 8);
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && text[i] != '\n') continue;
                string line = text.Substring(start, i - start);
                result.Append(letters ? ShapeLine(line) : ReverseLine(line));
                if (i < text.Length) result.Append('\n');
                start = i + 1;
            }
            return result.ToString();
        }

        // ───────────── shaping ─────────────

        /// <summary>One line, logical order in, logical order out (shaped, LTR runs reversed, brackets mirrored).</summary>
        private static string ShapeLine(string line)
        {
            int n = line.Length;
            if (n == 0) return line;

            bool[] inTag = MarkTags(line);
            var shaped = new List<char>(n);
            var tagFlag = new List<bool>(n);

            for (int i = 0; i < n; i++)
            {
                char c = line[i];
                if (inTag[i] || !Forms.TryGetValue(c, out char[] forms))
                {
                    shaped.Add(c);
                    tagFlag.Add(inTag[i]);
                    continue;
                }

                bool joinsPrevious = JoinsToPrevious(line, inTag, i);

                // lam + alef → one ligature glyph
                if (c == Lam)
                {
                    int a = NextLetter(line, inTag, i);
                    if (a >= 0 && LamAlef.TryGetValue(line[a], out char[] ligature))
                    {
                        shaped.Add(joinsPrevious ? ligature[1] : ligature[0]);
                        tagFlag.Add(false);
                        for (int k = i + 1; k < a; k++) { shaped.Add(line[k]); tagFlag.Add(false); } // marks between them
                        i = a;
                        continue;
                    }
                }

                bool joinsNext = forms[2] != '\0' && JoinsToNext(line, inTag, i);
                char form = joinsPrevious && joinsNext && forms[3] != '\0' ? forms[3]
                    : joinsPrevious && forms[1] != '\0' ? forms[1]
                    : joinsNext ? forms[2]
                    : forms[0];
                shaped.Add(form);
                tagFlag.Add(false);
            }

            ReverseLtrRuns(shaped, tagFlag);
            return new string(shaped.ToArray());
        }

        /// <summary>Index of the next character that takes part in joining, skipping marks; -1 at a tag or the end.</summary>
        private static int NextLetter(string line, bool[] inTag, int i)
        {
            for (int k = i + 1; k < line.Length; k++)
            {
                if (inTag[k]) return -1;
                if (IsTransparent(line[k])) continue;
                return k;
            }
            return -1;
        }

        private static int PreviousLetter(string line, bool[] inTag, int i)
        {
            for (int k = i - 1; k >= 0; k--)
            {
                if (inTag[k]) return -1;
                if (IsTransparent(line[k])) continue;
                return k;
            }
            return -1;
        }

        /// <summary>The letter before joins forward (it is dual-joining, or a tatweel).</summary>
        private static bool JoinsToPrevious(string line, bool[] inTag, int i)
        {
            if (line[i] == 'ء') return false; // hamza joins nothing
            int p = PreviousLetter(line, inTag, i);
            if (p < 0) return false;
            char previous = line[p];
            if (previous == Tatweel) return true;
            return Forms.TryGetValue(previous, out char[] forms) && forms[2] != '\0';
        }

        /// <summary>The letter after accepts a join from the right (any letter but hamza, or a tatweel).</summary>
        private static bool JoinsToNext(string line, bool[] inTag, int i)
        {
            int nx = NextLetter(line, inTag, i);
            if (nx < 0) return false;
            char next = line[nx];
            if (next == Tatweel) return true;
            return next != 'ء' && Forms.ContainsKey(next);
        }

        // ───────────── direction ─────────────

        /// <summary>
        /// In RTL layout every character is placed to the left of the one before it. A run of
        /// Latin / digits (with the spaces and punctuation inside it) is reversed here so it
        /// reads left to right again; brackets outside those runs are mirrored.
        /// </summary>
        private static void ReverseLtrRuns(List<char> chars, List<bool> tag)
        {
            int n = chars.Count;
            int i = 0;
            while (i < n)
            {
                if (tag[i] || !IsLtrChar(chars[i]))
                {
                    if (!tag[i]) chars[i] = Mirror(chars[i]);
                    i++;
                    continue;
                }

                // Run: from this LTR character to the last LTR character before an RTL
                // character or a tag; neutrals in between belong to it.
                int end = i;
                for (int k = i + 1; k < n; k++)
                {
                    if (tag[k] || IsRtlChar(chars[k])) break;
                    if (IsLtrChar(chars[k])) end = k;
                }
                // A bracket opened inside the run closes with it: "XXXX (format)" keeps its ")".
                int open = 0;
                for (int k = i; k <= end; k++)
                {
                    if (chars[k] == '(' || chars[k] == '[') open++;
                    else if ((chars[k] == ')' || chars[k] == ']') && open > 0) open--;
                }
                while (open > 0 && end + 1 < n && !tag[end + 1] && (chars[end + 1] == ')' || chars[end + 1] == ']'))
                {
                    end++;
                    open--;
                }

                for (int a = i, b = end; a < b; a++, b--)
                {
                    char t = chars[a]; chars[a] = chars[b]; chars[b] = t;
                }
                i = end + 1;
            }
        }

        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '{': return '}';
                case '}': return '{';
                case '«': return '»';
                case '»': return '«';
                default: return c;
            }
        }

        /// <summary>Marks the characters of rich-text tags (&lt;b&gt;, &lt;/color&gt;, &lt;size=80%&gt;…).</summary>
        private static bool[] MarkTags(string line)
        {
            var mark = new bool[line.Length];
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != '<') continue;
                int close = line.IndexOf('>', i + 1);
                if (close < 0 || close - i > 48 || close == i + 1) continue;
                char first = line[i + 1];
                bool looksLikeTag = first == '/' || first == '#' || (first >= 'a' && first <= 'z') || (first >= 'A' && first <= 'Z');
                if (!looksLikeTag || line.IndexOf('<', i + 1, close - i - 1) >= 0) continue;
                for (int k = i; k <= close; k++) mark[k] = true;
                i = close;
            }
            return mark;
        }

        /// <summary>Already-shaped text in visual order (somebody else's fixer): turned around for RTL layout.</summary>
        private static string ReverseLine(string line)
        {
            char[] chars = line.ToCharArray();
            System.Array.Reverse(chars);
            return new string(chars);
        }

        // ───────────── TMP hook ─────────────

        /// <summary>
        /// Preprocessor for the license UI's labels: whatever text is assigned, Arabic script is
        /// shaped just before TMP parses it. Labels keep their logical text in <c>.text</c>.
        /// </summary>
        public sealed class Preprocessor : ITextPreprocessor
        {
            public static readonly Preprocessor Instance = new Preprocessor();

            public string PreprocessText(string text)
            {
                return ForRtlLayout(text);
            }
        }
    }
}
