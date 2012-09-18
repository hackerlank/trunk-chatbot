namespace RTParser.Utils
{
    /// <summary>
    /// Encapsulates all the required methods and attributes for any text transformation.
    /// 
    /// An input Unifiable is provided and various methods and attributes can be used to grab
    /// a transformed Unifiable.
    /// 
    /// The protected ProcessChange() method is abstract and should be overridden to contain 
    /// the code for transforming the input text into the output text.
    /// </summary   
    public abstract class TextTransformer : StaticAIMLUtils
    {
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="bot">The bot this transformer is a part of</param>
        /// <param name="inputString">The input Unifiable to be transformed</param>
        public TextTransformer(AltBot bot, Unifiable inputString)
        {
            this.Proc = bot;
            this.inputStringU = inputString;
            initialString = inputString.AsString();
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="bot">The bot this transformer is a part of</param>
        public TextTransformer(AltBot bot)
        {
            this.Proc = bot;
            this.inputStringU = Unifiable.Empty;
        }

        /// <summary>
        /// Default ctor for used as part of late binding mechanism
        /// </summary>
        public TextTransformer()
        {
            this.Proc = null;
            this.inputStringU = Unifiable.Empty;
        }

        public virtual float CallCanUnify(Unifiable with)
        {
            return InputStringU == with ? Unifiable.UNIFY_TRUE : Unifiable.UNIFY_FALSE;
        }

        /// <summary>
        /// Do a transformation on the supplied input Unifiable
        /// </summary>
        /// <param name="input">The Unifiable to be transformed</param>
        /// <returns>The resulting output</returns>
        public string Transform(string input)
        {
            this.inputStringU = Unifiable.MakeUnifiableFromString(input, false);
            return this.Transform();
        }

        /// <summary>
        /// Do a transformation on the Unifiable found in the InputString attribute
        /// </summary>
        /// <returns>The resulting transformed Unifiable</returns>
        public virtual string Transform()
        {
            if (!IsNullOrEmpty(this.inputStringU))
            {
                return this.ProcessAimlChange();
            }
            else
            {
                return Unifiable.Empty;
            }
        }

        public virtual Unifiable ProcessAimlChange()
        {
            return ProcessChange();
        }

        /// <summary>
        /// The method that does the actual processing of the text.
        /// </summary>
        /// <returns>The resulting processed text</returns>
        protected abstract Unifiable ProcessChange();

        public virtual Unifiable CompleteProcessU()
        {
            return inputStringU;
        }

        #region Attributes

        public string initialString;

        /// <summary>
        /// Instance of the input Unifiable
        /// </summary>
        protected Unifiable inputStringU;

        /// <summary>
        /// The Proc that this transformation is connected with
        /// </summary>
        public AltBot Proc;

        /// <summary>
        /// The input Unifiable to be transformed in some way
        /// </summary>
        public Unifiable InputStringU
        {
            get { return this.inputStringU; }
            set { this.inputStringU = value; }
        }

        /// <summary>
        /// The transformed Unifiable
        /// </summary>
        public Unifiable OutputString
        {
            get { return this.Transform(); }
        }

        #endregion
    }
}