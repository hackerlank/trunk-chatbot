using System;
using System.Text.RegularExpressions;
using System.Text;

namespace RTParser.Normalize
{
    /// <summary>
    /// Strips any illegal characters found within the input Unifiable. Illegal characters are referenced from
    /// the bot's Strippers regex that is defined in the setup XML file.
    /// </summary>
    public class StripIllegalCharacters : RTParser.Utils.TextTransformer
    {
        public StripIllegalCharacters(RTParser.AltBot bot, Unifiable inputString) : base(bot, inputString)
        { }

        public StripIllegalCharacters(RTParser.AltBot bot)
            : base(bot) 
        { }

        protected override Unifiable ProcessChange()
        {
            string newVariable = ((string)inputStringU);
            if (newVariable != null)
            {
                if (newVariable.StartsWith("TAG-")) return inputStringU;
            }
            return this.Proc.Strippers.Replace(this.inputStringU, " ");
        }
    }
}
