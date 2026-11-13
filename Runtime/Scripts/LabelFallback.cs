using System;
using System.Collections.Generic;

namespace VRLicensing
{
    /// <summary>
    /// v1.12.0-pre.17 - which text a label shows and with which font, so that a license label is
    /// never left empty: the localized text with the first font that can draw it, otherwise the
    /// English text with the first font that can draw that. Fonts are tried in the order given
    /// (the label's own, the project's current default, the font the UI was built with).
    /// Generic over the font type so the rule can be tested without font assets.
    /// </summary>
    public static class LabelFallback
    {
        /// <returns>True when some font can draw the chosen text; false when none can draw either
        /// text (then English with the first font is returned, as before).</returns>
        public static bool Choose<TFont>(IList<TFont> fonts, string localized, string english,
            Func<TFont, string, bool> canRender, out string text, out TFont font)
        {
            foreach (string candidate in new[] { localized, english })
            {
                if (candidate == null) continue;
                for (int i = 0; i < fonts.Count; i++)
                {
                    if (!canRender(fonts[i], candidate)) continue;
                    text = candidate;
                    font = fonts[i];
                    return true;
                }
            }

            text = english ?? localized;
            font = fonts.Count > 0 ? fonts[0] : default(TFont);
            return false;
        }
    }
}
