using Chummer.Backend.Skills;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using NLog;

namespace Chummer
{
    /// <summary>
    /// A Sustained Magician Spell
    /// </summary>
    [HubClassTag("SourceID", true, "Name", "Extra")]
    [DebuggerDisplay("{DisplayName(GlobalOptions.DefaultLanguage)}")]

    public class SustainedSpell : Spell, IHasInternalId, IHasName, IHasXmlNode, ISustainable
    {
        private bool _blnSelfSustained = true;
        private int _intForce = 0;
        private int _intNetHits = 0;



        #region Constructor, Create, Save, Load, and Print Methods

        public SustainedSpell(Character objCharacter) : base(objCharacter)
        {
            //Create the GUID for new sustained spells
            guiID = Guid.NewGuid();
        }

        public void Create(Spell spellRef)
        {
            guiSourceID = spellRef.SourceID;
            Name = spellRef.Name;
        }



        /// <summary>
        /// Print the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter"></param>
        /// <param name="objCulture"></param>
        /// <param name="strLanguageToPrint"></param>
        public override void Print(XmlTextWriter objWriter, CultureInfo objCulture, string strLanguageToPrint)
        {
            if (objWriter == null)
                return;
            objWriter.WriteStartElement("sustainedobject");
            objWriter.WriteElementString("type", nameof(SustainedSpell));
            objWriter.WriteElementString("guid", InternalId);
            objWriter.WriteElementString("sourceid", SourceIDString);
            if (Limited)
                objWriter.WriteElementString("name", string.Format(objCulture, "{0}{1}({2})",
                    DisplayNameShort(strLanguageToPrint), LanguageManager.GetString("String_Space", strLanguageToPrint), LanguageManager.GetString("String_SpellLimited", strLanguageToPrint)));
            else if (Alchemical)
                objWriter.WriteElementString("name", string.Format(objCulture, "{0}{1}({2})",
                    DisplayNameShort(strLanguageToPrint), LanguageManager.GetString("String_Space", strLanguageToPrint), LanguageManager.GetString("String_SpellAlchemical", strLanguageToPrint)));
            else
                objWriter.WriteElementString("name", DisplayNameShort(strLanguageToPrint));
            objWriter.WriteElementString("name_english", Name);
            objWriter.WriteElementString("force", _intForce.ToString(GlobalOptions.InvariantCultureInfo));
            objWriter.WriteElementString("nethits", _intNetHits.ToString(GlobalOptions.InvariantCultureInfo));
            objWriter.WriteElementString("self", _blnSelfSustained.ToString(GlobalOptions.InvariantCultureInfo));
            objWriter.WriteEndElement();
        }

        /// <summary>
        /// Load the Spell from the XmlNode.
        /// </summary>
        /// <param name="objNode"></param>
        public override void Load(XmlNode objNode)
        {
            if (objNode == null)
                return;
            if (!objNode.TryGetField("guid", Guid.TryParse, out guiID))
            {
                guiID = Guid.NewGuid();
            }
            objNode.TryGetStringFieldQuickly("name", ref strName);
            if (!objNode.TryGetGuidFieldQuickly("sourceid", ref guiSourceID))
            {
                XmlNode node = GetNode(GlobalOptions.Language);
                node?.TryGetGuidFieldQuickly("id", ref guiSourceID);
            }
            objNode.TryGetInt32FieldQuickly("force", ref _intForce);
            objNode.TryGetInt32FieldQuickly("nethits", ref _intNetHits);
            objNode.TryGetBoolFieldQuickly("self", ref _blnSelfSustained);
        }

        /// <summary>
        /// Save the objects xml to the XmlWriter, used for Sustained spells only!
        /// </summary>
        /// <param name="objWriter"></param>
        public override void Save(XmlTextWriter objWriter)
        {
            if (objWriter == null)
                return;
            objWriter.WriteStartElement("sustainedobject");
            objWriter.WriteElementString("type", nameof(SustainedSpell));
            objWriter.WriteElementString("sourceid", SourceIDString);
            objWriter.WriteElementString("guid", InternalId);
            objWriter.WriteElementString("name", Name);
            objWriter.WriteElementString("self", _blnSelfSustained.ToString(GlobalOptions.InvariantCultureInfo));
            objWriter.WriteElementString("force", _intForce.ToString(GlobalOptions.InvariantCultureInfo));
            objWriter.WriteElementString("nethits", _intNetHits.ToString(GlobalOptions.InvariantCultureInfo));
            objWriter.WriteEndElement();
        }

        #endregion

        #region Properties
        /// <summary>
        /// Is the spell sustained by yourself?
        /// </summary>
        public bool SelfSustained
        {
            get => _blnSelfSustained;
            set
            {
                if (_blnSelfSustained != value)
                {
                    _blnSelfSustained = value;
                    objCharacter.OnPropertyChanged("SustainingPenalty");
                }

            }
            
        }

        /// <summary>
        /// Force of the sustained spell
        /// </summary>
        public int Force
        {
            get => _intForce;
            set => _intForce = value;
        }

        /// <summary>
        /// The Net Hits the Sustained Spell has
        /// </summary>
        public int NetHits
        {
            get => _intNetHits;
            set => _intNetHits = value;
        }

        #endregion

        #region Helper Methods
        #endregion
    }
}
