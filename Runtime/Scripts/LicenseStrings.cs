using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// The few license-UI strings that must follow the user's language: the privacy device
    /// code label and the store subscription panel (Meta requires an accurate, localized
    /// description of what the subscription unlocks). The rest of the license UI is English.
    /// Language: <see cref="LanguageOverride"/> if the host app sets it from its own selector,
    /// otherwise the headset's system language. Codes are ISO 639-1 (12 languages since v1.12.0-pre.12: + Dutch). "license" uses US spelling.
    /// </summary>
    public static class LicenseStrings
    {
        /// <summary>
        /// Optional ISO 639-1 code ("es", "pt", ...) set by the host app when it has its own
        /// language selector. Null/empty = follow Application.systemLanguage.
        /// </summary>
        public static string LanguageOverride;

        public static readonly string[] SupportedLanguages =
            { "en", "es", "fr", "de", "it", "pt", "ru", "zh", "hi", "fi", "sv", "nl" };

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
            if (lang == "zh") return total + word; // 3个月, 1年
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
    }
}
