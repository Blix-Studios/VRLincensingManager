using System.Collections.Generic;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// The few license-UI strings that must follow the user's language. The rest of the
    /// license UI is English on purpose; this covers what the privacy flow needs the user to
    /// find in their own language ("Device code", US spelling of "license" throughout).
    /// Language: <see cref="LanguageOverride"/> if the host app sets it from its own selector,
    /// otherwise the headset's system language. Codes are ISO 639-1.
    /// </summary>
    public static class LicenseStrings
    {
        /// <summary>
        /// Optional ISO 639-1 code ("es", "pt", ...) set by the host app when it has its own
        /// language selector. Null/empty = follow Application.systemLanguage.
        /// </summary>
        public static string LanguageOverride;

        public static readonly string[] SupportedLanguages =
            { "en", "es", "fr", "de", "it", "pt", "ru", "zh", "hi", "fi", "sv" };

        private static readonly Dictionary<string, string> DeviceCodeLabels = new Dictionary<string, string>
        {
            { "en", "Device code" },
            { "es", "Código del dispositivo" },
            { "fr", "Code de l'appareil" },
            { "de", "Gerätecode" },
            { "it", "Codice del dispositivo" },
            { "pt", "Código do dispositivo" },
            { "ru", "Код устройства" },
            { "zh", "设备代码" },
            { "hi", "डिवाइस कोड" },
            { "fi", "Laitekoodi" },
            { "sv", "Enhetskod" },
        };

        public static string CurrentLanguage
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(LanguageOverride))
                {
                    string code = LanguageOverride.Trim().ToLowerInvariant();
                    int dash = code.IndexOf('-');
                    if (dash > 0) code = code.Substring(0, dash);
                    if (DeviceCodeLabels.ContainsKey(code)) return code;
                }
                return FromSystemLanguage(Application.systemLanguage);
            }
        }

        public static string FromSystemLanguage(SystemLanguage lang)
        {
            switch (lang)
            {
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return "zh";
                case SystemLanguage.Hindi: return "hi";
                case SystemLanguage.Finnish: return "fi";
                case SystemLanguage.Swedish: return "sv";
                default: return "en";
            }
        }

        /// <summary>"Device code" in the given language (English when unknown).</summary>
        public static string DeviceCodeLabel(string language)
        {
            if (language != null && DeviceCodeLabels.TryGetValue(language, out string label)) return label;
            return DeviceCodeLabels["en"];
        }

        /// <summary>Full footer line, e.g. "Código del dispositivo: 3F9A1-C02B7".</summary>
        public static string DeviceCodeLine(string code, string language = null)
        {
            return DeviceCodeLabel(language ?? CurrentLanguage) + ": " + code;
        }
    }
}
