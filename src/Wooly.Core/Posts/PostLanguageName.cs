namespace Wooly.Core.Posts;

/// <summary>
///     The languages a post can say it is in, and how each is spelled where a user writes one, in the one place every
///     entry point can reach it — the <c>--language</c> flag, the <c>default_language</c> key in the config file, and
///     the TUI's list. A user who writes <c>French</c> in one of them has written the same word they would write in the
///     others, so the three cannot be allowed to accept different sets of them (ADR-0024).
/// </summary>
/// <remarks>
///     The client holds its own list rather than asking the instance for one, as it holds its own spelling of a
///     visibility. It is the list Mastodon itself accepts for a post (<c>LanguagesHelper::SUPPORTED_LOCALES</c>): the
///     ISO 639-1 codes it lists, the regional codes it takes as languages of their own (<c>zh-TW</c>, <c>mn-Mong</c>),
///     and the ISO 639-3 codes it takes for languages that have no two-letter one. A code Mastodon does not list is not
///     refused there but quietly replaced by the account's own language, so offering one would publish a post in a
///     language the author did not choose.
/// </remarks>
public static class PostLanguageName
{
    /// <summary>
    ///     Every language this client accepts, ISO 639-1 first and then ISO 639-3, each in code order with a regional
    ///     code after the language it belongs to.
    /// </summary>
    public static IReadOnlyList<PostLanguage> All { get; } =
    [
        new("aa", "Afar", "Afaraf"),
        new("ab", "Abkhaz", "аҧсуа бызшәа"),
        new("ae", "Avestan", "avesta"),
        new("af", "Afrikaans", "Afrikaans"),
        new("ak", "Akan", "Akan"),
        new("am", "Amharic", "አማርኛ"),
        new("an", "Aragonese", "aragonés"),
        new("ar", "Arabic", "العربية"),
        new("as", "Assamese", "অসমীয়া"),
        new("av", "Avaric", "авар мацӀ"),
        new("ay", "Aymara", "aymar aru"),
        new("az", "Azerbaijani", "azərbaycan dili"),
        new("ba", "Bashkir", "башҡорт теле"),
        new("be", "Belarusian", "беларуская мова"),
        new("bg", "Bulgarian", "български език"),
        new("bh", "Bihari", "भोजपुरी"),
        new("bi", "Bislama", "Bislama"),
        new("bm", "Bambara", "bamanankan"),
        new("bn", "Bengali", "বাংলা"),
        new("bo", "Tibetan", "བོད་ཡིག"),
        new("br", "Breton", "brezhoneg"),
        new("bs", "Bosnian", "bosanski jezik"),
        new("ca", "Catalan", "català"),
        new("ce", "Chechen", "нохчийн мотт"),
        new("ch", "Chamorro", "Chamoru"),
        new("co", "Corsican", "corsu"),
        new("cr", "Cree", "ᓀᐦᐃᔭᐍᐏᐣ"),
        new("cs", "Czech", "čeština"),
        new("cu", "Old Church Slavonic", "ѩзыкъ словѣньскъ"),
        new("cv", "Chuvash", "чӑваш чӗлхи"),
        new("cy", "Welsh", "Cymraeg"),
        new("da", "Danish", "dansk"),
        new("de", "German", "Deutsch"),
        new("dv", "Divehi", "ދިވެހި"),
        new("dz", "Dzongkha", "རྫོང་ཁ"),
        new("ee", "Ewe", "Eʋegbe"),
        new("el", "Greek", "Ελληνικά"),
        new("en", "English", "English"),
        new("eo", "Esperanto", "Esperanto"),
        new("es", "Spanish", "Español"),
        new("et", "Estonian", "eesti"),
        new("eu", "Basque", "euskara"),
        new("fa", "Persian", "فارسی"),
        new("ff", "Fula", "Fulfulde"),
        new("fi", "Finnish", "suomi"),
        new("fj", "Fijian", "vosa Vakaviti"),
        new("fo", "Faroese", "føroyskt"),
        new("fr", "French", "Français"),
        new("fy", "Western Frisian", "Frysk"),
        new("ga", "Irish", "Gaeilge"),
        new("gd", "Scottish Gaelic", "Gàidhlig"),
        new("gl", "Galician", "galego"),
        new("gu", "Gujarati", "ગુજરાતી"),
        new("gv", "Manx", "Gaelg"),
        new("ha", "Hausa", "هَوُسَ"),
        new("he", "Hebrew", "עברית"),
        new("hi", "Hindi", "हिन्दी"),
        new("ho", "Hiri Motu", "Hiri Motu"),
        new("hr", "Croatian", "hrvatski"),
        new("ht", "Haitian", "Kreyòl ayisyen"),
        new("hu", "Hungarian", "magyar"),
        new("hy", "Armenian", "Հայերեն"),
        new("hz", "Herero", "Otjiherero"),
        new("ia", "Interlingua", "Interlingua"),
        new("id", "Indonesian", "Bahasa Indonesia"),
        new("ie", "Interlingue", "Interlingue"),
        new("ig", "Igbo", "Asụsụ Igbo"),
        new("ii", "Nuosu", "ꆈꌠ꒿ Nuosuhxop"),
        new("ik", "Inupiaq", "Iñupiaq"),
        new("io", "Ido", "Ido"),
        new("is", "Icelandic", "Íslenska"),
        new("it", "Italian", "Italiano"),
        new("iu", "Inuktitut", "ᐃᓄᒃᑎᑐᑦ"),
        new("ja", "Japanese", "日本語"),
        new("jv", "Javanese", "basa Jawa"),
        new("ka", "Georgian", "ქართული"),
        new("kg", "Kongo", "Kikongo"),
        new("ki", "Kikuyu", "Gĩkũyũ"),
        new("kj", "Kwanyama", "Kuanyama"),
        new("kk", "Kazakh", "қазақ тілі"),
        new("kl", "Kalaallisut", "kalaallisut"),
        new("km", "Khmer", "ខេមរភាសា"),
        new("kn", "Kannada", "ಕನ್ನಡ"),
        new("ko", "Korean", "한국어"),
        new("kr", "Kanuri", "Kanuri"),
        new("ks", "Kashmiri", "कश्मीरी"),
        new("ku", "Kurmanji (Kurdish)", "Kurmancî"),
        new("kv", "Komi", "коми кыв"),
        new("kw", "Cornish", "Kernewek"),
        new("ky", "Kyrgyz", "Кыргызча"),
        new("la", "Latin", "latine"),
        new("lb", "Luxembourgish", "Lëtzebuergesch"),
        new("lg", "Ganda", "Luganda"),
        new("li", "Limburgish", "Limburgs"),
        new("ln", "Lingala", "Lingála"),
        new("lo", "Lao", "ລາວ"),
        new("lt", "Lithuanian", "lietuvių kalba"),
        new("lu", "Luba-Katanga", "Tshiluba"),
        new("lv", "Latvian", "latviešu valoda"),
        new("mg", "Malagasy", "fiteny malagasy"),
        new("mh", "Marshallese", "Kajin M̧ajeļ"),
        new("mi", "Māori", "te reo Māori"),
        new("mk", "Macedonian", "македонски јазик"),
        new("ml", "Malayalam", "മലയാളം"),
        new("mn", "Mongolian", "Монгол хэл"),
        new("mn-Mong", "Traditional Mongolian", "ᠮᠣᠩᠭᠣᠯ ᠬᠡᠯᠡ"),
        new("mr", "Marathi", "मराठी"),
        new("ms", "Malay", "Bahasa Melayu"),
        new("ms-Arab", "Jawi Malay", "بهاس ملايو"),
        new("mt", "Maltese", "Malti"),
        new("my", "Burmese", "ဗမာစာ"),
        new("na", "Nauru", "Ekakairũ Naoero"),
        new("nb", "Norwegian Bokmål", "Norsk bokmål"),
        new("nd", "Northern Ndebele", "isiNdebele"),
        new("ne", "Nepali", "नेपाली"),
        new("ng", "Ndonga", "Owambo"),
        new("nl", "Dutch", "Nederlands"),
        new("nn", "Norwegian Nynorsk", "Norsk nynorsk"),
        new("no", "Norwegian", "Norsk"),
        new("nr", "Southern Ndebele", "isiNdebele"),
        new("nv", "Navajo", "Diné bizaad"),
        new("ny", "Chichewa", "chiCheŵa"),
        new("oc", "Occitan", "occitan"),
        new("oj", "Ojibwe", "ᐊᓂᔑᓈᐯᒧᐎᓐ"),
        new("om", "Oromo", "Afaan Oromoo"),
        new("or", "Oriya", "ଓଡ଼ିଆ"),
        new("os", "Ossetian", "ирон æвзаг"),
        new("pa", "Punjabi", "ਪੰਜਾਬੀ"),
        new("pi", "Pāli", "पाऴि"),
        new("pl", "Polish", "polski"),
        new("ps", "Pashto", "پښتو"),
        new("pt", "Portuguese", "Português"),
        new("qu", "Quechua", "Runa Simi"),
        new("rm", "Romansh", "rumantsch grischun"),
        new("rn", "Kirundi", "Ikirundi"),
        new("ro", "Romanian", "Română"),
        new("ru", "Russian", "Русский"),
        new("rw", "Kinyarwanda", "Ikinyarwanda"),
        new("sa", "Sanskrit", "संस्कृतम्"),
        new("sc", "Sardinian", "sardu"),
        new("sd", "Sindhi", "सिन्धी"),
        new("se", "Northern Sami", "Davvisámegiella"),
        new("sg", "Sango", "yângâ tî sängö"),
        new("si", "Sinhala", "සිංහල"),
        new("sk", "Slovak", "slovenčina"),
        new("sl", "Slovenian", "slovenščina"),
        new("sn", "Shona", "chiShona"),
        new("so", "Somali", "Soomaaliga"),
        new("sq", "Albanian", "Shqip"),
        new("sr", "Serbian", "српски језик"),
        new("ss", "Swati", "SiSwati"),
        new("st", "Southern Sotho", "Sesotho"),
        new("su", "Sundanese", "Basa Sunda"),
        new("sv", "Swedish", "Svenska"),
        new("sw", "Swahili", "Kiswahili"),
        new("ta", "Tamil", "தமிழ்"),
        new("te", "Telugu", "తెలుగు"),
        new("tg", "Tajik", "тоҷикӣ"),
        new("th", "Thai", "ไทย"),
        new("ti", "Tigrinya", "ትግርኛ"),
        new("tk", "Turkmen", "Türkmençe"),
        new("tl", "Tagalog", "Wikang Tagalog"),
        new("tn", "Tswana", "Setswana"),
        new("to", "Tonga", "faka Tonga"),
        new("tr", "Turkish", "Türkçe"),
        new("ts", "Tsonga", "Xitsonga"),
        new("tt", "Tatar", "татар теле"),
        new("tw", "Twi", "Twi"),
        new("ty", "Tahitian", "Reo Tahiti"),
        new("ug", "Uyghur", "ئۇيغۇرچە"),
        new("uk", "Ukrainian", "Українська"),
        new("ur", "Urdu", "اردو"),
        new("uz", "Uzbek", "Oʻzbek"),
        new("ve", "Venda", "Tshivenḓa"),
        new("vi", "Vietnamese", "Tiếng Việt"),
        new("vo", "Volapük", "Volapük"),
        new("wa", "Walloon", "walon"),
        new("wo", "Wolof", "Wollof"),
        new("xh", "Xhosa", "isiXhosa"),
        new("yi", "Yiddish", "ייִדיש"),
        new("yo", "Yoruba", "Yorùbá"),
        new("za", "Zhuang", "Saɯ cueŋƅ"),
        new("zh", "Chinese", "中文"),
        new("zh-CN", "Chinese (China)", "简体中文"),
        new("zh-HK", "Chinese (Hong Kong)", "繁體中文（香港）"),
        new("zh-TW", "Chinese (Taiwan)", "繁體中文（臺灣）"),
        new("zh-YUE", "Cantonese", "廣東話"),
        new("zu", "Zulu", "isiZulu"),

        new("ast", "Asturian", "asturianu"),
        new("chr", "Cherokee", "ᏣᎳᎩ ᎦᏬᏂᎯᏍᏗ"),
        new("ckb", "Sorani (Kurdish)", "سۆرانی"),
        new("cnr", "Montenegrin", "crnogorski"),
        new("csb", "Kashubian", "kaszëbsczi"),
        new("gsw", "Swiss German", "Schwiizertütsch"),
        new("jbo", "Lojban", "la .lojban."),
        new("kab", "Kabyle", "Taqbaylit"),
        new("ldn", "Láadan", "Láadan"),
        new("lfn", "Lingua Franca Nova", "lingua franca nova"),
        new("lzz", "Lazuri", "ლაზური ნენა"),
        new("moh", "Mohawk", "Kanienʼkéha"),
        new("nan-TW", "Hokkien (Taiwan)", "臺語 (Hô-ló話)"),
        new("nds", "Low German", "Plattdüütsch"),
        new("ota", "Ottoman Turkish", "لسان عثمانی"),
        new("pdc", "Pennsylvania Dutch", "Pennsilfaani-Deitsch"),
        new("sco", "Scots", "Scots"),
        new("sma", "Southern Sami", "Åarjelsaemien Gïele"),
        new("smj", "Lule Sami", "Julevsámegiella"),
        new("szl", "Silesian", "ślůnsko godka"),
        new("tok", "Toki Pona", "toki pona"),
        new("vai", "Vai", "ꕙꔤ"),
        new("xal", "Kalmyk", "Хальмг келн"),
        new("xmf", "Mingrelian", "მარგალური ნინა"),
        new("zba", "Balaibalan", "باليبلن"),
        new("zgh", "Standard Moroccan Tamazight", "ⵜⴰⵎⴰⵣⵉⵖⵜ"),
    ];

    /// <summary>
    ///     The language <paramref name="code" /> is the code of, or <see langword="null" /> where this client lists no
    ///     such code — which a post read off an instance may still carry, because the instance knew it.
    /// </summary>
    public static PostLanguage? Of(string code) =>
        All.FirstOrDefault(language => string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     The language <paramref name="name" /> spells — its code, its English name or its own name, in any case — or
    ///     <see langword="null" /> if it spells none of them.
    /// </summary>
    /// <remarks>
    ///     A code is asked before a name, so that a word that happens to be both (<c>vai</c>) reads as the code an author
    ///     typed rather than as some other language's name.
    /// </remarks>
    public static PostLanguage? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        return Of(trimmed)
               ?? All.FirstOrDefault(language =>
                   string.Equals(language.Name, trimmed, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(language.OwnName, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     The languages an author part of the way through typing <paramref name="typed" /> may mean, for the TUI's list:
    ///     those whose code begins with it, then those whose name in either language holds it. A code typed in full leads.
    ///     Nothing typed offers every language.
    /// </summary>
    public static IReadOnlyList<PostLanguage> Matching(string typed)
    {
        var wanted = typed.Trim();

        if (wanted.Length == 0)
        {
            return All;
        }

        var exact = All.Where(language => string.Equals(language.Code, wanted, StringComparison.OrdinalIgnoreCase));
        var byCode = All.Where(language => language.Code.StartsWith(wanted, StringComparison.OrdinalIgnoreCase));
        var byName = All.Where(language =>
            language.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)
            || language.OwnName.Contains(wanted, StringComparison.OrdinalIgnoreCase));

        return [.. exact.Concat(byCode).Concat(byName).Distinct()];
    }

    /// <summary>
    ///     How a value that is not a language is described, shared so that the flag turning one down and the config file
    ///     turning one down cannot say different things about the same word. There are too many to list, so it says how
    ///     one is spelled instead.
    /// </summary>
    public static string Rejection(string name) =>
        $"'{name}' is not a language this client knows. Use its ISO 639 code (fr), its name in English (French) "
        + "or its own name (Français).";
}
