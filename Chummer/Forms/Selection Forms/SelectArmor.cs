/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Chummer.Backend.Equipment;

// ReSharper disable LocalizableElement

namespace Chummer
{
    public partial class SelectArmor : Form
    {
        private string _strSelectedArmor = string.Empty;

        private bool _blnLoading = true;
        private bool _blnSkipUpdate;
        private bool _blnAddAgain;
        private static string _strSelectCategory = string.Empty;
        private decimal _decMarkup;
        private Armor _objSelectedArmor;

        private readonly XmlDocument _objXmlDocument;
        private readonly Character _objCharacter;

        private readonly List<ListItem> _lstCategory = Utils.ListItemListPool.Get();
        private readonly HashSet<string> _setBlackMarketMaps = Utils.StringHashSetPool.Get();
        private int _intRating;
        private bool _blnBlackMarketDiscount;

        #region Control Events

        public SelectArmor(Character objCharacter)
        {
            Disposed += (sender, args) =>
            {
                Utils.ListItemListPool.Return(_lstCategory);
                Utils.StringHashSetPool.Return(_setBlackMarketMaps);
            };
            InitializeComponent();
            this.UpdateLightDarkMode();
            this.TranslateWinForm();
            _objCharacter = objCharacter ?? throw new ArgumentNullException(nameof(objCharacter));
            // Load the Armor information.
            _objXmlDocument = objCharacter.LoadData("armor.xml");
            _setBlackMarketMaps.AddRange(objCharacter.GenerateBlackMarketMappings(
                                             objCharacter.LoadDataXPath("armor.xml")
                                                         .SelectSingleNodeAndCacheExpression("/chummer")));
        }

        private async void SelectArmor_Load(object sender, EventArgs e)
        {
            if (_objCharacter.Created)
            {
                await chkHideOverAvailLimit.DoThreadSafeAsync(x =>
                {
                    x.Visible = false;
                    x.Checked = false;
                }).ConfigureAwait(false);
                await lblMarkupLabel.DoThreadSafeAsync(x => x.Visible = true).ConfigureAwait(false);
                await nudMarkup.DoThreadSafeAsync(x => x.Visible = true).ConfigureAwait(false);
                await lblMarkupPercentLabel.DoThreadSafeAsync(x => x.Visible = true).ConfigureAwait(false);
            }
            else
            {
                await chkHideOverAvailLimit.DoThreadSafeAsync(x =>
                {
                    x.Text = string.Format(
                        GlobalSettings.CultureInfo, x.Text,
                        _objCharacter.Settings.MaximumAvailability);
                    x.Checked = GlobalSettings.HideItemsOverAvailLimit;
                }).ConfigureAwait(false);
                await lblMarkupLabel.DoThreadSafeAsync(x => x.Visible = false).ConfigureAwait(false);
                await nudMarkup.DoThreadSafeAsync(x => x.Visible = false).ConfigureAwait(false);
                await lblMarkupPercentLabel.DoThreadSafeAsync(x => x.Visible = false).ConfigureAwait(false);
            }

            DataGridViewCellStyle dataGridViewNuyenCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.TopRight,
                Format = _objCharacter.Settings.NuyenFormat + await LanguageManager.GetStringAsync("String_NuyenSymbol").ConfigureAwait(false),
                NullValue = null
            };
            Cost.DefaultCellStyle = dataGridViewNuyenCellStyle;

            // Populate the Armor Category list.
            XmlNodeList objXmlCategoryList = _objXmlDocument.SelectNodes("/chummer/categories/category");
            if (objXmlCategoryList != null)
            {
                string strFilterPrefix = "/chummer/armors/armor[(" + await _objCharacter.Settings.BookXPathAsync().ConfigureAwait(false) + ") and category = ";
                foreach (XmlNode objXmlCategory in objXmlCategoryList)
                {
                    string strInnerText = objXmlCategory.InnerText;
                    if (_objXmlDocument.SelectSingleNode(strFilterPrefix + strInnerText.CleanXPath() + ']') != null)
                    {
                        _lstCategory.Add(new ListItem(strInnerText,
                            objXmlCategory.Attributes?["translate"]?.InnerText ?? strInnerText));
                    }
                }
            }
            _lstCategory.Sort(CompareListItems.CompareNames);

            if (_lstCategory.Count > 0)
            {
                _lstCategory.Insert(0, new ListItem("Show All", await LanguageManager.GetStringAsync("String_ShowAll").ConfigureAwait(false)));
            }
            
            await cboCategory.PopulateWithListItemsAsync(_lstCategory).ConfigureAwait(false);
            await cboCategory.DoThreadSafeAsync(x =>
            {
                // Select the first Category in the list.
                if (!string.IsNullOrEmpty(_strSelectCategory))
                    x.SelectedValue = _strSelectCategory;
                if (x.SelectedIndex == -1)
                    x.SelectedIndex = 0;
            }).ConfigureAwait(false);
            await chkBlackMarketDiscount.DoThreadSafeAsync(x => x.Visible = _objCharacter.BlackMarketDiscount).ConfigureAwait(false);

            _blnLoading = false;

            await RefreshList().ConfigureAwait(false);
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            _blnAddAgain = false;
            AcceptForm();
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private async void lstArmor_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_blnLoading)
                return;

            XmlNode xmlArmor = null;
            string strSelectedId = await lstArmor.DoThreadSafeFuncAsync(x => x.SelectedValue?.ToString()).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(strSelectedId))
                xmlArmor = _objXmlDocument.SelectSingleNode("/chummer/armors/armor[id = " + strSelectedId.CleanXPath() + ']');
            if (xmlArmor != null)
            {
                // Create the Armor so we can show its Total Avail (some Armor includes Chemical Seal which adds +6 which wouldn't be factored in properly otherwise).
                Armor objArmor = new Armor(_objCharacter);
                List<Weapon> lstWeapons = new List<Weapon>(1);
                objArmor.Create(xmlArmor, 0, lstWeapons, true, true, true);

                if (_objSelectedArmor != null)
                    await _objSelectedArmor.DisposeAsync().ConfigureAwait(false);
                _objSelectedArmor = objArmor;

                int intRating = 0;
                if (xmlArmor.TryGetInt32FieldQuickly("rating", ref intRating))
                {
                    await nudRating.DoThreadSafeAsync(x => x.Maximum = intRating).ConfigureAwait(false);
                    if (await chkHideOverAvailLimit.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        int intMaximum = await nudRating.DoThreadSafeFuncAsync(x => x.MaximumAsInt).ConfigureAwait(false);
                        while (intMaximum > 1 && !await SelectionShared.CheckAvailRestrictionAsync(xmlArmor, _objCharacter, intMaximum).ConfigureAwait(false))
                        {
                            --intMaximum;
                        }
                        await nudRating.DoThreadSafeAsync(x => x.Maximum = intMaximum).ConfigureAwait(false);
                    }

                    if (await chkShowOnlyAffordItems.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false) && !await chkFreeItem.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        decimal decCostMultiplier = 1 + (await nudMarkup.DoThreadSafeFuncAsync(x => x.Value).ConfigureAwait(false) / 100.0m);
                        if (_setBlackMarketMaps.Contains(xmlArmor.SelectSingleNode("category")?.Value))
                            decCostMultiplier *= 0.9m;
                        int intMaximum = await nudRating.DoThreadSafeFuncAsync(x => x.MaximumAsInt).ConfigureAwait(false);
                        while (intMaximum > 1 && !await SelectionShared.CheckNuyenRestrictionAsync(xmlArmor, _objCharacter.Nuyen, decCostMultiplier, intMaximum).ConfigureAwait(false))
                        {
                            --intMaximum;
                        }
                        await nudRating.DoThreadSafeAsync(x => x.Maximum = intMaximum).ConfigureAwait(false);
                    }
                    await lblRatingLabel.DoThreadSafeAsync(x => x.Visible = true).ConfigureAwait(false);
                    await nudRating.DoThreadSafeAsync(x =>
                    {
                        x.Minimum = 1;
                        x.Value = 1;
                        x.Enabled = x.Minimum != x.Maximum;
                        x.Visible = true;
                    }).ConfigureAwait(false);
                    await lblRatingNALabel.DoThreadSafeAsync(x => x.Visible = false).ConfigureAwait(false);
                }
                else
                {
                    await lblRatingLabel.DoThreadSafeAsync(x => x.Visible = true).ConfigureAwait(false);
                    await lblRatingNALabel.DoThreadSafeAsync(x => x.Visible = true).ConfigureAwait(false);
                    await nudRating.DoThreadSafeAsync(x =>
                    {
                        x.Minimum = 0;
                        x.Maximum = 0;
                        x.Value = 0;
                        x.Enabled = false;
                        x.Visible = false;
                    }).ConfigureAwait(false);
                }

                string strRatingLabel = xmlArmor.SelectSingleNode("ratinglabel")?.Value;
                strRatingLabel = !string.IsNullOrEmpty(strRatingLabel)
                    ? string.Format(GlobalSettings.CultureInfo, await LanguageManager.GetStringAsync("Label_RatingFormat").ConfigureAwait(false),
                                    await LanguageManager.GetStringAsync(strRatingLabel).ConfigureAwait(false))
                    : await LanguageManager.GetStringAsync("Label_Rating").ConfigureAwait(false);
                await lblRatingLabel.DoThreadSafeAsync(x => x.Text = strRatingLabel).ConfigureAwait(false);
            }
            else
            {
                if (_objSelectedArmor != null)
                    await _objSelectedArmor.DisposeAsync().ConfigureAwait(false);
                _objSelectedArmor = null;
                await lblRatingLabel.DoThreadSafeAsync(x => x.Visible = false).ConfigureAwait(false);
                await lblRatingNALabel.DoThreadSafeAsync(x => x.Visible = false).ConfigureAwait(false);
                await nudRating.DoThreadSafeAsync(x =>
                {
                    x.Visible = false;
                    x.Enabled = false;
                    x.Minimum = 0;
                    x.Value = 0;
                }).ConfigureAwait(false);
            }

            UpdateArmorInfo();
        }

        private async void RefreshCurrentList(object sender, EventArgs e)
        {
            await RefreshList().ConfigureAwait(false);
        }

        private void cmdOKAdd_Click(object sender, EventArgs e)
        {
            _blnAddAgain = true;
            AcceptForm();
        }

        private async void txtSearch_TextChanged(object sender, EventArgs e)
        {
            await RefreshList().ConfigureAwait(false);
        }

        private async void chkFreeItem_CheckedChanged(object sender, EventArgs e)
        {
            if (await chkShowOnlyAffordItems.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
            {
                await RefreshList().ConfigureAwait(false);
            }
            UpdateArmorInfo();
        }

        private void chkBlackMarketDiscount_CheckedChanged(object sender, EventArgs e)
        {
            UpdateArmorInfo();
        }

        private void nudRating_ValueChanged(object sender, EventArgs e)
        {
            UpdateArmorInfo();
        }

        private async void nudMarkup_ValueChanged(object sender, EventArgs e)
        {
            if (await chkShowOnlyAffordItems.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false) && !await chkFreeItem.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
            {
                await RefreshList().ConfigureAwait(false);
            }
            UpdateArmorInfo();
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Down when lstArmor.SelectedIndex + 1 < lstArmor.Items.Count:
                    ++lstArmor.SelectedIndex;
                    break;

                case Keys.Down:
                    {
                        if (lstArmor.Items.Count > 0)
                        {
                            lstArmor.SelectedIndex = 0;
                        }

                        break;
                    }
                case Keys.Up when lstArmor.SelectedIndex - 1 >= 0:
                    --lstArmor.SelectedIndex;
                    break;

                case Keys.Up:
                    {
                        if (lstArmor.Items.Count > 0)
                        {
                            lstArmor.SelectedIndex = lstArmor.Items.Count - 1;
                        }

                        break;
                    }
            }
        }

        private void txtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
                txtSearch.Select(txtSearch.Text.Length, 0);
        }

        private async void tmrSearch_Tick(object sender, EventArgs e)
        {
            tmrSearch.Stop();
            tmrSearch.Enabled = false;

            await RefreshList().ConfigureAwait(false);
        }

        private void dgvArmor_DoubleClick(object sender, EventArgs e)
        {
            AcceptForm();
        }

        #endregion Control Events

        #region Properties

        /// <summary>
        /// Whether or not the user wants to add another item after this one.
        /// </summary>
        public bool AddAgain => _blnAddAgain;

        /// <summary>
        /// Whether or not the selected Vehicle is used.
        /// </summary>
        public bool BlackMarketDiscount => _blnBlackMarketDiscount;

        /// <summary>
        /// Armor that was selected in the dialogue.
        /// </summary>
        public string SelectedArmor => _strSelectedArmor;

        /// <summary>
        /// Whether or not the item should be added for free.
        /// </summary>
        public bool FreeCost => chkFreeItem.Checked;

        /// <summary>
        /// Markup percentage.
        /// </summary>
        public decimal Markup => _decMarkup;

        /// <summary>
        /// Markup percentage.
        /// </summary>
        public int Rating => _intRating;

        #endregion Properties

        #region Methods

        /// <summary>
        /// Refreshes the displayed lists
        /// </summary>
        private async ValueTask RefreshList(CancellationToken token = default)
        {
            if (_blnLoading)
                return;

            using (new FetchSafelyFromPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdFilter))
            {
                sbdFilter.Append('(').Append(await _objCharacter.Settings.BookXPathAsync(token: token).ConfigureAwait(false)).Append(')');

                string strCategory = cboCategory.SelectedValue?.ToString();
                if (!string.IsNullOrEmpty(strCategory) && strCategory != "Show All"
                                                       && (GlobalSettings.SearchInCategoryOnly
                                                           || await txtSearch.DoThreadSafeFuncAsync(x => x.TextLength == 0, token: token).ConfigureAwait(false)))
                    sbdFilter.Append(" and category = ").Append(strCategory.CleanXPath());
                else
                {
                    using (new FetchSafelyFromPool<StringBuilder>(Utils.StringBuilderPool,
                                                                  out StringBuilder sbdCategoryFilter))
                    {
                        foreach (string strItem in _lstCategory.Select(x => x.Value))
                        {
                            if (!string.IsNullOrEmpty(strItem))
                                sbdCategoryFilter.Append("category = ").Append(strItem.CleanXPath()).Append(" or ");
                        }

                        if (sbdCategoryFilter.Length > 0)
                        {
                            sbdCategoryFilter.Length -= 4;
                            sbdFilter.Append(" and (").Append(sbdCategoryFilter).Append(')');
                        }
                    }
                }

                string strSearch = await txtSearch.DoThreadSafeFuncAsync(x => x.Text, token: token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(strSearch))
                    sbdFilter.Append(" and ").Append(CommonFunctions.GenerateSearchXPath(strSearch));

                await BuildArmorList(_objXmlDocument.SelectNodes("/chummer/armors/armor[" + sbdFilter + ']'), token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Builds the list of Armors to render in the active tab.
        /// </summary>
        /// <param name="objXmlArmorList">XmlNodeList of Armors to render.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        private async ValueTask BuildArmorList(XmlNodeList objXmlArmorList, CancellationToken token = default)
        {
            decimal decBaseMarkup = 1 + (await nudMarkup.DoThreadSafeFuncAsync(x => x.Value, token: token).ConfigureAwait(false) / 100.0m);
            bool blnHideOverAvailLimit = await chkHideOverAvailLimit.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false);
            bool blnFreeItem = await chkFreeItem.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false);
            bool blnShowOnlyAffordItems = await chkShowOnlyAffordItems.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false);
            switch (await tabControl.DoThreadSafeFuncAsync(x => x.SelectedIndex, token: token).ConfigureAwait(false))
            {
                case 1:
                    DataTable tabArmor = new DataTable("armor");
                    tabArmor.Columns.Add("ArmorGuid");
                    tabArmor.Columns.Add("ArmorName");
                    tabArmor.Columns.Add("Armor");
                    tabArmor.Columns["Armor"].DataType = typeof(int);
                    tabArmor.Columns.Add("Capacity");
                    tabArmor.Columns["Capacity"].DataType = typeof(decimal);
                    tabArmor.Columns.Add("Avail");
                    tabArmor.Columns["Avail"].DataType = typeof(AvailabilityValue);
                    tabArmor.Columns.Add("Special");
                    tabArmor.Columns.Add("Source");
                    tabArmor.Columns["Source"].DataType = typeof(SourceString);
                    tabArmor.Columns.Add("Cost");
                    tabArmor.Columns["Cost"].DataType = typeof(NuyenString);

                    // Populate the Armor list.
                    foreach (XmlNode objXmlArmor in objXmlArmorList)
                    {
                        decimal decCostMultiplier = decBaseMarkup;
                        if (_setBlackMarketMaps.Contains(objXmlArmor["category"]?.InnerText))
                            decCostMultiplier *= 0.9m;
                        if (!blnHideOverAvailLimit
                            || await SelectionShared.CheckAvailRestrictionAsync(objXmlArmor, _objCharacter, token: token).ConfigureAwait(false) && (blnFreeItem
                                || !blnShowOnlyAffordItems
                                || await SelectionShared.CheckNuyenRestrictionAsync(
                                    objXmlArmor, _objCharacter.Nuyen, decCostMultiplier, token: token).ConfigureAwait(false)))
                        {
                            using (Armor objArmor = new Armor(_objCharacter))
                            {
                                List<Weapon> lstWeapons = new List<Weapon>(1);
                                objArmor.Create(objXmlArmor, 0, lstWeapons, true, true, true);

                                string strArmorGuid = objArmor.SourceIDString;
                                string strArmorName = objArmor.CurrentDisplayName;
                                int intArmor = objArmor.TotalArmor;
                                decimal decCapacity
                                    = Convert.ToDecimal(objArmor.CalculatedCapacity(GlobalSettings.InvariantCultureInfo), GlobalSettings.InvariantCultureInfo);
                                AvailabilityValue objAvail = objArmor.TotalAvailTuple();
                                using (new FetchSafelyFromPool<StringBuilder>(Utils.StringBuilderPool,
                                                                              out StringBuilder sbdAccessories))
                                {
                                    foreach (ArmorMod objMod in objArmor.ArmorMods)
                                    {
                                        sbdAccessories.AppendLine(objMod.CurrentDisplayName);
                                    }

                                    foreach (Gear objGear in objArmor.GearChildren)
                                    {
                                        sbdAccessories.AppendLine(objGear.CurrentDisplayName);
                                    }

                                    if (sbdAccessories.Length > 0)
                                        sbdAccessories.Length -= Environment.NewLine.Length;
                                    SourceString strSource = await SourceString.GetSourceStringAsync(
                                        objArmor.Source, await objArmor.DisplayPageAsync(GlobalSettings.Language, token).ConfigureAwait(false),
                                        GlobalSettings.Language, GlobalSettings.CultureInfo, _objCharacter, token).ConfigureAwait(false);
                                    NuyenString strCost = new NuyenString(objArmor.DisplayCost(out decimal _, false));

                                    tabArmor.Rows.Add(strArmorGuid, strArmorName, intArmor, decCapacity, objAvail,
                                                      sbdAccessories.ToString(), strSource, strCost);
                                }
                            }
                        }
                    }

                    DataSet set = new DataSet("armor");
                    set.Tables.Add(tabArmor);

                    dgvArmor.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                    dgvArmor.DataSource = set;
                    dgvArmor.DataMember = "armor";
                    break;

                default:
                    using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool,
                                                                   out List<ListItem> lstArmors))
                    {
                        int intOverLimit = 0;
                        string strSpace = await LanguageManager.GetStringAsync("String_Space", token: token).ConfigureAwait(false);
                        foreach (XmlNode objXmlArmor in objXmlArmorList)
                        {
                            decimal decCostMultiplier = decBaseMarkup;
                            if (_setBlackMarketMaps.Contains(objXmlArmor["category"]?.InnerText))
                                decCostMultiplier *= 0.9m;
                            if ((!blnHideOverAvailLimit
                                 || await SelectionShared.CheckAvailRestrictionAsync(objXmlArmor, _objCharacter, token: token).ConfigureAwait(false))
                                && (blnFreeItem
                                    || !blnShowOnlyAffordItems
                                    || (await SelectionShared.CheckNuyenRestrictionAsync(
                                        objXmlArmor, _objCharacter.Nuyen, decCostMultiplier, token: token).ConfigureAwait(false))))
                            {
                                string strDisplayName = objXmlArmor["translate"]?.InnerText
                                                        ?? objXmlArmor["name"]?.InnerText;
                                if (!GlobalSettings.SearchInCategoryOnly && txtSearch.TextLength != 0)
                                {
                                    string strCategory = objXmlArmor["category"]?.InnerText;
                                    if (!string.IsNullOrEmpty(strCategory))
                                    {
                                        ListItem objFoundItem
                                            = _lstCategory.Find(objFind => objFind.Value.ToString() == strCategory);
                                        if (!string.IsNullOrEmpty(objFoundItem.Name))
                                        {
                                            strDisplayName += strSpace + '[' + objFoundItem.Name + ']';
                                        }
                                    }
                                }

                                lstArmors.Add(new ListItem(objXmlArmor["id"]?.InnerText, strDisplayName));
                            }
                            else
                                ++intOverLimit;
                        }

                        lstArmors.Sort(CompareListItems.CompareNames);
                        if (intOverLimit > 0)
                        {
                            // Add after sort so that it's always at the end
                            lstArmors.Add(new ListItem(string.Empty,
                                                       string.Format(GlobalSettings.CultureInfo,
                                                                     await LanguageManager.GetStringAsync(
                                                                         "String_RestrictedItemsHidden", token: token).ConfigureAwait(false),
                                                                     intOverLimit)));
                        }

                        _blnLoading = true;
                        string strOldSelected = await lstArmor.DoThreadSafeFuncAsync(x => x.SelectedValue?.ToString(), token: token).ConfigureAwait(false);
                        await lstArmor.PopulateWithListItemsAsync(lstArmors, token: token).ConfigureAwait(false);
                        _blnLoading = false;
                        if (!string.IsNullOrEmpty(strOldSelected))
                            await lstArmor.DoThreadSafeAsync(x => x.SelectedValue = strOldSelected, token: token).ConfigureAwait(false);
                        else
                            await lstArmor.DoThreadSafeAsync(x => x.SelectedIndex = -1, token: token).ConfigureAwait(false);
                        break;
                    }
            }
        }

        /// <summary>
        /// Accept the selected item and close the form.
        /// </summary>
        private void AcceptForm()
        {
            string strSelectedId = string.Empty;
            switch (tabControl.SelectedIndex)
            {
                case 0:
                    strSelectedId = lstArmor.SelectedValue?.ToString();
                    break;

                case 1:
                    strSelectedId = dgvArmor.SelectedRows[0].Cells[0].Value?.ToString();
                    break;
            }
            if (!string.IsNullOrEmpty(strSelectedId))
            {
                _strSelectCategory = (GlobalSettings.SearchInCategoryOnly || txtSearch.TextLength == 0)
                    ? cboCategory.SelectedValue?.ToString()
                    : _objXmlDocument.SelectSingleNode("/chummer/armors/armor[id = " + strSelectedId.CleanXPath() + "]/category")?.InnerText;
                _strSelectedArmor = strSelectedId;
                _decMarkup = nudMarkup.Value;
                _intRating = nudRating.ValueAsInt;
                _blnBlackMarketDiscount = chkBlackMarketDiscount.Checked;

                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void UpdateArmorInfo()
        {
            if (_blnLoading || _blnSkipUpdate)
                return;

            _blnSkipUpdate = true;
            if (_objSelectedArmor != null)
            {
                bool blnCanBlackMarketDiscount = _setBlackMarketMaps.Contains(_objSelectedArmor.Category);
                chkBlackMarketDiscount.Enabled = blnCanBlackMarketDiscount;
                if (!chkBlackMarketDiscount.Checked)
                {
                    chkBlackMarketDiscount.Checked = GlobalSettings.AssumeBlackMarket && blnCanBlackMarketDiscount;
                }
                else if (!blnCanBlackMarketDiscount)
                {
                    //Prevent chkBlackMarketDiscount from being checked if the category doesn't match.
                    chkBlackMarketDiscount.Checked = false;
                }

                _objSelectedArmor.DiscountCost = chkBlackMarketDiscount.Checked;
                _objSelectedArmor.Rating = nudRating.ValueAsInt;

                lblSource.Text = _objSelectedArmor.SourceDetail.ToString();
                lblSource.SetToolTip(_objSelectedArmor.SourceDetail.LanguageBookTooltip);
                lblSourceLabel.Visible = !string.IsNullOrEmpty(lblSource.Text);

                lblArmorValueLabel.Visible = true;
                lblArmorValue.Text = _objSelectedArmor.DisplayArmorValue;
                lblCapacityLabel.Visible = true;
                lblCapacity.Text = _objSelectedArmor.DisplayCapacity;

                lblCostLabel.Visible = true;
                decimal decItemCost = 0;
                if (chkFreeItem.Checked)
                {
                    lblCost.Text = (0.0m).ToString(_objCharacter.Settings.NuyenFormat, GlobalSettings.CultureInfo) + LanguageManager.GetString("String_NuyenSymbol");
                }
                else
                {
                    lblCost.Text = _objSelectedArmor.DisplayCost(out decItemCost, true, nudMarkup.Value / 100.0m);
                }

                AvailabilityValue objTotalAvail = _objSelectedArmor.TotalAvailTuple();
                lblAvailLabel.Visible = true;
                lblTestLabel.Visible = true;
                lblAvail.Text = objTotalAvail.ToString();
                lblTest.Text = _objCharacter.AvailTest(decItemCost, objTotalAvail);
            }
            else
            {
                chkBlackMarketDiscount.Enabled = false;
                chkBlackMarketDiscount.Checked = false;
                lblSourceLabel.Visible = false;
                lblSource.Text = string.Empty;
                lblSource.SetToolTip(string.Empty);

                lblArmorValueLabel.Visible = false;
                lblArmorValue.Text = string.Empty;
                lblCapacityLabel.Visible = false;
                lblCapacity.Text = string.Empty;
                lblCostLabel.Visible = false;
                lblCost.Text = string.Empty;
                lblAvailLabel.Visible = false;
                lblTestLabel.Visible = false;
                lblAvail.Text = string.Empty;
                lblTest.Text = string.Empty;
            }
            _blnSkipUpdate = false;
        }

        private async void OpenSourceFromLabel(object sender, EventArgs e)
        {
            await CommonFunctions.OpenPdfFromControl(sender).ConfigureAwait(false);
        }

        #endregion Methods
    }
}
