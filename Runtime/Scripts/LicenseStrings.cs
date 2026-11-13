using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Every user-facing string of the license UI in the user's language (12 languages since
    /// v1.12.0-pre.12: + Dutch; the whole UI since v1.12.0-pre.14 — before that only the device
    /// code label, the subscription panel, feedback and the exit survey were localized; 16 since
    /// v1.12.0-pre.17: + Arabic, Japanese, Korean and Urdu, in LicenseStringsExtraLanguages.cs).
    /// Language: <see cref="LanguageOverride"/> if the host app sets it from its own selector,
    /// otherwise the headset's system language. Codes are ISO 639-1. "license" uses US spelling.
    /// Forms of address follow each language's existing rows: tú / tu / você / du / sinä / je,
    /// vous / Sie / вы / 您 / आप.
    /// </summary>
    public static partial class LicenseStrings
    {
        /// <summary>
        /// Optional ISO 639-1 code ("es", "pt", ...) set by the host app when it has its own
        /// language selector. Null/empty = follow Application.systemLanguage. Setting a different
        /// value raises <see cref="LanguageChanged"/> so an open license panel re-renders.
        /// </summary>
        public static string LanguageOverride
        {
            get => languageOverride;
            set
            {
                if (languageOverride == value) return;
                languageOverride = value;
                LanguageChanged?.Invoke();
            }
        }
        private static string languageOverride;

        /// <summary>Raised when <see cref="LanguageOverride"/> changes (v1.12.0-pre.14).</summary>
        public static event Action LanguageChanged;

        public static readonly string[] SupportedLanguages =
            { "en", "es", "fr", "de", "it", "pt", "ru", "zh", "hi", "fi", "sv", "nl", "ar", "ja", "ko", "ur" };

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
            { "sv", "Enhetskod" }, { "nl", "Apparaatcode" },
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
            { "sv", "Bästa värde" }, { "nl", "Beste keuze" },
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
            { "sv", "Hela kursen: alla moduler, praktiska övningar, utvärderingar och certifikat." }, { "nl", "Volledige cursus: alle modules, praktijkoefeningen, toetsen en certificaat." },
        };

        // Term labels keyed by Meta OfferTerm token, then language.
        private static readonly Dictionary<string, Dictionary<string, string>> TermLabels =
            new Dictionary<string, Dictionary<string, string>>
        {
            { "MONTHLY", new Dictionary<string, string> {
                { "en", "Monthly" }, { "es", "Mensual" }, { "fr", "Mensuel" }, { "de", "Monatlich" },
                { "it", "Mensile" }, { "pt", "Mensal" }, { "ru", "Ежемесячно" }, { "zh", "每月" },
                { "hi", "मासिक" }, { "fi", "Kuukausittain" }, { "sv", "Månadsvis" }, { "nl", "Maandelijks" } } },
            { "SEMIANNUAL", new Dictionary<string, string> {
                { "en", "6 months" }, { "es", "6 meses" }, { "fr", "6 mois" }, { "de", "6 Monate" },
                { "it", "6 mesi" }, { "pt", "6 meses" }, { "ru", "6 месяцев" }, { "zh", "6个月" },
                { "hi", "6 महीने" }, { "fi", "6 kuukautta" }, { "sv", "6 månader" }, { "nl", "6 maanden" } } },
            { "ANNUAL", new Dictionary<string, string> {
                { "en", "Annual" }, { "es", "Anual" }, { "fr", "Annuel" }, { "de", "Jährlich" },
                { "it", "Annuale" }, { "pt", "Anual" }, { "ru", "Ежегодно" }, { "zh", "每年" },
                { "hi", "वार्षिक" }, { "fi", "Vuosittain" }, { "sv", "Årligen" }, { "nl", "Jaarlijks" } } },
            { "WEEKLY", new Dictionary<string, string> {
                { "en", "Weekly" }, { "es", "Semanal" }, { "fr", "Hebdomadaire" }, { "de", "Wöchentlich" },
                { "it", "Settimanale" }, { "pt", "Semanal" }, { "ru", "Еженедельно" }, { "zh", "每周" },
                { "hi", "साप्ताहिक" }, { "fi", "Viikoittain" }, { "sv", "Veckovis" }, { "nl", "Wekelijks" } } },
            { "QUARTERLY", new Dictionary<string, string> {
                { "en", "3 months" }, { "es", "3 meses" }, { "fr", "3 mois" }, { "de", "3 Monate" },
                { "it", "3 mesi" }, { "pt", "3 meses" }, { "ru", "3 месяца" }, { "zh", "3个月" },
                { "hi", "3 महीने" }, { "fi", "3 kuukautta" }, { "sv", "3 månader" }, { "nl", "3 maanden" } } },
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
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                // Urdu has no SystemLanguage value: it comes from LanguageOverride ("ur").
                default: return "en";
            }
        }

        // v1.12.0-pre.8: plans can be opened before / during the demo.
        private static readonly Dictionary<string, string> SubscribeLabels = new Dictionary<string, string>
        {
            { "en", "Subscribe" },
            { "es", "Suscribirse" },
            { "fr", "S'abonner" },
            { "de", "Abonnieren" },
            { "it", "Abbonati" },
            { "pt", "Assinar" },
            { "ru", "Подписаться" },
            { "zh", "订阅" },
            { "hi", "सदस्यता लें" },
            { "fi", "Tilaa" },
            { "sv", "Prenumerera" }, { "nl", "Abonneren" },
        };

        private static readonly Dictionary<string, string> BackLabels = new Dictionary<string, string>
        {
            { "en", "Back" },
            { "es", "Volver" },
            { "fr", "Retour" },
            { "de", "Zurück" },
            { "it", "Indietro" },
            { "pt", "Voltar" },
            { "ru", "Назад" },
            { "zh", "返回" },
            { "hi", "वापस" },
            { "fi", "Takaisin" },
            { "sv", "Tillbaka" }, { "nl", "Terug" },
        };

        // ───────────── v1.12.0-pre.9: intro offers, feedback and the demo exit survey ─────────────

        // Calendar units for intro-offer durations: singular, plural (Russian adds the 2-4 form).
        private static readonly Dictionary<string, string[]> WeekWords = new Dictionary<string, string[]>
        {
            { "en", new[] { "week", "weeks" } }, { "es", new[] { "semana", "semanas" } },
            { "fr", new[] { "semaine", "semaines" } }, { "de", new[] { "Woche", "Wochen" } },
            { "it", new[] { "settimana", "settimane" } }, { "pt", new[] { "semana", "semanas" } },
            { "ru", new[] { "неделя", "недели", "недель" } }, { "zh", new[] { "周", "周" } },
            { "hi", new[] { "सप्ताह", "सप्ताह" } }, { "fi", new[] { "viikko", "viikkoa" } },
            { "sv", new[] { "vecka", "veckor" } }, { "nl", new[] { "week", "weken" } },
        };
        private static readonly Dictionary<string, string[]> MonthWords = new Dictionary<string, string[]>
        {
            { "en", new[] { "month", "months" } }, { "es", new[] { "mes", "meses" } },
            { "fr", new[] { "mois", "mois" } }, { "de", new[] { "Monat", "Monate" } },
            { "it", new[] { "mese", "mesi" } }, { "pt", new[] { "mês", "meses" } },
            { "ru", new[] { "месяц", "месяца", "месяцев" } }, { "zh", new[] { "个月", "个月" } },
            { "hi", new[] { "महीना", "महीने" } }, { "fi", new[] { "kuukausi", "kuukautta" } },
            { "sv", new[] { "månad", "månader" } }, { "nl", new[] { "maand", "maanden" } },
        };
        private static readonly Dictionary<string, string[]> YearWords = new Dictionary<string, string[]>
        {
            { "en", new[] { "year", "years" } }, { "es", new[] { "año", "años" } },
            { "fr", new[] { "an", "ans" } }, { "de", new[] { "Jahr", "Jahre" } },
            { "it", new[] { "anno", "anni" } }, { "pt", new[] { "ano", "anos" } },
            { "ru", new[] { "год", "года", "лет" } }, { "zh", new[] { "年", "年" } },
            { "hi", new[] { "साल", "साल" } }, { "fi", new[] { "vuosi", "vuotta" } },
            { "sv", new[] { "år", "år" } }, { "nl", new[] { "jaar", "jaar" } },
        };

        // "{0}" = duration ("3 months"), "{1}" = regular price. Second line of a plan button
        // whose billing plan carries an intro offer; the first line shows the intro price.
        private static readonly Dictionary<string, string> IntroNoteFormats = new Dictionary<string, string>
        {
            { "en", "for the first {0}, then {1}" },
            { "es", "durante {0}, después {1}" },
            { "fr", "pendant {0}, puis {1}" },
            { "de", "für {0}, danach {1}" },
            { "it", "per {0}, poi {1}" },
            { "pt", "durante {0}, depois {1}" },
            { "ru", "в течение {0}, затем {1}" },
            { "zh", "首{0}，之后{1}" },
            { "hi", "पहले {0} के लिए, फिर {1}" },
            { "fi", "ensimmäiset {0}, sitten {1}" },
            { "sv", "under {0}, därefter {1}" }, { "nl", "de eerste {0}, daarna {1}" },
        };

        private static readonly Dictionary<string, string> FeedbackLabels = new Dictionary<string, string>
        {
            { "en", "Send feedback" }, { "es", "Enviar comentarios" }, { "fr", "Envoyer un avis" },
            { "de", "Feedback senden" }, { "it", "Invia un feedback" }, { "pt", "Enviar feedback" },
            { "ru", "Оставить отзыв" }, { "zh", "发送反馈" }, { "hi", "प्रतिक्रिया भेजें" },
            { "fi", "Lähetä palautetta" }, { "sv", "Skicka feedback" }, { "nl", "Feedback sturen" },
        };
        private static readonly Dictionary<string, string> RatingHints = new Dictionary<string, string>
        {
            { "en", "How would you rate it? (optional)" }, { "es", "¿Qué nota le pones? (opcional)" },
            { "fr", "Quelle note donnez-vous ? (facultatif)" }, { "de", "Wie bewerten Sie es? (optional)" },
            { "it", "Che voto gli dai? (facoltativo)" }, { "pt", "Que nota você dá? (opcional)" },
            { "ru", "Как вы его оцениваете? (необязательно)" }, { "zh", "您的评分？（可选）" },
            { "hi", "आप इसे कैसे रेट करेंगे? (वैकल्पिक)" }, { "fi", "Minkä arvosanan annat? (valinnainen)" },
            { "sv", "Vilket betyg ger du? (frivilligt)" }, { "nl", "Welk cijfer geef je? (optioneel)" },
        };
        private static readonly Dictionary<string, Dictionary<string, string>> CategoryLabels =
            new Dictionary<string, Dictionary<string, string>>
        {
            { AppFeedback.CategoryBug, new Dictionary<string, string> {
                { "en", "Problem / bug" }, { "es", "Problema / error" }, { "fr", "Problème / bug" }, { "de", "Problem / Fehler" },
                { "it", "Problema / bug" }, { "pt", "Problema / erro" }, { "ru", "Проблема / ошибка" }, { "zh", "问题 / 故障" },
                { "hi", "समस्या / बग" }, { "fi", "Ongelma / vika" }, { "sv", "Problem / bugg" }, { "nl", "Probleem / fout" } } },
            { AppFeedback.CategoryPerformance, new Dictionary<string, string> {
                { "en", "Performance" }, { "es", "Rendimiento" }, { "fr", "Performances" }, { "de", "Leistung" },
                { "it", "Prestazioni" }, { "pt", "Desempenho" }, { "ru", "Производительность" }, { "zh", "性能" },
                { "hi", "प्रदर्शन" }, { "fi", "Suorituskyky" }, { "sv", "Prestanda" }, { "nl", "Prestaties" } } },
            { AppFeedback.CategoryContent, new Dictionary<string, string> {
                { "en", "Content" }, { "es", "Contenido" }, { "fr", "Contenu" }, { "de", "Inhalt" },
                { "it", "Contenuti" }, { "pt", "Conteúdo" }, { "ru", "Содержание" }, { "zh", "内容" },
                { "hi", "सामग्री" }, { "fi", "Sisältö" }, { "sv", "Innehåll" }, { "nl", "Inhoud" } } },
            { AppFeedback.CategorySuggestion, new Dictionary<string, string> {
                { "en", "Suggestion" }, { "es", "Sugerencia" }, { "fr", "Suggestion" }, { "de", "Vorschlag" },
                { "it", "Suggerimento" }, { "pt", "Sugestão" }, { "ru", "Предложение" }, { "zh", "建议" },
                { "hi", "सुझाव" }, { "fi", "Ehdotus" }, { "sv", "Förslag" }, { "nl", "Suggestie" } } },
        };
        private static readonly Dictionary<string, string> TextPlaceholders = new Dictionary<string, string>
        {
            { "en", "Tell us more (optional)" }, { "es", "Cuéntanos más (opcional)" }, { "fr", "Dites-nous en plus (facultatif)" },
            { "de", "Erzählen Sie uns mehr (optional)" }, { "it", "Dicci di più (facoltativo)" }, { "pt", "Conte-nos mais (opcional)" },
            { "ru", "Расскажите подробнее (необязательно)" }, { "zh", "告诉我们更多（可选）" }, { "hi", "हमें और बताएं (वैकल्पिक)" },
            { "fi", "Kerro lisää (valinnainen)" }, { "sv", "Berätta mer (frivilligt)" }, { "nl", "Vertel ons meer (optioneel)" },
        };
        private static readonly Dictionary<string, string> SendLabels = new Dictionary<string, string>
        {
            { "en", "Send" }, { "es", "Enviar" }, { "fr", "Envoyer" }, { "de", "Senden" }, { "it", "Invia" },
            { "pt", "Enviar" }, { "ru", "Отправить" }, { "zh", "发送" }, { "hi", "भेजें" }, { "fi", "Lähetä" }, { "sv", "Skicka" }, { "nl", "Versturen" },
        };
        private static readonly Dictionary<string, string> SkipLabels = new Dictionary<string, string>
        {
            { "en", "Skip" }, { "es", "Omitir" }, { "fr", "Passer" }, { "de", "Überspringen" }, { "it", "Salta" },
            { "pt", "Pular" }, { "ru", "Пропустить" }, { "zh", "跳过" }, { "hi", "छोड़ें" }, { "fi", "Ohita" }, { "sv", "Hoppa över" }, { "nl", "Overslaan" },
        };
        private static readonly Dictionary<string, string> ChooseCategoryLines = new Dictionary<string, string>
        {
            { "en", "Choose a category first." }, { "es", "Elige primero una categoría." }, { "fr", "Choisissez d'abord une catégorie." },
            { "de", "Bitte zuerst eine Kategorie wählen." }, { "it", "Scegli prima una categoria." }, { "pt", "Escolha primeiro uma categoria." },
            { "ru", "Сначала выберите категорию." }, { "zh", "请先选择一个类别。" }, { "hi", "पहले एक श्रेणी चुनें।" },
            { "fi", "Valitse ensin luokka." }, { "sv", "Välj först en kategori." }, { "nl", "Kies eerst een categorie." },
        };
        private static readonly Dictionary<string, string> ThanksLines = new Dictionary<string, string>
        {
            { "en", "Thank you for your feedback!" }, { "es", "¡Gracias por tus comentarios!" }, { "fr", "Merci pour votre avis !" },
            { "de", "Vielen Dank für Ihr Feedback!" }, { "it", "Grazie per il tuo feedback!" }, { "pt", "Obrigado pelo seu feedback!" },
            { "ru", "Спасибо за отзыв!" }, { "zh", "感谢您的反馈！" }, { "hi", "आपकी प्रतिक्रिया के लिए धन्यवाद!" },
            { "fi", "Kiitos palautteestasi!" }, { "sv", "Tack för din feedback!" }, { "nl", "Bedankt voor je feedback!" },
        };
        private static readonly Dictionary<string, string> SendFailedLines = new Dictionary<string, string>
        {
            { "en", "Could not send right now. Please try again later." }, { "es", "No se pudo enviar ahora. Inténtalo más tarde." },
            { "fr", "Envoi impossible pour le moment. Réessayez plus tard." }, { "de", "Senden derzeit nicht möglich. Bitte später erneut versuchen." },
            { "it", "Invio non riuscito. Riprova più tardi." }, { "pt", "Não foi possível enviar agora. Tente novamente mais tarde." },
            { "ru", "Не удалось отправить. Попробуйте позже." }, { "zh", "目前无法发送，请稍后再试。" },
            { "hi", "अभी नहीं भेजा जा सका। कृपया बाद में पुनः प्रयास करें।" }, { "fi", "Lähetys ei onnistunut. Yritä myöhemmin uudelleen." },
            { "sv", "Kunde inte skicka just nu. Försök igen senare." }, { "nl", "Versturen lukt nu niet. Probeer het later opnieuw." },
        };
        private static readonly Dictionary<string, string> RateLimitedLines = new Dictionary<string, string>
        {
            { "en", "You have sent enough feedback for today. Thank you!" }, { "es", "Ya has enviado suficientes comentarios por hoy. ¡Gracias!" },
            { "fr", "Vous avez envoyé assez d'avis pour aujourd'hui. Merci !" }, { "de", "Für heute reicht das Feedback. Vielen Dank!" },
            { "it", "Hai già inviato abbastanza feedback per oggi. Grazie!" }, { "pt", "Você já enviou feedback suficiente por hoje. Obrigado!" },
            { "ru", "На сегодня отзывов достаточно. Спасибо!" }, { "zh", "今天的反馈已足够，谢谢！" },
            { "hi", "आज के लिए पर्याप्त प्रतिक्रिया मिल गई। धन्यवाद!" }, { "fi", "Palautetta on jo riittävästi tälle päivälle. Kiitos!" },
            { "sv", "Du har skickat tillräckligt med feedback för idag. Tack!" }, { "nl", "Je hebt vandaag al genoeg feedback gestuurd. Bedankt!" },
        };
        private static readonly Dictionary<string, string> ExitQuestions = new Dictionary<string, string>
        {
            { "en", "What stopped you?" }, { "es", "¿Qué te frenó?" }, { "fr", "Qu'est-ce qui vous a retenu ?" },
            { "de", "Was hat Sie abgehalten?" }, { "it", "Cosa ti ha frenato?" }, { "pt", "O que te impediu?" },
            { "ru", "Что вас остановило?" }, { "zh", "是什么让您止步？" }, { "hi", "आपको किस बात ने रोका?" },
            { "fi", "Mikä pysäytti sinut?" }, { "sv", "Vad hindrade dig?" }, { "nl", "Waarom ben je gestopt?" },
        };
        private static readonly Dictionary<string, Dictionary<string, string>> ExitReasonLabels =
            new Dictionary<string, Dictionary<string, string>>
        {
            { AppFeedback.ReasonPrice, new Dictionary<string, string> {
                { "en", "The price" }, { "es", "El precio" }, { "fr", "Le prix" }, { "de", "Der Preis" },
                { "it", "Il prezzo" }, { "pt", "O preço" }, { "ru", "Цена" }, { "zh", "价格" },
                { "hi", "कीमत" }, { "fi", "Hinta" }, { "sv", "Priset" }, { "nl", "De prijs" } } },
            { AppFeedback.ReasonForMyCompany, new Dictionary<string, string> {
                { "en", "It's for my company" }, { "es", "Es para mi empresa" }, { "fr", "C'est pour mon entreprise" }, { "de", "Es ist für meine Firma" },
                { "it", "È per la mia azienda" }, { "pt", "É para a minha empresa" }, { "ru", "Это для моей компании" }, { "zh", "是给我公司用的" },
                { "hi", "यह मेरी कंपनी के लिए है" }, { "fi", "Se on yritykselleni" }, { "sv", "Det är för mitt företag" }, { "nl", "Het is voor mijn bedrijf of school" } } },
            { AppFeedback.ReasonNotWhatIExpected, new Dictionary<string, string> {
                { "en", "Not what I expected" }, { "es", "No es lo que esperaba" }, { "fr", "Pas ce que j'attendais" }, { "de", "Nicht, was ich erwartet habe" },
                { "it", "Non è quello che mi aspettavo" }, { "pt", "Não é o que eu esperava" }, { "ru", "Не то, что я ожидал" }, { "zh", "和我预期的不同" },
                { "hi", "जैसा सोचा था वैसा नहीं" }, { "fi", "Ei sitä mitä odotin" }, { "sv", "Inte vad jag väntade mig" }, { "nl", "Niet wat ik verwachtte" } } },
            { AppFeedback.ReasonOther, new Dictionary<string, string> {
                { "en", "Other" }, { "es", "Otro motivo" }, { "fr", "Autre" }, { "de", "Sonstiges" },
                { "it", "Altro" }, { "pt", "Outro" }, { "ru", "Другое" }, { "zh", "其他" },
                { "hi", "अन्य" }, { "fi", "Muu" }, { "sv", "Annat" }, { "nl", "Iets anders" } } },
        };
        private static readonly Dictionary<string, string> ExitThanksLines = new Dictionary<string, string>
        {
            { "en", "Thanks for letting us know." }, { "es", "Gracias por contárnoslo." }, { "fr", "Merci de nous l'avoir dit." },
            { "de", "Danke für die Rückmeldung." }, { "it", "Grazie per avercelo detto." }, { "pt", "Obrigado por nos contar." },
            { "ru", "Спасибо, что сообщили." }, { "zh", "感谢您的告知。" }, { "hi", "हमें बताने के लिए धन्यवाद।" },
            { "fi", "Kiitos, että kerroit." }, { "sv", "Tack för att du berättade." }, { "nl", "Bedankt dat je het laat weten." },
        };
        // Shown after "It's for my company": the key path is on the same panel. No URL, QR or
        // price (Meta App Policies 1.1 — store builds never point outside Meta).
        private static readonly Dictionary<string, string> CompanyHintLines = new Dictionary<string, string>
        {
            { "en", "Companies and training centres request a quote on our website and receive license keys — enter yours here." },
            { "es", "Las empresas y centros de formación piden presupuesto en nuestra web y reciben claves de licencia: introduce la tuya aquí." },
            { "fr", "Les entreprises et centres de formation demandent un devis sur notre site et reçoivent des clés de licence : saisissez la vôtre ici." },
            { "de", "Unternehmen und Schulungszentren fordern auf unserer Website ein Angebot an und erhalten Lizenzschlüssel – geben Sie Ihren hier ein." },
            { "it", "Aziende e centri di formazione richiedono un preventivo sul nostro sito e ricevono chiavi di licenza: inserisci la tua qui." },
            { "pt", "Empresas e centros de formação pedem um orçamento no nosso site e recebem chaves de licença: insira a sua aqui." },
            { "ru", "Компании и учебные центры запрашивают предложение на нашем сайте и получают лицензионные ключи — введите свой здесь." },
            { "zh", "企业和培训中心可在我们的网站上索取报价并获得许可证密钥，请在此输入您的密钥。" },
            { "hi", "कंपनियाँ और प्रशिक्षण केंद्र हमारी वेबसाइट पर कोटेशन मांगते हैं और लाइसेंस कुंजी पाते हैं — अपनी कुंजी यहाँ दर्ज करें।" },
            { "fi", "Yritykset ja koulutuskeskukset pyytävät tarjouksen verkkosivuiltamme ja saavat lisenssiavaimet – syötä omasi tähän." },
            { "sv", "Företag och utbildningscenter begär en offert på vår webbplats och får licensnycklar – ange din här." }, { "nl", "Bedrijven en opleidingscentra vragen een offerte aan op onze website en krijgen licentiecodes. Voer je code hier in." },
        };

        /// <summary>
        /// Localized duration of an intro offer: <paramref name="count"/> periods of a Meta
        /// OfferTerm (e.g. 3 × MONTHLY → "3 months", 1 × ANNUAL → "1 year"). Null for unknown terms.
        /// </summary>
        public static string Duration(string term, int count, string language = null)
        {
            if (count <= 0 || !StoreSubscription.TermUnit(term, out int perTerm, out string unit)) return null;
            string lang = language ?? CurrentLanguage;
            int total = perTerm * count;
            Dictionary<string, string[]> words = unit == "week" ? WeekWords : unit == "month" ? MonthWords : YearWords;
            string[] forms = words.TryGetValue(lang, out var f) ? f : words["en"];
            string word = Plural(forms, total, lang);
            if (lang == "zh" || lang == "ja" || lang == "ko") return total + word; // 3个月, 3か月, 3개월
            if (lang == "ar" && total == 2) return word;                          // the dual says "two" by itself
            return total + " " + word;
        }

        private static string Plural(string[] forms, int n, string lang)
        {
            if (lang == "ru" && forms.Length >= 3)
            {
                int mod10 = n % 10, mod100 = n % 100;
                if (mod10 == 1 && mod100 != 11) return forms[0];
                if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return forms[1];
                return forms[2];
            }
            if (lang == "ar" && forms.Length >= 4)
            {
                // Arabic: 1 singular, 2 dual, 3-10 plural, 11 and more singular (accusative).
                if (n == 1) return forms[0];
                if (n == 2) return forms[2];
                return n <= 10 ? forms[1] : forms[3];
            }
            if (lang == "fr" && n < 2) return forms[0]; // French: singular below 2
            return n == 1 ? forms[0] : forms[Math.Min(1, forms.Length - 1)];
        }

        /// <summary>
        /// Second line of a plan button with an intro offer: "for the first 3 months, then US$39.99".
        /// <paramref name="duration"/> comes from <see cref="Duration"/>; prices are the store's strings.
        /// </summary>
        public static string IntroOfferNote(string duration, string regularPrice, string language = null)
        {
            return string.Format(Pick(IntroNoteFormats, language ?? CurrentLanguage), duration, regularPrice);
        }

        /// <summary>"Send feedback" (pause-menu button and feedback panel title).</summary>
        public static string Feedback(string language = null) => Pick(FeedbackLabels, language ?? CurrentLanguage);
        public static string RatingHint(string language = null) => Pick(RatingHints, language ?? CurrentLanguage);
        public static string TextPlaceholder(string language = null) => Pick(TextPlaceholders, language ?? CurrentLanguage);
        public static string Send(string language = null) => Pick(SendLabels, language ?? CurrentLanguage);
        public static string Skip(string language = null) => Pick(SkipLabels, language ?? CurrentLanguage);
        public static string ChooseCategory(string language = null) => Pick(ChooseCategoryLines, language ?? CurrentLanguage);
        public static string Thanks(string language = null) => Pick(ThanksLines, language ?? CurrentLanguage);
        public static string SendFailed(string language = null) => Pick(SendFailedLines, language ?? CurrentLanguage);
        public static string RateLimited(string language = null) => Pick(RateLimitedLines, language ?? CurrentLanguage);
        public static string ExitQuestion(string language = null) => Pick(ExitQuestions, language ?? CurrentLanguage);
        public static string ExitThanks(string language = null) => Pick(ExitThanksLines, language ?? CurrentLanguage);
        public static string CompanyHint(string language = null) => Pick(CompanyHintLines, language ?? CurrentLanguage);

        /// <summary>Label of a feedback category code (bug, performance, content, suggestion).</summary>
        public static string CategoryLabel(string category, string language = null)
        {
            if (category != null && CategoryLabels.TryGetValue(category, out var table)) return Pick(table, language ?? CurrentLanguage);
            return category ?? "";
        }

        /// <summary>Label of a demo-exit reason code (price, for_my_company, not_what_i_expected, other).</summary>
        public static string ExitReasonLabel(string reason, string language = null)
        {
            if (reason != null && ExitReasonLabels.TryGetValue(reason, out var table)) return Pick(table, language ?? CurrentLanguage);
            return reason ?? "";
        }

        /// <summary>"Subscribe" button label (welcome panel, game pause menu).</summary>
        public static string Subscribe(string language = null) => Pick(SubscribeLabels, language ?? CurrentLanguage);

        /// <summary>"Back" button label (plans panel).</summary>
        public static string Back(string language = null) => Pick(BackLabels, language ?? CurrentLanguage);

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

        // ───────────── UI table access (v1.12.0-pre.14) ─────────────

        /// <summary>Every key of the UI table, for the coverage test.</summary>
        public static IEnumerable<string> UiKeys => UiTable.Keys;

        /// <summary>Raw lookup without the English fallback (tests). False when the key or language is missing.</summary>
        public static bool TryGetUi(string key, string language, out string text)
        {
            text = null;
            return key != null && language != null
                && UiTable.TryGetValue(key, out var table) && table.TryGetValue(language, out text);
        }

        /// <summary>
        /// Text of a UI key in <paramref name="language"/> (default: <see cref="CurrentLanguage"/>),
        /// English when the language is missing, the key itself when the key is unknown.
        /// </summary>
        public static string Get(string key, string language = null)
        {
            if (key != null && UiTable.TryGetValue(key, out var table)) return Pick(table, language ?? CurrentLanguage);
            return key ?? "";
        }

        /// <summary>string.Format over <see cref="Get"/>; a malformed row falls back to the English row.</summary>
        public static string Format(string key, string language, params object[] args)
        {
            string pattern = Get(key, language);
            try { return string.Format(pattern, args); }
            catch (FormatException)
            {
                try { return string.Format(Get(key, "en"), args); }
                catch (FormatException) { return pattern; }
            }
        }

        /// <summary>
        /// Short human length of a demo: "15 min", "1 h", "1.5 h" (localized units; Chinese has no
        /// space). Demos are commonly configured well under an hour, so minutes stay minutes.
        /// </summary>
        public static string ShortDuration(float seconds, string language = null)
        {
            string lang = language ?? CurrentLanguage;
            if (seconds < 3600f)
                return Format(Key.MinutesFormat, lang, Mathf.CeilToInt(seconds / 60f));

            float hours = seconds / 3600f;
            string value = Mathf.Approximately(hours, Mathf.Round(hours))
                ? Mathf.RoundToInt(hours).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : hours.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            return Format(Key.HoursFormat, lang, value);
        }

        // ───────────── v1.12.0-pre.14: every remaining license-UI string ─────────────
        // One row per key, every language. Keys are the constants in LicenseStrings.Key;
        // UiKeys / TryGetUi exist for the coverage test. Placeholders are string.Format's.

        /// <summary>Keys of the UI table (v1.12.0-pre.14). Use with <see cref="Get"/> / <see cref="Format"/>.</summary>
        public static class Key
        {
            // Welcome panel
            public const string WelcomeSubtitle = "WelcomeSubtitle";
            public const string StartDemo = "StartDemo";
            public const string EnterKey = "EnterKey";
            public const string ScanQr = "ScanQr";
            // {0} = time remaining, {1} = demo length (both from ShortDuration)
            public const string WelcomeDemoLine = "WelcomeDemoLine";
            // {0} = whole minutes
            public const string MinutesFormat = "MinutesFormat";
            // {0} = hours ("1" or "1.5")
            public const string HoursFormat = "HoursFormat";
            // Key-entry panel
            public const string KeyHint = "KeyHint";
            public const string Activate = "Activate";
            public const string Validating = "Validating";
            public const string Cancel = "Cancel";
            public const string EnterFullKey = "EnterFullKey";
            public const string EnterKeyPrompt = "EnterKeyPrompt";
            public const string ValidationError = "ValidationError";
            // SupabaseClient fallback when the server sends no error text
            public const string InvalidKey = "InvalidKey";
            // {0} = server status code (revoked, suspended…)
            public const string LicenseNotActive = "LicenseNotActive";
            // {0} = transport error from UnityWebRequest
            public const string ConnectionError = "ConnectionError";
            // {0} = parser exception message
            public const string ResponseError = "ResponseError";
            public const string ClockTampered = "ClockTampered";
            // Demo-expired panel
            public const string DemoExpiredTitle = "DemoExpiredTitle";
            // {0} = app display name
            public const string DemoExpiredLine = "DemoExpiredLine";
            public const string HaveKeyTitle = "HaveKeyTitle";
            // License-expired panel, key column
            public const string EnterKeyTitle = "EnterKeyTitle";
            // Store plans column
            public const string UnlockTitle = "UnlockTitle";
            public const string LoadingPlans = "LoadingPlans";
            public const string SubscriptionFoundVerifying = "SubscriptionFoundVerifying";
            public const string SubscriptionVerified = "SubscriptionVerified";
            public const string BillingNote = "BillingNote";
            public const string OpeningCheckout = "OpeningCheckout";
            public const string SubscriptionActivated = "SubscriptionActivated";
            public const string PlansUnavailable = "PlansUnavailable";
            public const string StoreUnavailable = "StoreUnavailable";
            public const string CheckoutCancelled = "CheckoutCancelled";
            // {0} = store error text
            public const string CheckoutFailed = "CheckoutFailed";
            public const string IdentifyFailed = "IdentifyFailed";
            public const string VerifyFailed = "VerifyFailed";
            public const string NoSubscription = "NoSubscription";
            public const string InternetNeeded = "InternetNeeded";
            // Store build whose app-ownership check failed (the app quits shortly after)
            public const string EntitlementBlocked = "EntitlementBlocked";
            // License-expired panel
            public const string LicenseExpiredTitle = "LicenseExpiredTitle";
            // {0} = app display name
            public const string StoreExpiredLine = "StoreExpiredLine";
            public const string ChoosePlanOrKey = "ChoosePlanOrKey";
            // {0} = app display name
            public const string LicenseExpiredLine = "LicenseExpiredLine";
            // {0} = app display name, {1} = server status code
            public const string LicenseInactiveLine = "LicenseInactiveLine";
            public const string ChoosePlanOrNewKey = "ChoosePlanOrNewKey";
            public const string RenewOnWeb = "RenewOnWeb";
            public const string AskOrgForKey = "AskOrgForKey";
            // Success panel
            public const string SubscriptionActiveTitle = "SubscriptionActiveTitle";
            public const string ThanksForSubscribing = "ThanksForSubscribing";
            public const string SubscriptionActiveBody = "SubscriptionActiveBody";
            public const string SubscriptionNewBody = "SubscriptionNewBody";
            public const string LicenseActiveTitle = "LicenseActiveTitle";
            public const string KeyRedeemedTitle = "KeyRedeemedTitle";
            public const string LicenseActiveBody = "LicenseActiveBody";
            public const string LicenseNewBody = "LicenseNewBody";
            public const string LicenseActivated = "LicenseActivated";
            // QR scan HUD and scanner status lines
            public const string ScanTitle = "ScanTitle";
            public const string ScanSubtitle = "ScanSubtitle";
            public const string ScanCancelled = "ScanCancelled";
            public const string StartingCamera = "StartingCamera";
            public const string QrDetected = "QrDetected";
            // Appended after a terminal scanner error
            public const string EnterKeyManually = "EnterKeyManually";
            public const string RequestingCamera = "RequestingCamera";
            public const string CameraDenied = "CameraDenied";
            public const string QrDecoderMissing = "QrDecoderMissing";
            public const string NoCamera = "NoCamera";
            public const string PointQr = "PointQr";
            public const string CameraNotShared = "CameraNotShared";
            public const string NoQrDetected = "NoQrDetected";
            // Direct (non-store) APKs: organisation hint / purchase CTA column
            public const string OrgTitle = "OrgTitle";
            public const string OrgBody = "OrgBody";
            public const string BuyTitle = "BuyTitle";
            public const string SpecialOffer = "SpecialOffer";
            // {0} = promo code (rich text bold kept in every language)
            public const string UseCode = "UseCode";
            public const string ScanPhonePromo = "ScanPhonePromo";
            public const string ScanPhoneOrVisit = "ScanPhoneOrVisit";
            public const string VisitToPurchase = "VisitToPurchase";
            // Head-locked countdown; {0} = mm:ss
            public const string DemoHud = "DemoHud";
        }

        private static readonly Dictionary<string, Dictionary<string, string>> UiTable =
            new Dictionary<string, Dictionary<string, string>>
        {
            // Welcome panel
            { Key.WelcomeSubtitle, new Dictionary<string, string> {
                { "en", "Select an option to continue" },
                { "es", "Elige una opción para continuar" },
                { "fr", "Choisissez une option pour continuer" },
                { "de", "Wählen Sie eine Option, um fortzufahren" },
                { "it", "Scegli un'opzione per continuare" },
                { "pt", "Escolha uma opção para continuar" },
                { "ru", "Выберите вариант, чтобы продолжить" },
                { "zh", "请选择一个选项以继续" },
                { "hi", "जारी रखने के लिए एक विकल्प चुनें" },
                { "fi", "Valitse vaihtoehto jatkaaksesi" },
                { "sv", "Välj ett alternativ för att fortsätta" },
                { "nl", "Kies een optie om verder te gaan" },
            } },
            { Key.StartDemo, new Dictionary<string, string> {
                { "en", "Start Free Demo" },
                { "es", "Iniciar demo gratuita" },
                { "fr", "Démarrer la démo gratuite" },
                { "de", "Kostenlose Demo starten" },
                { "it", "Avvia la demo gratuita" },
                { "pt", "Iniciar demo gratuita" },
                { "ru", "Начать бесплатную демоверсию" },
                { "zh", "开始免费试用" },
                { "hi", "मुफ़्त डेमो शुरू करें" },
                { "fi", "Aloita ilmainen demo" },
                { "sv", "Starta gratis demo" },
                { "nl", "Probeer de demo" },
            } },
            { Key.EnterKey, new Dictionary<string, string> {
                { "en", "Enter License Key" },
                { "es", "Introducir clave de licencia" },
                { "fr", "Saisir une clé de licence" },
                { "de", "Lizenzschlüssel eingeben" },
                { "it", "Inserisci la chiave di licenza" },
                { "pt", "Inserir chave de licença" },
                { "ru", "Ввести лицензионный ключ" },
                { "zh", "输入许可证密钥" },
                { "hi", "लाइसेंस कुंजी दर्ज करें" },
                { "fi", "Syötä lisenssiavain" },
                { "sv", "Ange licensnyckel" },
                { "nl", "Licentiecode invoeren" },
            } },
            { Key.ScanQr, new Dictionary<string, string> {
                { "en", "Scan QR" },
                { "es", "Escanear QR" },
                { "fr", "Scanner un QR" },
                { "de", "QR-Code scannen" },
                { "it", "Scansiona QR" },
                { "pt", "Escanear QR" },
                { "ru", "Сканировать QR" },
                { "zh", "扫描二维码" },
                { "hi", "QR स्कैन करें" },
                { "fi", "Skannaa QR" },
                { "sv", "Skanna QR" },
                { "nl", "QR scannen" },
            } },
            // {0} = time remaining, {1} = demo length (both from ShortDuration)
            { Key.WelcomeDemoLine, new Dictionary<string, string> {
                { "en", "Free demo: {0} remaining of {1}" },
                { "es", "Demo gratuita: quedan {0} de {1}" },
                { "fr", "Démo gratuite : il reste {0} sur {1}" },
                { "de", "Kostenlose Demo: noch {0} von {1}" },
                { "it", "Demo gratuita: {0} rimanenti su {1}" },
                { "pt", "Demo gratuita: restam {0} de {1}" },
                { "ru", "Бесплатная демоверсия: осталось {0} из {1}" },
                { "zh", "免费试用：剩余 {0}，共 {1}" },
                { "hi", "मुफ़्त डेमो: {1} में से {0} बाकी" },
                { "fi", "Ilmainen demo: jäljellä {0} / {1}" },
                { "sv", "Gratis demo: {0} kvar av {1}" },
                { "nl", "Gratis demo: nog {0} van {1}" },
            } },
            // {0} = whole minutes
            { Key.MinutesFormat, new Dictionary<string, string> {
                { "en", "{0} min" },
                { "es", "{0} min" },
                { "fr", "{0} min" },
                { "de", "{0} Min." },
                { "it", "{0} min" },
                { "pt", "{0} min" },
                { "ru", "{0} мин" },
                { "zh", "{0}分钟" },
                { "hi", "{0} मिनट" },
                { "fi", "{0} min" },
                { "sv", "{0} min" },
                { "nl", "{0} min" },
            } },
            // {0} = hours ("1" or "1.5")
            { Key.HoursFormat, new Dictionary<string, string> {
                { "en", "{0} h" },
                { "es", "{0} h" },
                { "fr", "{0} h" },
                { "de", "{0} Std." },
                { "it", "{0} h" },
                { "pt", "{0} h" },
                { "ru", "{0} ч" },
                { "zh", "{0}小时" },
                { "hi", "{0} घंटे" },
                { "fi", "{0} h" },
                { "sv", "{0} h" },
                { "nl", "{0} uur" },
            } },
            // Key-entry panel
            { Key.KeyHint, new Dictionary<string, string> {
                { "en", "Enter your license key (format XXXX-XXXX-XXXX-XXXX)" },
                { "es", "Introduce tu clave de licencia (formato XXXX-XXXX-XXXX-XXXX)" },
                { "fr", "Saisissez votre clé de licence (format XXXX-XXXX-XXXX-XXXX)" },
                { "de", "Geben Sie Ihren Lizenzschlüssel ein (Format XXXX-XXXX-XXXX-XXXX)" },
                { "it", "Inserisci la tua chiave di licenza (formato XXXX-XXXX-XXXX-XXXX)" },
                { "pt", "Insira a sua chave de licença (formato XXXX-XXXX-XXXX-XXXX)" },
                { "ru", "Введите лицензионный ключ (формат XXXX-XXXX-XXXX-XXXX)" },
                { "zh", "请输入您的许可证密钥（格式 XXXX-XXXX-XXXX-XXXX）" },
                { "hi", "अपनी लाइसेंस कुंजी दर्ज करें (प्रारूप XXXX-XXXX-XXXX-XXXX)" },
                { "fi", "Syötä lisenssiavaimesi (muoto XXXX-XXXX-XXXX-XXXX)" },
                { "sv", "Ange din licensnyckel (format XXXX-XXXX-XXXX-XXXX)" },
                { "nl", "Voer je licentiecode in (formaat XXXX-XXXX-XXXX-XXXX)" },
            } },
            { Key.Activate, new Dictionary<string, string> {
                { "en", "Activate License" },
                { "es", "Activar licencia" },
                { "fr", "Activer la licence" },
                { "de", "Lizenz aktivieren" },
                { "it", "Attiva licenza" },
                { "pt", "Ativar licença" },
                { "ru", "Активировать лицензию" },
                { "zh", "激活许可证" },
                { "hi", "लाइसेंस सक्रिय करें" },
                { "fi", "Aktivoi lisenssi" },
                { "sv", "Aktivera licens" },
                { "nl", "Licentie activeren" },
            } },
            { Key.Validating, new Dictionary<string, string> {
                { "en", "Validating..." },
                { "es", "Validando..." },
                { "fr", "Vérification..." },
                { "de", "Wird geprüft..." },
                { "it", "Verifica in corso..." },
                { "pt", "Validando..." },
                { "ru", "Проверка..." },
                { "zh", "正在验证..." },
                { "hi", "जाँच हो रही है..." },
                { "fi", "Tarkistetaan..." },
                { "sv", "Verifierar..." },
                { "nl", "Controleren..." },
            } },
            { Key.Cancel, new Dictionary<string, string> {
                { "en", "Cancel" },
                { "es", "Cancelar" },
                { "fr", "Annuler" },
                { "de", "Abbrechen" },
                { "it", "Annulla" },
                { "pt", "Cancelar" },
                { "ru", "Отмена" },
                { "zh", "取消" },
                { "hi", "रद्द करें" },
                { "fi", "Peruuta" },
                { "sv", "Avbryt" },
                { "nl", "Annuleren" },
            } },
            { Key.EnterFullKey, new Dictionary<string, string> {
                { "en", "Enter the full key (XXXX-XXXX-XXXX-XXXX)" },
                { "es", "Introduce la clave completa (XXXX-XXXX-XXXX-XXXX)" },
                { "fr", "Saisissez la clé complète (XXXX-XXXX-XXXX-XXXX)" },
                { "de", "Geben Sie den vollständigen Schlüssel ein (XXXX-XXXX-XXXX-XXXX)" },
                { "it", "Inserisci la chiave completa (XXXX-XXXX-XXXX-XXXX)" },
                { "pt", "Insira a chave completa (XXXX-XXXX-XXXX-XXXX)" },
                { "ru", "Введите ключ полностью (XXXX-XXXX-XXXX-XXXX)" },
                { "zh", "请输入完整密钥（XXXX-XXXX-XXXX-XXXX）" },
                { "hi", "पूरी कुंजी दर्ज करें (XXXX-XXXX-XXXX-XXXX)" },
                { "fi", "Syötä koko avain (XXXX-XXXX-XXXX-XXXX)" },
                { "sv", "Ange hela nyckeln (XXXX-XXXX-XXXX-XXXX)" },
                { "nl", "Voer de volledige code in (XXXX-XXXX-XXXX-XXXX)" },
            } },
            { Key.EnterKeyPrompt, new Dictionary<string, string> {
                { "en", "Please enter a license key." },
                { "es", "Introduce una clave de licencia." },
                { "fr", "Veuillez saisir une clé de licence." },
                { "de", "Bitte geben Sie einen Lizenzschlüssel ein." },
                { "it", "Inserisci una chiave di licenza." },
                { "pt", "Insira uma chave de licença." },
                { "ru", "Введите лицензионный ключ." },
                { "zh", "请输入许可证密钥。" },
                { "hi", "कृपया लाइसेंस कुंजी दर्ज करें।" },
                { "fi", "Syötä lisenssiavain." },
                { "sv", "Ange en licensnyckel." },
                { "nl", "Voer een licentiecode in." },
            } },
            { Key.ValidationError, new Dictionary<string, string> {
                { "en", "Error validating the license." },
                { "es", "Error al validar la licencia." },
                { "fr", "Erreur lors de la vérification de la licence." },
                { "de", "Fehler beim Prüfen der Lizenz." },
                { "it", "Errore durante la verifica della licenza." },
                { "pt", "Erro ao validar a licença." },
                { "ru", "Ошибка при проверке лицензии." },
                { "zh", "验证许可证时出错。" },
                { "hi", "लाइसेंस की जाँच में त्रुटि।" },
                { "fi", "Virhe lisenssin tarkistuksessa." },
                { "sv", "Fel vid verifiering av licensen." },
                { "nl", "Fout bij het controleren van de licentie." },
            } },
            // SupabaseClient fallback when the server sends no error text
            { Key.InvalidKey, new Dictionary<string, string> {
                { "en", "Invalid or inactive license key." },
                { "es", "Clave de licencia no válida o inactiva." },
                { "fr", "Clé de licence invalide ou inactive." },
                { "de", "Ungültiger oder inaktiver Lizenzschlüssel." },
                { "it", "Chiave di licenza non valida o inattiva." },
                { "pt", "Chave de licença inválida ou inativa." },
                { "ru", "Недействительный или неактивный лицензионный ключ." },
                { "zh", "许可证密钥无效或未激活。" },
                { "hi", "अमान्य या निष्क्रिय लाइसेंस कुंजी।" },
                { "fi", "Virheellinen tai passiivinen lisenssiavain." },
                { "sv", "Ogiltig eller inaktiv licensnyckel." },
                { "nl", "Ongeldige of inactieve licentiecode." },
            } },
            // {0} = server status code (revoked, suspended…)
            { Key.LicenseNotActive, new Dictionary<string, string> {
                { "en", "This license is not active ({0}). Ask your organisation for a new key." },
                { "es", "Esta licencia no está activa ({0}). Pide una clave nueva a tu organización." },
                { "fr", "Cette licence n'est pas active ({0}). Demandez une nouvelle clé à votre organisation." },
                { "de", "Diese Lizenz ist nicht aktiv ({0}). Bitten Sie Ihre Organisation um einen neuen Schlüssel." },
                { "it", "Questa licenza non è attiva ({0}). Chiedi una nuova chiave alla tua organizzazione." },
                { "pt", "Esta licença não está ativa ({0}). Peça uma nova chave à sua organização." },
                { "ru", "Эта лицензия не действует ({0}). Запросите новый ключ у своей организации." },
                { "zh", "此许可证未激活（{0}）。请向您的机构索取新密钥。" },
                { "hi", "यह लाइसेंस सक्रिय नहीं है ({0})। अपने संगठन से नई कुंजी माँगें।" },
                { "fi", "Tämä lisenssi ei ole voimassa ({0}). Pyydä organisaatioltasi uusi avain." },
                { "sv", "Den här licensen är inte aktiv ({0}). Be din organisation om en ny nyckel." },
                { "nl", "Deze licentie is niet actief ({0}). Vraag je organisatie om een nieuwe code." },
            } },
            // {0} = transport error from UnityWebRequest
            { Key.ConnectionError, new Dictionary<string, string> {
                { "en", "Connection error: {0}" },
                { "es", "Error de conexión: {0}" },
                { "fr", "Erreur de connexion : {0}" },
                { "de", "Verbindungsfehler: {0}" },
                { "it", "Errore di connessione: {0}" },
                { "pt", "Erro de conexão: {0}" },
                { "ru", "Ошибка подключения: {0}" },
                { "zh", "连接错误：{0}" },
                { "hi", "कनेक्शन त्रुटि: {0}" },
                { "fi", "Yhteysvirhe: {0}" },
                { "sv", "Anslutningsfel: {0}" },
                { "nl", "Verbindingsfout: {0}" },
            } },
            // {0} = parser exception message
            { Key.ResponseError, new Dictionary<string, string> {
                { "en", "Error processing the response: {0}" },
                { "es", "Error al procesar la respuesta: {0}" },
                { "fr", "Erreur de traitement de la réponse : {0}" },
                { "de", "Fehler beim Verarbeiten der Antwort: {0}" },
                { "it", "Errore nell'elaborazione della risposta: {0}" },
                { "pt", "Erro ao processar a resposta: {0}" },
                { "ru", "Ошибка обработки ответа: {0}" },
                { "zh", "处理响应时出错：{0}" },
                { "hi", "प्रतिक्रिया संसाधित करने में त्रुटि: {0}" },
                { "fi", "Virhe vastauksen käsittelyssä: {0}" },
                { "sv", "Fel vid hantering av svaret: {0}" },
                { "nl", "Fout bij het verwerken van het antwoord: {0}" },
            } },
            { Key.ClockTampered, new Dictionary<string, string> {
                { "en", "System clock has been tampered with. Connect to the internet to verify." },
                { "es", "Se ha manipulado el reloj del sistema. Conéctate a internet para verificar." },
                { "fr", "L'horloge du système a été modifiée. Connectez-vous à Internet pour vérifier." },
                { "de", "Die Systemuhr wurde manipuliert. Stellen Sie eine Internetverbindung her, um zu überprüfen." },
                { "it", "L'orologio di sistema è stato manomesso. Connettiti a internet per verificare." },
                { "pt", "O relógio do sistema foi alterado. Conecte-se à internet para verificar." },
                { "ru", "Системные часы были изменены. Подключитесь к интернету для проверки." },
                { "zh", "系统时钟已被篡改。请连接网络进行验证。" },
                { "hi", "सिस्टम घड़ी से छेड़छाड़ की गई है। सत्यापन के लिए इंटरनेट से जुड़ें।" },
                { "fi", "Järjestelmän kelloa on muutettu. Yhdistä internetiin tarkistusta varten." },
                { "sv", "Systemklockan har manipulerats. Anslut till internet för att verifiera." },
                { "nl", "De systeemklok is aangepast. Maak verbinding met internet om te controleren." },
            } },
            // Demo-expired panel
            { Key.DemoExpiredTitle, new Dictionary<string, string> {
                { "en", "Demo Expired" },
                { "es", "Demo finalizada" },
                { "fr", "Démo terminée" },
                { "de", "Demo abgelaufen" },
                { "it", "Demo terminata" },
                { "pt", "Demo encerrada" },
                { "ru", "Демоверсия завершена" },
                { "zh", "试用已结束" },
                { "hi", "डेमो समाप्त" },
                { "fi", "Demo päättynyt" },
                { "sv", "Demon har gått ut" },
                { "nl", "Demo afgelopen" },
            } },
            // {0} = app display name
            { Key.DemoExpiredLine, new Dictionary<string, string> {
                { "en", "Your free trial of {0} has ended." },
                { "es", "Tu prueba gratuita de {0} ha terminado." },
                { "fr", "Votre essai gratuit de {0} est terminé." },
                { "de", "Ihre kostenlose Testphase von {0} ist beendet." },
                { "it", "La tua prova gratuita di {0} è terminata." },
                { "pt", "O seu período de teste gratuito de {0} terminou." },
                { "ru", "Бесплатный пробный период {0} завершён." },
                { "zh", "您的 {0} 免费试用已结束。" },
                { "hi", "{0} का आपका मुफ़्त ट्रायल समाप्त हो गया है।" },
                { "fi", "Ilmainen kokeilujaksosi ({0}) on päättynyt." },
                { "sv", "Din gratis provperiod av {0} har tagit slut." },
                { "nl", "Je gratis proefperiode van {0} is afgelopen." },
            } },
            { Key.HaveKeyTitle, new Dictionary<string, string> {
                { "en", "Already have a license key?" },
                { "es", "¿Ya tienes una clave de licencia?" },
                { "fr", "Vous avez déjà une clé de licence ?" },
                { "de", "Haben Sie schon einen Lizenzschlüssel?" },
                { "it", "Hai già una chiave di licenza?" },
                { "pt", "Já tem uma chave de licença?" },
                { "ru", "Уже есть лицензионный ключ?" },
                { "zh", "已有许可证密钥？" },
                { "hi", "क्या आपके पास पहले से लाइसेंस कुंजी है?" },
                { "fi", "Onko sinulla jo lisenssiavain?" },
                { "sv", "Har du redan en licensnyckel?" },
                { "nl", "Heb je al een licentiecode?" },
            } },
            // License-expired panel, key column
            { Key.EnterKeyTitle, new Dictionary<string, string> {
                { "en", "Enter a license key" },
                { "es", "Introduce una clave de licencia" },
                { "fr", "Saisissez une clé de licence" },
                { "de", "Lizenzschlüssel eingeben" },
                { "it", "Inserisci una chiave di licenza" },
                { "pt", "Insira uma chave de licença" },
                { "ru", "Введите лицензионный ключ" },
                { "zh", "输入许可证密钥" },
                { "hi", "लाइसेंस कुंजी दर्ज करें" },
                { "fi", "Syötä lisenssiavain" },
                { "sv", "Ange en licensnyckel" },
                { "nl", "Voer een licentiecode in" },
            } },
            // Store plans column
            { Key.UnlockTitle, new Dictionary<string, string> {
                { "en", "Unlock the full course" },
                { "es", "Desbloquea el curso completo" },
                { "fr", "Débloquez le cours complet" },
                { "de", "Vollständigen Kurs freischalten" },
                { "it", "Sblocca il corso completo" },
                { "pt", "Desbloqueie o curso completo" },
                { "ru", "Откройте полный курс" },
                { "zh", "解锁完整课程" },
                { "hi", "पूरा कोर्स अनलॉक करें" },
                { "fi", "Avaa koko kurssi" },
                { "sv", "Lås upp hela kursen" },
                { "nl", "Ontgrendel de volledige cursus" },
            } },
            { Key.LoadingPlans, new Dictionary<string, string> {
                { "en", "Loading plans…" },
                { "es", "Cargando planes…" },
                { "fr", "Chargement des formules…" },
                { "de", "Tarife werden geladen…" },
                { "it", "Caricamento dei piani…" },
                { "pt", "Carregando planos…" },
                { "ru", "Загрузка тарифов…" },
                { "zh", "正在加载套餐…" },
                { "hi", "प्लान लोड हो रहे हैं…" },
                { "fi", "Ladataan tilausvaihtoehtoja…" },
                { "sv", "Laddar planer…" },
                { "nl", "Abonnementen laden…" },
            } },
            { Key.SubscriptionFoundVerifying, new Dictionary<string, string> {
                { "en", "An active subscription was found on this Meta account. Verifying…" },
                { "es", "Se encontró una suscripción activa en esta cuenta de Meta. Verificando…" },
                { "fr", "Un abonnement actif a été trouvé sur ce compte Meta. Vérification…" },
                { "de", "Für dieses Meta-Konto wurde ein aktives Abonnement gefunden. Wird überprüft…" },
                { "it", "È stato trovato un abbonamento attivo su questo account Meta. Verifica in corso…" },
                { "pt", "Foi encontrada uma assinatura ativa nesta conta Meta. Verificando…" },
                { "ru", "На этом аккаунте Meta найдена активная подписка. Проверка…" },
                { "zh", "此 Meta 账户已有有效订阅。正在验证…" },
                { "hi", "इस Meta खाते पर एक सक्रिय सदस्यता मिली। जाँच हो रही है…" },
                { "fi", "Tällä Meta-tilillä on aktiivinen tilaus. Tarkistetaan…" },
                { "sv", "En aktiv prenumeration hittades på detta Meta-konto. Verifierar…" },
                { "nl", "Er is een actief abonnement gevonden op dit Meta-account. Controleren…" },
            } },
            { Key.SubscriptionVerified, new Dictionary<string, string> {
                { "en", "Subscription verified" },
                { "es", "Suscripción verificada" },
                { "fr", "Abonnement vérifié" },
                { "de", "Abonnement bestätigt" },
                { "it", "Abbonamento verificato" },
                { "pt", "Assinatura verificada" },
                { "ru", "Подписка подтверждена" },
                { "zh", "订阅已验证" },
                { "hi", "सदस्यता सत्यापित" },
                { "fi", "Tilaus vahvistettu" },
                { "sv", "Prenumeration verifierad" },
                { "nl", "Abonnement bevestigd" },
            } },
            { Key.BillingNote, new Dictionary<string, string> {
                { "en", "Billed through your Meta account. Manage or cancel anytime in your Meta settings." },
                { "es", "Se cobra a través de tu cuenta de Meta. Gestiona o cancela cuando quieras en los ajustes de Meta." },
                { "fr", "Facturé via votre compte Meta. Gérez ou annulez à tout moment dans vos paramètres Meta." },
                { "de", "Abrechnung über Ihr Meta-Konto. Jederzeit in den Meta-Einstellungen verwalten oder kündigen." },
                { "it", "Addebitato tramite il tuo account Meta. Gestisci o annulla in qualsiasi momento nelle impostazioni Meta." },
                { "pt", "Cobrado pela sua conta Meta. Gerencie ou cancele a qualquer momento nas configurações da Meta." },
                { "ru", "Оплата через ваш аккаунт Meta. Управляйте подпиской или отмените её в любое время в настройках Meta." },
                { "zh", "通过您的 Meta 账户扣费。可随时在 Meta 设置中管理或取消。" },
                { "hi", "आपके Meta खाते से बिल किया जाता है। Meta सेटिंग्स में कभी भी प्रबंधित या रद्द करें।" },
                { "fi", "Veloitetaan Meta-tililtäsi. Hallitse tai peru milloin tahansa Meta-asetuksissa." },
                { "sv", "Debiteras via ditt Meta-konto. Hantera eller avsluta när som helst i dina Meta-inställningar." },
                { "nl", "Wordt afgerekend via je Meta-account. Beheer of annuleer het wanneer je wilt in je Meta-instellingen." },
            } },
            { Key.OpeningCheckout, new Dictionary<string, string> {
                { "en", "Opening Meta checkout…" },
                { "es", "Abriendo el pago de Meta…" },
                { "fr", "Ouverture du paiement Meta…" },
                { "de", "Meta-Kasse wird geöffnet…" },
                { "it", "Apertura del pagamento Meta…" },
                { "pt", "Abrindo o checkout da Meta…" },
                { "ru", "Открывается оплата Meta…" },
                { "zh", "正在打开 Meta 结账…" },
                { "hi", "Meta चेकआउट खुल रहा है…" },
                { "fi", "Avataan Meta-kassaa…" },
                { "sv", "Öppnar Meta-kassan…" },
                { "nl", "Meta-betaling openen…" },
            } },
            { Key.SubscriptionActivated, new Dictionary<string, string> {
                { "en", "Subscription activated" },
                { "es", "Suscripción activada" },
                { "fr", "Abonnement activé" },
                { "de", "Abonnement aktiviert" },
                { "it", "Abbonamento attivato" },
                { "pt", "Assinatura ativada" },
                { "ru", "Подписка активирована" },
                { "zh", "订阅已激活" },
                { "hi", "सदस्यता सक्रिय" },
                { "fi", "Tilaus aktivoitu" },
                { "sv", "Prenumeration aktiverad" },
                { "nl", "Abonnement geactiveerd" },
            } },
            { Key.PlansUnavailable, new Dictionary<string, string> {
                { "en", "Plans are not available right now. Please try again later." },
                { "es", "Los planes no están disponibles ahora mismo. Inténtalo más tarde." },
                { "fr", "Les formules ne sont pas disponibles pour le moment. Réessayez plus tard." },
                { "de", "Die Tarife sind derzeit nicht verfügbar. Bitte später erneut versuchen." },
                { "it", "I piani non sono disponibili al momento. Riprova più tardi." },
                { "pt", "Os planos não estão disponíveis no momento. Tente novamente mais tarde." },
                { "ru", "Тарифы сейчас недоступны. Попробуйте позже." },
                { "zh", "套餐暂时不可用，请稍后再试。" },
                { "hi", "प्लान अभी उपलब्ध नहीं हैं। कृपया बाद में पुनः प्रयास करें।" },
                { "fi", "Tilausvaihtoehdot eivät ole juuri nyt saatavilla. Yritä myöhemmin uudelleen." },
                { "sv", "Planerna är inte tillgängliga just nu. Försök igen senare." },
                { "nl", "De abonnementen zijn nu niet beschikbaar. Probeer het later opnieuw." },
            } },
            { Key.StoreUnavailable, new Dictionary<string, string> {
                { "en", "The store is not available right now." },
                { "es", "La tienda no está disponible ahora mismo." },
                { "fr", "La boutique n'est pas disponible pour le moment." },
                { "de", "Der Store ist derzeit nicht verfügbar." },
                { "it", "Lo store non è disponibile al momento." },
                { "pt", "A loja não está disponível no momento." },
                { "ru", "Магазин сейчас недоступен." },
                { "zh", "商店暂时不可用。" },
                { "hi", "स्टोर अभी उपलब्ध नहीं है।" },
                { "fi", "Kauppa ei ole juuri nyt käytettävissä." },
                { "sv", "Butiken är inte tillgänglig just nu." },
                { "nl", "De store is nu niet beschikbaar." },
            } },
            { Key.CheckoutCancelled, new Dictionary<string, string> {
                { "en", "Checkout cancelled." },
                { "es", "Pago cancelado." },
                { "fr", "Paiement annulé." },
                { "de", "Kauf abgebrochen." },
                { "it", "Pagamento annullato." },
                { "pt", "Compra cancelada." },
                { "ru", "Оплата отменена." },
                { "zh", "已取消结账。" },
                { "hi", "चेकआउट रद्द किया गया।" },
                { "fi", "Osto peruutettu." },
                { "sv", "Köpet avbröts." },
                { "nl", "Betaling geannuleerd." },
            } },
            // {0} = store error text
            { Key.CheckoutFailed, new Dictionary<string, string> {
                { "en", "Checkout failed: {0}" },
                { "es", "Error en el pago: {0}" },
                { "fr", "Échec du paiement : {0}" },
                { "de", "Kauf fehlgeschlagen: {0}" },
                { "it", "Pagamento non riuscito: {0}" },
                { "pt", "Falha na compra: {0}" },
                { "ru", "Ошибка оплаты: {0}" },
                { "zh", "结账失败：{0}" },
                { "hi", "चेकआउट विफल: {0}" },
                { "fi", "Osto epäonnistui: {0}" },
                { "sv", "Köpet misslyckades: {0}" },
                { "nl", "Betaling mislukt: {0}" },
            } },
            { Key.IdentifyFailed, new Dictionary<string, string> {
                { "en", "Could not identify your Meta account. Check your connection and try again." },
                { "es", "No se pudo identificar tu cuenta de Meta. Comprueba la conexión e inténtalo de nuevo." },
                { "fr", "Impossible d'identifier votre compte Meta. Vérifiez votre connexion et réessayez." },
                { "de", "Ihr Meta-Konto konnte nicht ermittelt werden. Prüfen Sie Ihre Verbindung und versuchen Sie es erneut." },
                { "it", "Impossibile identificare il tuo account Meta. Controlla la connessione e riprova." },
                { "pt", "Não foi possível identificar a sua conta Meta. Verifique a conexão e tente novamente." },
                { "ru", "Не удалось определить ваш аккаунт Meta. Проверьте подключение и попробуйте снова." },
                { "zh", "无法识别您的 Meta 账户。请检查网络连接后重试。" },
                { "hi", "आपके Meta खाते की पहचान नहीं हो सकी। कनेक्शन जाँचें और फिर से प्रयास करें।" },
                { "fi", "Meta-tiliäsi ei voitu tunnistaa. Tarkista yhteys ja yritä uudelleen." },
                { "sv", "Kunde inte identifiera ditt Meta-konto. Kontrollera anslutningen och försök igen." },
                { "nl", "Je Meta-account kon niet worden herkend. Controleer je verbinding en probeer het opnieuw." },
            } },
            { Key.VerifyFailed, new Dictionary<string, string> {
                { "en", "Could not verify the subscription. Check your connection and try again." },
                { "es", "No se pudo verificar la suscripción. Comprueba la conexión e inténtalo de nuevo." },
                { "fr", "Impossible de vérifier l'abonnement. Vérifiez votre connexion et réessayez." },
                { "de", "Das Abonnement konnte nicht überprüft werden. Prüfen Sie Ihre Verbindung und versuchen Sie es erneut." },
                { "it", "Impossibile verificare l'abbonamento. Controlla la connessione e riprova." },
                { "pt", "Não foi possível verificar a assinatura. Verifique a conexão e tente novamente." },
                { "ru", "Не удалось проверить подписку. Проверьте подключение и попробуйте снова." },
                { "zh", "无法验证订阅。请检查网络连接后重试。" },
                { "hi", "सदस्यता सत्यापित नहीं हो सकी। कनेक्शन जाँचें और फिर से प्रयास करें।" },
                { "fi", "Tilausta ei voitu vahvistaa. Tarkista yhteys ja yritä uudelleen." },
                { "sv", "Kunde inte verifiera prenumerationen. Kontrollera anslutningen och försök igen." },
                { "nl", "Het abonnement kon niet worden gecontroleerd. Controleer je verbinding en probeer het opnieuw." },
            } },
            { Key.NoSubscription, new Dictionary<string, string> {
                { "en", "No active subscription was found for this Meta account." },
                { "es", "No se encontró ninguna suscripción activa en esta cuenta de Meta." },
                { "fr", "Aucun abonnement actif n'a été trouvé pour ce compte Meta." },
                { "de", "Für dieses Meta-Konto wurde kein aktives Abonnement gefunden." },
                { "it", "Nessun abbonamento attivo trovato per questo account Meta." },
                { "pt", "Nenhuma assinatura ativa foi encontrada nesta conta Meta." },
                { "ru", "Для этого аккаунта Meta активная подписка не найдена." },
                { "zh", "未找到此 Meta 账户的有效订阅。" },
                { "hi", "इस Meta खाते के लिए कोई सक्रिय सदस्यता नहीं मिली।" },
                { "fi", "Tälle Meta-tilille ei löytynyt aktiivista tilausta." },
                { "sv", "Ingen aktiv prenumeration hittades för detta Meta-konto." },
                { "nl", "Er is geen actief abonnement gevonden voor dit Meta-account." },
            } },
            { Key.InternetNeeded, new Dictionary<string, string> {
                { "en", "An internet connection is needed to verify your subscription." },
                { "es", "Se necesita conexión a internet para verificar tu suscripción." },
                { "fr", "Une connexion Internet est nécessaire pour vérifier votre abonnement." },
                { "de", "Zum Überprüfen Ihres Abonnements ist eine Internetverbindung erforderlich." },
                { "it", "Serve una connessione a internet per verificare il tuo abbonamento." },
                { "pt", "É necessária uma conexão com a internet para verificar a sua assinatura." },
                { "ru", "Для проверки подписки необходимо подключение к интернету." },
                { "zh", "需要连接网络才能验证您的订阅。" },
                { "hi", "आपकी सदस्यता सत्यापित करने के लिए इंटरनेट कनेक्शन ज़रूरी है।" },
                { "fi", "Tilauksesi vahvistamiseen tarvitaan internetyhteys." },
                { "sv", "En internetanslutning krävs för att verifiera din prenumeration." },
                { "nl", "Er is een internetverbinding nodig om je abonnement te controleren." },
            } },
            // Store build whose app-ownership check failed (the app quits shortly after)
            { Key.EntitlementBlocked, new Dictionary<string, string> {
                { "en", "This app is not available for the signed-in Meta account.\nPlease install it from the Meta Horizon Store with the account that owns it." },
                { "es", "Esta app no está disponible para la cuenta de Meta con la que has iniciado sesión.\nInstálala desde la Meta Horizon Store con la cuenta que la compró." },
                { "fr", "Cette application n'est pas disponible pour le compte Meta connecté.\nInstallez-la depuis le Meta Horizon Store avec le compte qui la possède." },
                { "de", "Diese App ist für das angemeldete Meta-Konto nicht verfügbar.\nBitte installieren Sie sie aus dem Meta Horizon Store mit dem Konto, dem sie gehört." },
                { "it", "Questa app non è disponibile per l'account Meta con cui hai effettuato l'accesso.\nInstallala dal Meta Horizon Store con l'account che la possiede." },
                { "pt", "Este app não está disponível para a conta Meta conectada.\nInstale-o pela Meta Horizon Store com a conta que o possui." },
                { "ru", "Это приложение недоступно для текущего аккаунта Meta.\nУстановите его из Meta Horizon Store с аккаунта, которому оно принадлежит." },
                { "zh", "此应用对当前登录的 Meta 账户不可用。\n请使用拥有该应用的账户从 Meta Horizon Store 安装。" },
                { "hi", "यह ऐप साइन-इन किए गए Meta खाते के लिए उपलब्ध नहीं है।\nकृपया इसे उस खाते से Meta Horizon Store से इंस्टॉल करें जिसके पास यह है।" },
                { "fi", "Tämä sovellus ei ole käytettävissä kirjautuneella Meta-tilillä.\nAsenna se Meta Horizon Storesta sillä tilillä, joka omistaa sen." },
                { "sv", "Den här appen är inte tillgänglig för det inloggade Meta-kontot.\nInstallera den från Meta Horizon Store med kontot som äger den." },
                { "nl", "Deze app is niet beschikbaar voor het ingelogde Meta-account.\nInstalleer hem via de Meta Horizon Store met het account dat de app bezit." },
            } },
            // License-expired panel
            { Key.LicenseExpiredTitle, new Dictionary<string, string> {
                { "en", "License Expired" },
                { "es", "Licencia caducada" },
                { "fr", "Licence expirée" },
                { "de", "Lizenz abgelaufen" },
                { "it", "Licenza scaduta" },
                { "pt", "Licença expirada" },
                { "ru", "Лицензия истекла" },
                { "zh", "许可证已过期" },
                { "hi", "लाइसेंस समाप्त" },
                { "fi", "Lisenssi vanhentunut" },
                { "sv", "Licensen har gått ut" },
                { "nl", "Licentie verlopen" },
            } },
            // {0} = app display name
            { Key.StoreExpiredLine, new Dictionary<string, string> {
                { "en", "Your subscription to {0} has ended or needs to be verified again." },
                { "es", "Tu suscripción a {0} ha terminado o debe verificarse de nuevo." },
                { "fr", "Votre abonnement à {0} est terminé ou doit être vérifié à nouveau." },
                { "de", "Ihr Abonnement von {0} ist beendet oder muss erneut überprüft werden." },
                { "it", "Il tuo abbonamento a {0} è terminato o deve essere verificato di nuovo." },
                { "pt", "A sua assinatura de {0} terminou ou precisa ser verificada novamente." },
                { "ru", "Ваша подписка на {0} закончилась или требует повторной проверки." },
                { "zh", "您对 {0} 的订阅已结束或需要重新验证。" },
                { "hi", "{0} की आपकी सदस्यता समाप्त हो गई है या फिर से सत्यापित करनी होगी।" },
                { "fi", "Tilauksesi ({0}) on päättynyt tai se on vahvistettava uudelleen." },
                { "sv", "Din prenumeration på {0} har upphört eller måste verifieras igen." },
                { "nl", "Je abonnement op {0} is afgelopen of moet opnieuw worden gecontroleerd." },
            } },
            { Key.ChoosePlanOrKey, new Dictionary<string, string> {
                { "en", "Choose a plan to continue, or enter a license key." },
                { "es", "Elige un plan para continuar o introduce una clave de licencia." },
                { "fr", "Choisissez une formule pour continuer, ou saisissez une clé de licence." },
                { "de", "Wählen Sie einen Tarif, um fortzufahren, oder geben Sie einen Lizenzschlüssel ein." },
                { "it", "Scegli un piano per continuare oppure inserisci una chiave di licenza." },
                { "pt", "Escolha um plano para continuar ou insira uma chave de licença." },
                { "ru", "Выберите тариф, чтобы продолжить, или введите лицензионный ключ." },
                { "zh", "选择一个套餐以继续，或输入许可证密钥。" },
                { "hi", "जारी रखने के लिए एक प्लान चुनें या लाइसेंस कुंजी दर्ज करें।" },
                { "fi", "Valitse tilaus jatkaaksesi tai syötä lisenssiavain." },
                { "sv", "Välj en plan för att fortsätta, eller ange en licensnyckel." },
                { "nl", "Kies een abonnement om verder te gaan, of voer een licentiecode in." },
            } },
            // {0} = app display name
            { Key.LicenseExpiredLine, new Dictionary<string, string> {
                { "en", "Your license for {0} has expired." },
                { "es", "Tu licencia de {0} ha caducado." },
                { "fr", "Votre licence pour {0} a expiré." },
                { "de", "Ihre Lizenz für {0} ist abgelaufen." },
                { "it", "La tua licenza per {0} è scaduta." },
                { "pt", "A sua licença de {0} expirou." },
                { "ru", "Срок действия вашей лицензии на {0} истёк." },
                { "zh", "您的 {0} 许可证已过期。" },
                { "hi", "{0} का आपका लाइसेंस समाप्त हो गया है।" },
                { "fi", "Lisenssisi ({0}) on vanhentunut." },
                { "sv", "Din licens för {0} har gått ut." },
                { "nl", "Je licentie voor {0} is verlopen." },
            } },
            // {0} = app display name, {1} = server status code
            { Key.LicenseInactiveLine, new Dictionary<string, string> {
                { "en", "Your license for {0} is no longer active ({1})." },
                { "es", "Tu licencia de {0} ya no está activa ({1})." },
                { "fr", "Votre licence pour {0} n'est plus active ({1})." },
                { "de", "Ihre Lizenz für {0} ist nicht mehr aktiv ({1})." },
                { "it", "La tua licenza per {0} non è più attiva ({1})." },
                { "pt", "A sua licença de {0} já não está ativa ({1})." },
                { "ru", "Ваша лицензия на {0} больше не действует ({1})." },
                { "zh", "您的 {0} 许可证已失效（{1}）。" },
                { "hi", "{0} का आपका लाइसेंस अब सक्रिय नहीं है ({1})।" },
                { "fi", "Lisenssisi ({0}) ei ole enää voimassa ({1})." },
                { "sv", "Din licens för {0} är inte längre aktiv ({1})." },
                { "nl", "Je licentie voor {0} is niet meer actief ({1})." },
            } },
            { Key.ChoosePlanOrNewKey, new Dictionary<string, string> {
                { "en", "Choose a plan to continue, or enter a new license key." },
                { "es", "Elige un plan para continuar o introduce una nueva clave de licencia." },
                { "fr", "Choisissez une formule pour continuer, ou saisissez une nouvelle clé de licence." },
                { "de", "Wählen Sie einen Tarif, um fortzufahren, oder geben Sie einen neuen Lizenzschlüssel ein." },
                { "it", "Scegli un piano per continuare oppure inserisci una nuova chiave di licenza." },
                { "pt", "Escolha um plano para continuar ou insira uma nova chave de licença." },
                { "ru", "Выберите тариф, чтобы продолжить, или введите новый лицензионный ключ." },
                { "zh", "选择一个套餐以继续，或输入新的许可证密钥。" },
                { "hi", "जारी रखने के लिए एक प्लान चुनें या नई लाइसेंस कुंजी दर्ज करें।" },
                { "fi", "Valitse tilaus jatkaaksesi tai syötä uusi lisenssiavain." },
                { "sv", "Välj en plan för att fortsätta, eller ange en ny licensnyckel." },
                { "nl", "Kies een abonnement om verder te gaan, of voer een nieuwe licentiecode in." },
            } },
            { Key.RenewOnWeb, new Dictionary<string, string> {
                { "en", "Renew on the web portal or enter a new key." },
                { "es", "Renueva en el portal web o introduce una clave nueva." },
                { "fr", "Renouvelez sur le portail web ou saisissez une nouvelle clé." },
                { "de", "Verlängern Sie im Webportal oder geben Sie einen neuen Schlüssel ein." },
                { "it", "Rinnova sul portale web oppure inserisci una nuova chiave." },
                { "pt", "Renove no portal web ou insira uma nova chave." },
                { "ru", "Продлите лицензию на веб-портале или введите новый ключ." },
                { "zh", "请在网站上续订，或输入新的密钥。" },
                { "hi", "वेब पोर्टल पर नवीनीकरण करें या नई कुंजी दर्ज करें।" },
                { "fi", "Uusi lisenssi verkkoportaalissa tai syötä uusi avain." },
                { "sv", "Förnya på webbportalen eller ange en ny nyckel." },
                { "nl", "Verleng via het webportaal of voer een nieuwe code in." },
            } },
            { Key.AskOrgForKey, new Dictionary<string, string> {
                { "en", "Ask your organisation for a new key and enter it below." },
                { "es", "Pide una clave nueva a tu organización e introdúcela abajo." },
                { "fr", "Demandez une nouvelle clé à votre organisation et saisissez-la ci-dessous." },
                { "de", "Bitten Sie Ihre Organisation um einen neuen Schlüssel und geben Sie ihn unten ein." },
                { "it", "Chiedi una nuova chiave alla tua organizzazione e inseriscila qui sotto." },
                { "pt", "Peça uma nova chave à sua organização e insira-a abaixo." },
                { "ru", "Запросите новый ключ у своей организации и введите его ниже." },
                { "zh", "请向您的机构索取新密钥并在下方输入。" },
                { "hi", "अपने संगठन से नई कुंजी माँगें और उसे नीचे दर्ज करें।" },
                { "fi", "Pyydä organisaatioltasi uusi avain ja syötä se alle." },
                { "sv", "Be din organisation om en ny nyckel och ange den nedan." },
                { "nl", "Vraag je organisatie om een nieuwe code en voer die hieronder in." },
            } },
            // Success panel
            { Key.SubscriptionActiveTitle, new Dictionary<string, string> {
                { "en", "Subscription active" },
                { "es", "Suscripción activa" },
                { "fr", "Abonnement actif" },
                { "de", "Abonnement aktiv" },
                { "it", "Abbonamento attivo" },
                { "pt", "Assinatura ativa" },
                { "ru", "Подписка активна" },
                { "zh", "订阅有效" },
                { "hi", "सदस्यता सक्रिय है" },
                { "fi", "Tilaus voimassa" },
                { "sv", "Prenumeration aktiv" },
                { "nl", "Abonnement actief" },
            } },
            { Key.ThanksForSubscribing, new Dictionary<string, string> {
                { "en", "Thank you for subscribing!" },
                { "es", "¡Gracias por suscribirte!" },
                { "fr", "Merci pour votre abonnement !" },
                { "de", "Vielen Dank für Ihr Abonnement!" },
                { "it", "Grazie per esserti abbonato!" },
                { "pt", "Obrigado por assinar!" },
                { "ru", "Спасибо за подписку!" },
                { "zh", "感谢您的订阅！" },
                { "hi", "सदस्यता लेने के लिए धन्यवाद!" },
                { "fi", "Kiitos tilauksestasi!" },
                { "sv", "Tack för din prenumeration!" },
                { "nl", "Bedankt voor je abonnement!" },
            } },
            { Key.SubscriptionActiveBody, new Dictionary<string, string> {
                { "en", "Your Meta account has an active subscription.\nEverything's good — enjoy the simulator." },
                { "es", "Tu cuenta de Meta tiene una suscripción activa.\nTodo en orden: disfruta del simulador." },
                { "fr", "Votre compte Meta a un abonnement actif.\nTout est en ordre, profitez du simulateur." },
                { "de", "Ihr Meta-Konto hat ein aktives Abonnement.\nAlles in Ordnung – viel Spaß mit dem Simulator." },
                { "it", "Il tuo account Meta ha un abbonamento attivo.\nTutto a posto: goditi il simulatore." },
                { "pt", "A sua conta Meta tem uma assinatura ativa.\nEstá tudo certo: aproveite o simulador." },
                { "ru", "На вашем аккаунте Meta есть активная подписка.\nВсё в порядке — приятной работы в симуляторе." },
                { "zh", "您的 Meta 账户有有效订阅。\n一切就绪，尽情体验模拟器吧。" },
                { "hi", "आपके Meta खाते पर एक सक्रिय सदस्यता है।\nसब ठीक है — सिम्युलेटर का आनंद लें।" },
                { "fi", "Meta-tililläsi on aktiivinen tilaus.\nKaikki kunnossa – nauti simulaattorista." },
                { "sv", "Ditt Meta-konto har en aktiv prenumeration.\nAllt är klart – ha det så kul i simulatorn." },
                { "nl", "Je Meta-account heeft een actief abonnement.\nAlles in orde, veel plezier met de simulator." },
            } },
            { Key.SubscriptionNewBody, new Dictionary<string, string> {
                { "en", "Your subscription is active.\nThe simulator will unlock automatically." },
                { "es", "Tu suscripción está activa.\nEl simulador se desbloqueará automáticamente." },
                { "fr", "Votre abonnement est actif.\nLe simulateur va se débloquer automatiquement." },
                { "de", "Ihr Abonnement ist aktiv.\nDer Simulator wird automatisch freigeschaltet." },
                { "it", "Il tuo abbonamento è attivo.\nIl simulatore si sbloccherà automaticamente." },
                { "pt", "A sua assinatura está ativa.\nO simulador será desbloqueado automaticamente." },
                { "ru", "Ваша подписка активна.\nСимулятор разблокируется автоматически." },
                { "zh", "您的订阅已生效。\n模拟器将自动解锁。" },
                { "hi", "आपकी सदस्यता सक्रिय है।\nसिम्युलेटर अपने आप अनलॉक हो जाएगा।" },
                { "fi", "Tilauksesi on voimassa.\nSimulaattori avautuu automaattisesti." },
                { "sv", "Din prenumeration är aktiv.\nSimulatorn låses upp automatiskt." },
                { "nl", "Je abonnement is actief.\nDe simulator wordt automatisch ontgrendeld." },
            } },
            { Key.LicenseActiveTitle, new Dictionary<string, string> {
                { "en", "License active" },
                { "es", "Licencia activa" },
                { "fr", "Licence active" },
                { "de", "Lizenz aktiv" },
                { "it", "Licenza attiva" },
                { "pt", "Licença ativa" },
                { "ru", "Лицензия активна" },
                { "zh", "许可证有效" },
                { "hi", "लाइसेंस सक्रिय है" },
                { "fi", "Lisenssi voimassa" },
                { "sv", "Licens aktiv" },
                { "nl", "Licentie actief" },
            } },
            { Key.KeyRedeemedTitle, new Dictionary<string, string> {
                { "en", "Key redeemed successfully!" },
                { "es", "¡Clave canjeada correctamente!" },
                { "fr", "Clé activée avec succès !" },
                { "de", "Schlüssel erfolgreich eingelöst!" },
                { "it", "Chiave riscattata con successo!" },
                { "pt", "Chave resgatada com sucesso!" },
                { "ru", "Ключ успешно активирован!" },
                { "zh", "密钥兑换成功！" },
                { "hi", "कुंजी सफलतापूर्वक रिडीम हुई!" },
                { "fi", "Avain lunastettu!" },
                { "sv", "Nyckeln har lösts in!" },
                { "nl", "Code succesvol ingewisseld!" },
            } },
            { Key.LicenseActiveBody, new Dictionary<string, string> {
                { "en", "This device already has a valid license.\nEverything's good — enjoy the simulator." },
                { "es", "Este dispositivo ya tiene una licencia válida.\nTodo en orden: disfruta del simulador." },
                { "fr", "Cet appareil a déjà une licence valide.\nTout est en ordre, profitez du simulateur." },
                { "de", "Dieses Gerät hat bereits eine gültige Lizenz.\nAlles in Ordnung – viel Spaß mit dem Simulator." },
                { "it", "Questo dispositivo ha già una licenza valida.\nTutto a posto: goditi il simulatore." },
                { "pt", "Este dispositivo já tem uma licença válida.\nEstá tudo certo: aproveite o simulador." },
                { "ru", "На этом устройстве уже есть действующая лицензия.\nВсё в порядке — приятной работы в симуляторе." },
                { "zh", "此设备已有有效许可证。\n一切就绪，尽情体验模拟器吧。" },
                { "hi", "इस डिवाइस पर पहले से एक मान्य लाइसेंस है।\nसब ठीक है — सिम्युलेटर का आनंद लें।" },
                { "fi", "Tällä laitteella on jo voimassa oleva lisenssi.\nKaikki kunnossa – nauti simulaattorista." },
                { "sv", "Den här enheten har redan en giltig licens.\nAllt är klart – ha det så kul i simulatorn." },
                { "nl", "Dit apparaat heeft al een geldige licentie.\nAlles in orde, veel plezier met de simulator." },
            } },
            { Key.LicenseNewBody, new Dictionary<string, string> {
                { "en", "Your license has been activated successfully.\nThe simulator will unlock automatically." },
                { "es", "Tu licencia se ha activado correctamente.\nEl simulador se desbloqueará automáticamente." },
                { "fr", "Votre licence a été activée avec succès.\nLe simulateur va se débloquer automatiquement." },
                { "de", "Ihre Lizenz wurde erfolgreich aktiviert.\nDer Simulator wird automatisch freigeschaltet." },
                { "it", "La tua licenza è stata attivata con successo.\nIl simulatore si sbloccherà automaticamente." },
                { "pt", "A sua licença foi ativada com sucesso.\nO simulador será desbloqueado automaticamente." },
                { "ru", "Ваша лицензия успешно активирована.\nСимулятор разблокируется автоматически." },
                { "zh", "您的许可证已成功激活。\n模拟器将自动解锁。" },
                { "hi", "आपका लाइसेंस सफलतापूर्वक सक्रिय हो गया है।\nसिम्युलेटर अपने आप अनलॉक हो जाएगा।" },
                { "fi", "Lisenssisi on aktivoitu.\nSimulaattori avautuu automaattisesti." },
                { "sv", "Din licens har aktiverats.\nSimulatorn låses upp automatiskt." },
                { "nl", "Je licentie is geactiveerd.\nDe simulator wordt automatisch ontgrendeld." },
            } },
            { Key.LicenseActivated, new Dictionary<string, string> {
                { "en", "License activated successfully" },
                { "es", "Licencia activada correctamente" },
                { "fr", "Licence activée avec succès" },
                { "de", "Lizenz erfolgreich aktiviert" },
                { "it", "Licenza attivata con successo" },
                { "pt", "Licença ativada com sucesso" },
                { "ru", "Лицензия успешно активирована" },
                { "zh", "许可证激活成功" },
                { "hi", "लाइसेंस सफलतापूर्वक सक्रिय हुआ" },
                { "fi", "Lisenssi aktivoitu" },
                { "sv", "Licensen har aktiverats" },
                { "nl", "Licentie geactiveerd" },
            } },
            // QR scan HUD and scanner status lines
            { Key.ScanTitle, new Dictionary<string, string> {
                { "en", "Look at the QR code" },
                { "es", "Mira el código QR" },
                { "fr", "Regardez le code QR" },
                { "de", "Schauen Sie auf den QR-Code" },
                { "it", "Guarda il codice QR" },
                { "pt", "Olhe para o código QR" },
                { "ru", "Посмотрите на QR-код" },
                { "zh", "请注视二维码" },
                { "hi", "QR कोड को देखें" },
                { "fi", "Katso QR-koodia" },
                { "sv", "Titta på QR-koden" },
                { "nl", "Kijk naar de QR-code" },
            } },
            { Key.ScanSubtitle, new Dictionary<string, string> {
                { "en", "Hold it steady in front of you — or cancel and type the key manually." },
                { "es", "Mantenlo quieto frente a ti, o cancela y escribe la clave a mano." },
                { "fr", "Tenez-le immobile devant vous, ou annulez et saisissez la clé manuellement." },
                { "de", "Halten Sie ihn ruhig vor sich – oder brechen Sie ab und geben Sie den Schlüssel manuell ein." },
                { "it", "Tienilo fermo davanti a te, oppure annulla e digita la chiave a mano." },
                { "pt", "Mantenha-o parado à sua frente, ou cancele e digite a chave manualmente." },
                { "ru", "Держите его неподвижно перед собой — или отмените и введите ключ вручную." },
                { "zh", "请将二维码稳定地置于眼前，或取消并手动输入密钥。" },
                { "hi", "इसे अपने सामने स्थिर रखें — या रद्द करके कुंजी हाथ से टाइप करें।" },
                { "fi", "Pidä se paikallaan edessäsi – tai peruuta ja kirjoita avain käsin." },
                { "sv", "Håll den stilla framför dig – eller avbryt och skriv in nyckeln manuellt." },
                { "nl", "Houd hem stil voor je, of annuleer en typ de code zelf in." },
            } },
            { Key.ScanCancelled, new Dictionary<string, string> {
                { "en", "Scan cancelled." },
                { "es", "Escaneo cancelado." },
                { "fr", "Scan annulé." },
                { "de", "Scan abgebrochen." },
                { "it", "Scansione annullata." },
                { "pt", "Leitura cancelada." },
                { "ru", "Сканирование отменено." },
                { "zh", "已取消扫描。" },
                { "hi", "स्कैन रद्द किया गया।" },
                { "fi", "Skannaus peruutettu." },
                { "sv", "Skanningen avbröts." },
                { "nl", "Scannen geannuleerd." },
            } },
            { Key.StartingCamera, new Dictionary<string, string> {
                { "en", "Starting camera..." },
                { "es", "Iniciando la cámara..." },
                { "fr", "Démarrage de la caméra..." },
                { "de", "Kamera wird gestartet..." },
                { "it", "Avvio della fotocamera..." },
                { "pt", "Iniciando a câmera..." },
                { "ru", "Запуск камеры..." },
                { "zh", "正在启动相机..." },
                { "hi", "कैमरा शुरू हो रहा है..." },
                { "fi", "Käynnistetään kameraa..." },
                { "sv", "Startar kameran..." },
                { "nl", "Camera starten..." },
            } },
            { Key.QrDetected, new Dictionary<string, string> {
                { "en", "QR detected — validating..." },
                { "es", "QR detectado: validando..." },
                { "fr", "QR détecté, vérification..." },
                { "de", "QR erkannt – wird geprüft..." },
                { "it", "QR rilevato: verifica in corso..." },
                { "pt", "QR detectado: validando..." },
                { "ru", "QR-код распознан — проверка..." },
                { "zh", "已识别二维码，正在验证..." },
                { "hi", "QR मिला — जाँच हो रही है..." },
                { "fi", "QR tunnistettu – tarkistetaan..." },
                { "sv", "QR hittad – verifierar..." },
                { "nl", "QR herkend, controleren..." },
            } },
            // Appended after a terminal scanner error
            { Key.EnterKeyManually, new Dictionary<string, string> {
                { "en", "Enter your key manually." },
                { "es", "Introduce la clave a mano." },
                { "fr", "Saisissez votre clé manuellement." },
                { "de", "Geben Sie Ihren Schlüssel manuell ein." },
                { "it", "Inserisci la chiave manualmente." },
                { "pt", "Insira a sua chave manualmente." },
                { "ru", "Введите ключ вручную." },
                { "zh", "请手动输入密钥。" },
                { "hi", "अपनी कुंजी हाथ से दर्ज करें।" },
                { "fi", "Syötä avain käsin." },
                { "sv", "Ange din nyckel manuellt." },
                { "nl", "Voer je code zelf in." },
            } },
            { Key.RequestingCamera, new Dictionary<string, string> {
                { "en", "Requesting camera permission..." },
                { "es", "Solicitando permiso de cámara..." },
                { "fr", "Demande d'accès à la caméra..." },
                { "de", "Kamerazugriff wird angefragt..." },
                { "it", "Richiesta del permesso per la fotocamera..." },
                { "pt", "Solicitando permissão da câmera..." },
                { "ru", "Запрос доступа к камере..." },
                { "zh", "正在请求相机权限..." },
                { "hi", "कैमरा अनुमति माँगी जा रही है..." },
                { "fi", "Pyydetään kameran käyttöoikeutta..." },
                { "sv", "Begär kameraåtkomst..." },
                { "nl", "Cameratoestemming vragen..." },
            } },
            { Key.CameraDenied, new Dictionary<string, string> {
                { "en", "Camera permission denied." },
                { "es", "Permiso de cámara denegado." },
                { "fr", "Accès à la caméra refusé." },
                { "de", "Kamerazugriff verweigert." },
                { "it", "Permesso per la fotocamera negato." },
                { "pt", "Permissão da câmera negada." },
                { "ru", "Доступ к камере запрещён." },
                { "zh", "相机权限被拒绝。" },
                { "hi", "कैमरा अनुमति अस्वीकृत।" },
                { "fi", "Kameran käyttöoikeus evätty." },
                { "sv", "Kameraåtkomst nekad." },
                { "nl", "Cameratoestemming geweigerd." },
            } },
            { Key.QrDecoderMissing, new Dictionary<string, string> {
                { "en", "QR decoder not available." },
                { "es", "Lector de QR no disponible." },
                { "fr", "Décodeur QR non disponible." },
                { "de", "QR-Decoder nicht verfügbar." },
                { "it", "Decodificatore QR non disponibile." },
                { "pt", "Leitor de QR não disponível." },
                { "ru", "Декодер QR недоступен." },
                { "zh", "二维码解码器不可用。" },
                { "hi", "QR डिकोडर उपलब्ध नहीं है।" },
                { "fi", "QR-lukija ei ole käytettävissä." },
                { "sv", "QR-avkodaren är inte tillgänglig." },
                { "nl", "QR-lezer niet beschikbaar." },
            } },
            { Key.NoCamera, new Dictionary<string, string> {
                { "en", "No camera available on this headset." },
                { "es", "No hay cámara disponible en este visor." },
                { "fr", "Aucune caméra disponible sur ce casque." },
                { "de", "Auf diesem Headset ist keine Kamera verfügbar." },
                { "it", "Nessuna fotocamera disponibile su questo visore." },
                { "pt", "Nenhuma câmera disponível neste headset." },
                { "ru", "На этой гарнитуре нет доступной камеры." },
                { "zh", "此头显没有可用的相机。" },
                { "hi", "इस हेडसेट पर कोई कैमरा उपलब्ध नहीं है।" },
                { "fi", "Näissä laseissa ei ole kameraa käytettävissä." },
                { "sv", "Ingen kamera tillgänglig på det här headsetet." },
                { "nl", "Geen camera beschikbaar op deze headset." },
            } },
            { Key.PointQr, new Dictionary<string, string> {
                { "en", "Point the QR code into view..." },
                { "es", "Apunta al código QR..." },
                { "fr", "Placez le code QR dans votre champ de vision..." },
                { "de", "Richten Sie den Blick auf den QR-Code..." },
                { "it", "Inquadra il codice QR..." },
                { "pt", "Aponte para o código QR..." },
                { "ru", "Наведите взгляд на QR-код..." },
                { "zh", "请将二维码置于视野中..." },
                { "hi", "QR कोड को सामने लाएँ..." },
                { "fi", "Kohdista katse QR-koodiin..." },
                { "sv", "Rikta blicken mot QR-koden..." },
                { "nl", "Richt je blik op de QR-code..." },
            } },
            { Key.CameraNotShared, new Dictionary<string, string> {
                { "en", "This headset is not sharing the camera image. Update HorizonOS, or enter the key manually." },
                { "es", "Este visor no comparte la imagen de la cámara. Actualiza HorizonOS o introduce la clave a mano." },
                { "fr", "Ce casque ne partage pas l'image de la caméra. Mettez à jour HorizonOS ou saisissez la clé manuellement." },
                { "de", "Dieses Headset gibt das Kamerabild nicht frei. Aktualisieren Sie HorizonOS oder geben Sie den Schlüssel manuell ein." },
                { "it", "Questo visore non condivide l'immagine della fotocamera. Aggiorna HorizonOS oppure inserisci la chiave a mano." },
                { "pt", "Este headset não compartilha a imagem da câmera. Atualize o HorizonOS ou insira a chave manualmente." },
                { "ru", "Эта гарнитура не передаёт изображение с камеры. Обновите HorizonOS или введите ключ вручную." },
                { "zh", "此头显未共享相机画面。请更新 HorizonOS，或手动输入密钥。" },
                { "hi", "यह हेडसेट कैमरा छवि साझा नहीं कर रहा है। HorizonOS अपडेट करें या कुंजी हाथ से दर्ज करें।" },
                { "fi", "Nämä lasit eivät jaa kamerakuvaa. Päivitä HorizonOS tai syötä avain käsin." },
                { "sv", "Det här headsetet delar inte kamerabilden. Uppdatera HorizonOS eller ange nyckeln manuellt." },
                { "nl", "Deze headset deelt het camerabeeld niet. Werk HorizonOS bij of voer de code zelf in." },
            } },
            { Key.NoQrDetected, new Dictionary<string, string> {
                { "en", "No QR code detected. Try again or enter the key manually." },
                { "es", "No se detectó ningún código QR. Inténtalo de nuevo o introduce la clave a mano." },
                { "fr", "Aucun code QR détecté. Réessayez ou saisissez la clé manuellement." },
                { "de", "Kein QR-Code erkannt. Versuchen Sie es erneut oder geben Sie den Schlüssel manuell ein." },
                { "it", "Nessun codice QR rilevato. Riprova oppure inserisci la chiave a mano." },
                { "pt", "Nenhum código QR detectado. Tente novamente ou insira a chave manualmente." },
                { "ru", "QR-код не распознан. Попробуйте снова или введите ключ вручную." },
                { "zh", "未检测到二维码。请重试或手动输入密钥。" },
                { "hi", "कोई QR कोड नहीं मिला। फिर से प्रयास करें या कुंजी हाथ से दर्ज करें।" },
                { "fi", "QR-koodia ei tunnistettu. Yritä uudelleen tai syötä avain käsin." },
                { "sv", "Ingen QR-kod hittades. Försök igen eller ange nyckeln manuellt." },
                { "nl", "Geen QR-code gevonden. Probeer het opnieuw of voer de code zelf in." },
            } },
            // Direct (non-store) APKs: organisation hint / purchase CTA column
            { Key.OrgTitle, new Dictionary<string, string> {
                { "en", "Continue your training" },
                { "es", "Continúa tu formación" },
                { "fr", "Poursuivez votre formation" },
                { "de", "Setzen Sie Ihre Schulung fort" },
                { "it", "Continua la tua formazione" },
                { "pt", "Continue o seu treinamento" },
                { "ru", "Продолжите обучение" },
                { "zh", "继续您的培训" },
                { "hi", "अपना प्रशिक्षण जारी रखें" },
                { "fi", "Jatka koulutustasi" },
                { "sv", "Fortsätt din utbildning" },
                { "nl", "Ga verder met je training" },
            } },
            { Key.OrgBody, new Dictionary<string, string> {
                { "en", "The full course is licensed to companies and training centres.\n\nAsk your employer or training centre for a license key, then enter it on the right or scan its QR code." },
                { "es", "El curso completo se licencia a empresas y centros de formación.\n\nPide una clave de licencia a tu empresa o centro de formación e introdúcela a la derecha, o escanea su código QR." },
                { "fr", "Le cours complet est sous licence pour les entreprises et centres de formation.\n\nDemandez une clé de licence à votre employeur ou centre de formation, puis saisissez-la à droite ou scannez son code QR." },
                { "de", "Der vollständige Kurs wird an Unternehmen und Schulungszentren lizenziert.\n\nBitten Sie Ihren Arbeitgeber oder Ihr Schulungszentrum um einen Lizenzschlüssel und geben Sie ihn rechts ein oder scannen Sie seinen QR-Code." },
                { "it", "Il corso completo è concesso in licenza ad aziende e centri di formazione.\n\nChiedi una chiave di licenza al tuo datore di lavoro o centro di formazione, poi inseriscila a destra o scansiona il suo codice QR." },
                { "pt", "O curso completo é licenciado para empresas e centros de formação.\n\nPeça uma chave de licença à sua empresa ou centro de formação e insira-a à direita, ou escaneie o seu código QR." },
                { "ru", "Полный курс лицензируется компаниям и учебным центрам.\n\nЗапросите лицензионный ключ у работодателя или учебного центра, затем введите его справа или отсканируйте его QR-код." },
                { "zh", "完整课程面向企业和培训中心授权。\n\n请向您的雇主或培训中心索取许可证密钥，然后在右侧输入或扫描其二维码。" },
                { "hi", "पूरा कोर्स कंपनियों और प्रशिक्षण केंद्रों को लाइसेंस किया जाता है।\n\nअपने नियोक्ता या प्रशिक्षण केंद्र से लाइसेंस कुंजी माँगें, फिर उसे दाईं ओर दर्ज करें या उसका QR कोड स्कैन करें।" },
                { "fi", "Koko kurssi lisensoidaan yrityksille ja koulutuskeskuksille.\n\nPyydä lisenssiavain työnantajaltasi tai koulutuskeskukselta ja syötä se oikealle tai skannaa sen QR-koodi." },
                { "sv", "Hela kursen licensieras till företag och utbildningscenter.\n\nBe din arbetsgivare eller ditt utbildningscenter om en licensnyckel och ange den till höger, eller skanna dess QR-kod." },
                { "nl", "De volledige cursus wordt in licentie gegeven aan bedrijven en opleidingscentra.\n\nVraag je werkgever of opleidingscentrum om een licentiecode en voer die rechts in, of scan de QR-code." },
            } },
            { Key.BuyTitle, new Dictionary<string, string> {
                { "en", "Get the full version" },
                { "es", "Consigue la versión completa" },
                { "fr", "Obtenez la version complète" },
                { "de", "Vollversion erhalten" },
                { "it", "Ottieni la versione completa" },
                { "pt", "Obtenha a versão completa" },
                { "ru", "Получите полную версию" },
                { "zh", "获取完整版" },
                { "hi", "पूरा संस्करण प्राप्त करें" },
                { "fi", "Hanki täysi versio" },
                { "sv", "Skaffa fullversionen" },
                { "nl", "Koop de volledige versie" },
            } },
            { Key.SpecialOffer, new Dictionary<string, string> {
                { "en", "Special offer" },
                { "es", "Oferta especial" },
                { "fr", "Offre spéciale" },
                { "de", "Sonderangebot" },
                { "it", "Offerta speciale" },
                { "pt", "Oferta especial" },
                { "ru", "Специальное предложение" },
                { "zh", "特别优惠" },
                { "hi", "विशेष ऑफ़र" },
                { "fi", "Erikoistarjous" },
                { "sv", "Specialerbjudande" },
                { "nl", "Speciale aanbieding" },
            } },
            // {0} = promo code (rich text bold kept in every language)
            { Key.UseCode, new Dictionary<string, string> {
                { "en", "Use code  <b>{0}</b>" },
                { "es", "Usa el código  <b>{0}</b>" },
                { "fr", "Utilisez le code  <b>{0}</b>" },
                { "de", "Code verwenden:  <b>{0}</b>" },
                { "it", "Usa il codice  <b>{0}</b>" },
                { "pt", "Use o código  <b>{0}</b>" },
                { "ru", "Используйте код  <b>{0}</b>" },
                { "zh", "使用优惠码  <b>{0}</b>" },
                { "hi", "कोड  <b>{0}</b>  इस्तेमाल करें" },
                { "fi", "Käytä koodia  <b>{0}</b>" },
                { "sv", "Använd koden  <b>{0}</b>" },
                { "nl", "Gebruik code  <b>{0}</b>" },
            } },
            { Key.ScanPhonePromo, new Dictionary<string, string> {
                { "en", "Scan with your phone — the discount is applied automatically." },
                { "es", "Escanea con tu móvil: el descuento se aplica automáticamente." },
                { "fr", "Scannez avec votre téléphone, la remise est appliquée automatiquement." },
                { "de", "Mit dem Handy scannen – der Rabatt wird automatisch angewendet." },
                { "it", "Scansiona con il telefono: lo sconto viene applicato automaticamente." },
                { "pt", "Escaneie com o seu celular: o desconto é aplicado automaticamente." },
                { "ru", "Отсканируйте телефоном — скидка применится автоматически." },
                { "zh", "用手机扫描，折扣将自动生效。" },
                { "hi", "अपने फ़ोन से स्कैन करें — छूट अपने आप लागू हो जाएगी।" },
                { "fi", "Skannaa puhelimellasi – alennus lisätään automaattisesti." },
                { "sv", "Skanna med din telefon – rabatten dras av automatiskt." },
                { "nl", "Scan met je telefoon, de korting wordt automatisch toegepast." },
            } },
            { Key.ScanPhoneOrVisit, new Dictionary<string, string> {
                { "en", "Scan with your phone, or visit the address above." },
                { "es", "Escanea con tu móvil o visita la dirección de arriba." },
                { "fr", "Scannez avec votre téléphone ou rendez-vous à l'adresse ci-dessus." },
                { "de", "Mit dem Handy scannen oder die obige Adresse aufrufen." },
                { "it", "Scansiona con il telefono oppure visita l'indirizzo qui sopra." },
                { "pt", "Escaneie com o seu celular ou acesse o endereço acima." },
                { "ru", "Отсканируйте телефоном или перейдите по адресу выше." },
                { "zh", "用手机扫描，或访问上方网址。" },
                { "hi", "अपने फ़ोन से स्कैन करें या ऊपर दिए पते पर जाएँ।" },
                { "fi", "Skannaa puhelimellasi tai avaa yllä oleva osoite." },
                { "sv", "Skanna med din telefon eller besök adressen ovan." },
                { "nl", "Scan met je telefoon of ga naar het adres hierboven." },
            } },
            { Key.VisitToPurchase, new Dictionary<string, string> {
                { "en", "Visit the address above to purchase a license." },
                { "es", "Visita la dirección de arriba para comprar una licencia." },
                { "fr", "Rendez-vous à l'adresse ci-dessus pour acheter une licence." },
                { "de", "Rufen Sie die obige Adresse auf, um eine Lizenz zu kaufen." },
                { "it", "Visita l'indirizzo qui sopra per acquistare una licenza." },
                { "pt", "Acesse o endereço acima para comprar uma licença." },
                { "ru", "Перейдите по адресу выше, чтобы купить лицензию." },
                { "zh", "请访问上方网址购买许可证。" },
                { "hi", "लाइसेंस खरीदने के लिए ऊपर दिए पते पर जाएँ।" },
                { "fi", "Osta lisenssi yllä olevasta osoitteesta." },
                { "sv", "Besök adressen ovan för att köpa en licens." },
                { "nl", "Ga naar het adres hierboven om een licentie te kopen." },
            } },
            // Head-locked countdown; {0} = mm:ss
            { Key.DemoHud, new Dictionary<string, string> {
                { "en", "Demo  ·  {0} left" },
                { "es", "Demo  ·  quedan {0}" },
                { "fr", "Démo  ·  il reste {0}" },
                { "de", "Demo  ·  noch {0}" },
                { "it", "Demo  ·  {0} rimanenti" },
                { "pt", "Demo  ·  restam {0}" },
                { "ru", "Демо  ·  осталось {0}" },
                { "zh", "试用  ·  剩余 {0}" },
                { "hi", "डेमो  ·  {0} बाकी" },
                { "fi", "Demo  ·  {0} jäljellä" },
                { "sv", "Demo  ·  {0} kvar" },
                { "nl", "Demo  ·  nog {0}" },
            } },
        };
    }
}
