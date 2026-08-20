namespace Tourmaline26.Logic
{
    /// <summary>
    /// Información difundida a los pasajeros (mensaje pregrabado o editado manualmente).
    /// Título y cuerpo admiten hasta <see cref="LanguageCount"/> idiomas separados por '|'.
    /// </summary>
    public sealed class PassengerInformation
    {
        /// <summary>Número fijo de variantes lingüísticas (p. ej. ca|es|en).</summary>
        public const int LanguageCount = 3;

        private readonly string[] mcolTitles = CreateEmptySlots();
        private readonly string[] mcolTexts = CreateEmptySlots();
        private int mvarLanguageIndex;
        private string? mvarIconKey;

        /// <summary>Descripción interna del mensaje (no se muestra en el popup).</summary>
        public string Comment { get; set; } = "Descripción de este mensaje";

        /// <summary>Importancia media: el anuncio sustituye el LED interior.</summary>
        public const byte MediumImportance = 128;

        /// <summary>Importancia alta: el anuncio sustituye LED interior y exterior.</summary>
        public const byte HighImportance = 201;

        /// <summary>
        /// Tamaño relativo del popup: 0 = no visible, 255 = toda la superficie.
        /// En LED: ≥ <see cref="MediumImportance"/> interior;
        /// ≥ <see cref="HighImportance"/> interior y exterior.
        /// </summary>
        public byte Importance { get; set; } = 128;

        /// <summary>El anuncio activo debe pintarse en el LED interior.</summary>
        public bool ShowsOnInteriorLed => Importance >= MediumImportance;

        /// <summary>El anuncio activo debe pintarse también en el LED exterior.</summary>
        public bool ShowsOnExteriorLed => Importance >= HighImportance;

        /// <summary>
        /// Clave de icono para <c>ColorIcon</c>. Null o vacío = sin icono.
        /// </summary>
        public string? IconKey
        {
            get => mvarIconKey;
            set => mvarIconKey = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// Idioma activo (0 .. <see cref="LanguageCount"/>-1). Valores fuera de rango se acotan.
        /// </summary>
        public int LanguageIndex
        {
            get => mvarLanguageIndex;
            set => mvarLanguageIndex = ClampLanguage(value);
        }

        /// <summary>
        /// Asigna los títulos multiidioma. Formato: <c>"título0|título1|título2"</c>.
        /// Si faltan partes se rellenan con vacío; si sobran se ignoran.
        /// </summary>
        public string TitleText
        {
            get => PackSlots(mcolTitles);
            set => AssignSlots(mcolTitles, value);
        }

        /// <summary>
        /// Asigna los cuerpos multiidioma. Formato: <c>"texto0|texto1|texto2"</c>.
        /// Si faltan partes se rellenan con vacío; si sobran se ignoran.
        /// </summary>
        public string MessageText
        {
            get => PackSlots(mcolTexts);
            set => AssignSlots(mcolTexts, value);
        }

        /// <summary>Título del idioma activo.</summary>
        public string CurrentTitle => mcolTitles[mvarLanguageIndex];

        /// <summary>Cuerpo del idioma activo.</summary>
        public string CurrentText => mcolTexts[mvarLanguageIndex];

        /// <summary>Indica si el mensaje debe mostrarse (Importance &gt; 0).</summary>
        public bool IsVisible => Importance > 0;

        /// <summary>Título en el idioma indicado (índice acotado de forma segura).</summary>
        public string GetTitle(int languageIndex) => mcolTitles[ClampLanguage(languageIndex)];

        /// <summary>Cuerpo en el idioma indicado (índice acotado de forma segura).</summary>
        public string GetText(int languageIndex) => mcolTexts[ClampLanguage(languageIndex)];

        /// <summary>
        /// Copia defensiva de los títulos (siempre <see cref="LanguageCount"/> elementos no nulos).
        /// </summary>
        public string[] GetTitlesCopy() => (string[])mcolTitles.Clone();

        /// <summary>
        /// Copia defensiva de los cuerpos (siempre <see cref="LanguageCount"/> elementos no nulos).
        /// </summary>
        public string[] GetTextsCopy() => (string[])mcolTexts.Clone();

        /// <summary>Copia profunda del mensaje (arrays incluidos).</summary>
        public PassengerInformation Clone()
        {
            var clone = new PassengerInformation
            {
                Comment = Comment,
                Importance = Importance,
                IconKey = IconKey,
                LanguageIndex = LanguageIndex,
                TitleText = TitleText,
                MessageText = MessageText
            };
            return clone;
        }

        private static string[] CreateEmptySlots()
        {
            var mcolSlots = new string[LanguageCount];
            for (int i = 0; i < LanguageCount; i++)
                mcolSlots[i] = string.Empty;
            return mcolSlots;
        }

        private static int ClampLanguage(int languageIndex) =>
            Math.Clamp(languageIndex, 0, LanguageCount - 1);

        private static void AssignSlots(string[] mcolTarget, string? packed)
        {
            // Nunca reasignamos el array: tamaño fijo y referencias internas estables.
            var mcolParts = (packed ?? string.Empty).Split('|');
            for (int i = 0; i < LanguageCount; i++)
            {
                if (i < mcolParts.Length && mcolParts[i] is not null)
                    mcolTarget[i] = mcolParts[i].Trim();
                else
                    mcolTarget[i] = string.Empty;
            }
        }

        private static string PackSlots(string[] mcolSource) =>
            string.Join("|", mcolSource);
    }
}
