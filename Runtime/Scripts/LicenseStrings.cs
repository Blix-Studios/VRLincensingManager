using System.Collections.Generic;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// The few license-UI strings that must follow the user's language: the privacy device
    /// code label and the store subscription panel (Meta requires an accurate, localized
    /// description of what the subscription unlocks). The rest of the license UI is English.
    /// Language: <see cref="LanguageOverride"/> if the host app sets it from its own selector,
    /// otherwise the headset's system language. Codes are ISO 639-1. "license" uses US spelling.
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

        private static readonly Dictionary<string, string> BestValueLabels = new Dictionary<string, string>
        {
            { "en", "Best value" },
            { "es", "Mejor valor" },
            { "fr", "Meilleure offre" },
            { "de", "Bestes Angebot" },
            { "it", "Miglior valore" },
            { "pt", "Melhor valor" },
            { "ru", "Самый выгодный" },
            { "zh", "最划算" },
            { "hi", "सबसे किफ़ायती" },
            { "fi", "Paras hinta" },
            { "sv", "Bästa värde" },
        };

        private static readonly Dictionary<string, string> BenefitsLines = new Dictionary<string, string>
        {
            { "en", "Full course: all modules, practical exercises, evaluations and certificate." },
            { "es", "Curso completo: todos los módulos, ejercicios prácticos, evaluaciones y certificado." },
            { "fr", "Cours complet : tous les modules, exercices pratiques, évaluations et certificat." },
            { "de", "Vollständiger Kurs: alle Module, praktische Übungen, Prüfungen und Zertifikat." },
            { "it", "Corso completo: tutti i moduli, esercizi pratici, valutazioni e certificato." },
            { "pt", "Curso completo: todos os módulos, exercícios práticos, avaliações e certificado." },
            { "ru", "Полный курс: все модули, практические упражнения, оценки и сертификат." },
            { "zh", "完整课程：全部模块、实践练习、考核和证书。" },
            { "hi", "पूरा कोर्स: सभी मॉड्यूल, व्यावहारिक अभ्यास, मूल्यांकन और प्रमाणपत्र।" },
            { "fi", "Koko kurssi: kaikki moduulit, käytännön harjoitukset, arvioinnit ja todistus." },
            { "sv", "Hela kursen: alla moduler, praktiska övningar, utvärderingar och certifikat." },
        };

        // Term labels keyed by Meta OfferTerm token, then language.
        private static readonly Dictionary<string, Dictionary<string, string>> TermLabels =
            new Dictionary<string, Dictionary<string, string>>
        {
            { "MONTHLY", new Dictionary<string, string> {
                { "en", "Monthly" }, { "es", "Mensual" }, { "fr", "Mensuel" }, { "de", "Monatlich" },
                { "it", "Mensile" }, { "pt", "Mensal" }, { "ru", "Ежемесячно" }, { "zh", "每月" },
                { "hi", "मासिक" }, { "fi", "Kuukausittain" }, { "sv", "Månadsvis" } } },
            { "SEMIANNUAL", new Dictionary<string, string> {
                { "en", "6 months" }, { "es", "6 meses" }, { "fr", "6 mois" }, { "de", "6 Monate" },
                { "it", "6 mesi" }, { "pt", "6 meses" }, { "ru", "6 месяцев" }, { "zh", "6个月" },
                { "hi", "6 महीने" }, { "fi", "6 kuukautta" }, { "sv", "6 månader" } } },
            { "ANNUAL", new Dictionary<string, string> {
                { "en", "Annual" }, { "es", "Anual" }, { "fr", "Annuel" }, { "de", "Jährlich" },
                { "it", "Annuale" }, { "pt", "Anual" }, { "ru", "Ежегодно" }, { "zh", "每年" },
                { "hi", "वार्षिक" }, { "fi", "Vuosittain" }, { "sv", "Årligen" } } },
            { "WEEKLY", new Dictionary<string, string> {
                { "en", "Weekly" }, { "es", "Semanal" }, { "fr", "Hebdomadaire" }, { "de", "Wöchentlich" },
                { "it", "Settimanale" }, { "pt", "Semanal" }, { "ru", "Еженедельно" }, { "zh", "每周" },
                { "hi", "साप्ताहिक" }, { "fi", "Viikoittain" }, { "sv", "Veckovis" } } },
            { "QUARTERLY", new Dictionary<string, string> {
                { "en", "3 months" }, { "es", "3 meses" }, { "fr", "3 mois" }, { "de", "3 Monate" },
                { "it", "3 mesi" }, { "pt", "3 meses" }, { "ru", "3 месяца" }, { "zh", "3个月" },
                { "hi", "3 महीने" }, { "fi", "3 kuukautta" }, { "sv", "3 månader" } } },
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

        private static string Pick(Dictionary<string, string> table, string language)
        {
            if (language != null && table.TryGetValue(language, out string s)) return s;
            return table["en"];
        }

        /// <summary>"Device code" in the given language (English when unknown).</summary>
        public static string DeviceCodeLabel(string language) => Pick(DeviceCodeLabels, language);

        /// <summary>Full footer line, e.g. "Código del dispositivo: 3F9A1-C02B7".</summary>
        public static string DeviceCodeLine(string code, string language = null)
        {
            return DeviceCodeLabel(language ?? CurrentLanguage) + ": " + code;
        }

        /// <summary>"Best value" badge for the annual plan.</summary>
        public static string BestValue(string language = null) => Pick(BestValueLabels, language ?? CurrentLanguage);

        /// <summary>What the subscription unlocks (Meta requires an accurate in-app description).</summary>
        public static string Benefits(string language = null) => Pick(BenefitsLines, language ?? CurrentLanguage);

        /// <summary>Human label for a Meta OfferTerm token; the token itself when unknown.</summary>
        public static string TermLabel(string term, string language = null)
        {
            string key = StoreSubscription.NormalizeTerm(term);
            if (TermLabels.TryGetValue(key, out var table)) return Pick(table, language ?? CurrentLanguage);
            return key;
        }
    }
}
