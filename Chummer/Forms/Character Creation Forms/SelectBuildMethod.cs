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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chummer
{
    public sealed partial class SelectBuildMethod : Form
    {
        private readonly Character _objCharacter;
        private readonly CharacterBuildMethod _eStartingBuildMethod;
        private readonly bool _blnForExistingCharacter;

        #region Control Events

        public SelectBuildMethod(Character objCharacter, bool blnUseCurrentValues = false)
        {
            _objCharacter = objCharacter ?? throw new ArgumentNullException(nameof(objCharacter));
            _eStartingBuildMethod = _objCharacter.Settings.BuildMethod;
            _blnForExistingCharacter = blnUseCurrentValues;
            InitializeComponent();
            this.UpdateLightDarkMode();
            this.TranslateWinForm();
        }

        private async void cmdOK_Click(object sender, EventArgs e)
        {
            if (!(await cboCharacterSetting.DoThreadSafeFuncAsync(x => x.SelectedValue).ConfigureAwait(false) is CharacterSettings objSelectedGameplayOption))
                return;
            CharacterBuildMethod eSelectedBuildMethod = objSelectedGameplayOption.BuildMethod;
            if (_blnForExistingCharacter && !_objCharacter.Created && _objCharacter.Settings.BuildMethod == _objCharacter.EffectiveBuildMethod && eSelectedBuildMethod != _eStartingBuildMethod)
            {
                if (Program.ShowMessageBox(this,
                    string.Format(GlobalSettings.CultureInfo, await LanguageManager.GetStringAsync("Message_SelectBP_SwitchBuildMethods").ConfigureAwait(false),
                        await LanguageManager.GetStringAsync("String_" + eSelectedBuildMethod).ConfigureAwait(false), await LanguageManager.GetStringAsync("String_" + _eStartingBuildMethod).ConfigureAwait(false)).WordWrap(),
                    await LanguageManager.GetStringAsync("MessageTitle_SelectBP_SwitchBuildMethods").ConfigureAwait(false), MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
                string strOldCharacterSettingsKey = await _objCharacter.GetSettingsKeyAsync().ConfigureAwait(false);
                await _objCharacter.SetSettingsKeyAsync((await (await SettingsManager.GetLoadedCharacterSettingsAsync().ConfigureAwait(false))
                                                               .FirstOrDefaultAsync(x => ReferenceEquals(x.Value, objSelectedGameplayOption)).ConfigureAwait(false)).Key).ConfigureAwait(false);
                // If the character is loading, make sure we only switch build methods after we've loaded, otherwise we might cause all sorts of nastiness
                if (_objCharacter.IsLoading)
                    await _objCharacter.PostLoadMethodsAsync.EnqueueAsync(x => _objCharacter.SwitchBuildMethods(_eStartingBuildMethod, eSelectedBuildMethod, strOldCharacterSettingsKey, x)).ConfigureAwait(false);
                else if (!await _objCharacter.SwitchBuildMethods(_eStartingBuildMethod, eSelectedBuildMethod, strOldCharacterSettingsKey).ConfigureAwait(false))
                    return;
            }
            else
            {
                await _objCharacter.SetSettingsKeyAsync((await (await SettingsManager.GetLoadedCharacterSettingsAsync().ConfigureAwait(false))
                                                               .FirstOrDefaultAsync(
                                                                   x => ReferenceEquals(
                                                                       x.Value, objSelectedGameplayOption)).ConfigureAwait(false)).Key).ConfigureAwait(false);
            }
            _objCharacter.IgnoreRules = await chkIgnoreRules.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false);
            await this.DoThreadSafeAsync(x =>
            {
                x.DialogResult = DialogResult.OK;
                x.Close();
            }).ConfigureAwait(false);
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private async void cmdEditCharacterOption_Click(object sender, EventArgs e)
        {
            CursorWait objCursorWait = await CursorWait.NewAsync(this).ConfigureAwait(false);
            try
            {
                object objOldSelected = await cboCharacterSetting.DoThreadSafeFuncAsync(x => x.SelectedValue).ConfigureAwait(false);
                using (ThreadSafeForm<EditCharacterSettings> frmOptions
                       = await ThreadSafeForm<EditCharacterSettings>.GetAsync(
                           () => new EditCharacterSettings(objOldSelected as CharacterSettings)).ConfigureAwait(false))
                    await frmOptions.ShowDialogSafeAsync(this).ConfigureAwait(false);

                await this.DoThreadSafeAsync(x => x.SuspendLayout()).ConfigureAwait(false);
                try
                {
                    // Populate the Gameplay Settings list.
                    using (new FetchSafelyFromPool<List<ListItem>>(
                               Utils.ListItemListPool, out List<ListItem> lstGameplayOptions))
                    {
                        IAsyncReadOnlyDictionary<string, CharacterSettings> dicCharacterSettings = await SettingsManager.GetLoadedCharacterSettingsAsync().ConfigureAwait(false);
                        lstGameplayOptions.AddRange(dicCharacterSettings.Values
                                                                   .Select(objLoopOptions =>
                                                                               new ListItem(
                                                                                   objLoopOptions,
                                                                                   objLoopOptions.DisplayName)));
                        lstGameplayOptions.Sort(CompareListItems.CompareNames);
                        await cboCharacterSetting.PopulateWithListItemsAsync(lstGameplayOptions).ConfigureAwait(false);
                        await cboCharacterSetting.DoThreadSafeAsync(x => x.SelectedValue = objOldSelected).ConfigureAwait(false);
                        if (await cboCharacterSetting.DoThreadSafeFuncAsync(x => x.SelectedIndex).ConfigureAwait(false) == -1
                            && lstGameplayOptions.Count > 0)
                        {
                            (bool blnSuccess, CharacterSettings objSetting)
                                = await dicCharacterSettings.TryGetValueAsync(
                                    GlobalSettings.DefaultCharacterSetting).ConfigureAwait(false);
                            await cboCharacterSetting.DoThreadSafeAsync(x =>
                            {
                                if (blnSuccess)
                                    x.SelectedValue = objSetting;
                                if (x.SelectedIndex == -1 && lstGameplayOptions.Count > 0)
                                {
                                    x.SelectedIndex = 0;
                                }
                            }).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    await this.DoThreadSafeAsync(x => x.ResumeLayout()).ConfigureAwait(false);
                }
            }
            finally
            {
                await objCursorWait.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async void SelectBuildMethod_Load(object sender, EventArgs e)
        {
            CursorWait objCursorWait = await CursorWait.NewAsync(this).ConfigureAwait(false);
            try
            {
                await this.DoThreadSafeAsync(x => x.SuspendLayout()).ConfigureAwait(false);
                try
                {
                    // Populate the Character Settings list.
                    using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool,
                                                                   out List<ListItem> lstCharacterSettings))
                    {
                        IAsyncReadOnlyDictionary<string, CharacterSettings> dicCharacterSettings = await SettingsManager.GetLoadedCharacterSettingsAsync().ConfigureAwait(false);
                        foreach (CharacterSettings objLoopSetting in dicCharacterSettings.Values)
                        {
                            lstCharacterSettings.Add(new ListItem(objLoopSetting, objLoopSetting.DisplayName));
                        }

                        lstCharacterSettings.Sort(CompareListItems.CompareNames);
                        await cboCharacterSetting.PopulateWithListItemsAsync(lstCharacterSettings).ConfigureAwait(false);
                        if (_blnForExistingCharacter)
                        {
                            (bool blnSuccess, CharacterSettings objSetting)
                                = await dicCharacterSettings.TryGetValueAsync(
                                    await _objCharacter.GetSettingsKeyAsync().ConfigureAwait(false)).ConfigureAwait(false);
                            if (blnSuccess)
                                await cboCharacterSetting.DoThreadSafeAsync(x => x.SelectedValue = objSetting).ConfigureAwait(false);
                            if (await cboCharacterSetting.DoThreadSafeFuncAsync(x => x.SelectedIndex).ConfigureAwait(false) == -1)
                            {
                                CharacterSettings objSetting2;
                                (blnSuccess, objSetting2)
                                    = await dicCharacterSettings.TryGetValueAsync(
                                        GlobalSettings.DefaultCharacterSetting).ConfigureAwait(false);
                                if (blnSuccess)
                                    await cboCharacterSetting.DoThreadSafeAsync(x => x.SelectedValue = objSetting2).ConfigureAwait(false);
                            }

                            await chkIgnoreRules.DoThreadSafeAsync(x => x.Checked = _objCharacter.IgnoreRules).ConfigureAwait(false);
                        }
                        else
                        {
                            (bool blnSuccess, CharacterSettings objSetting)
                                = await dicCharacterSettings.TryGetValueAsync(
                                    GlobalSettings.DefaultCharacterSetting).ConfigureAwait(false);
                            if (blnSuccess)
                                await cboCharacterSetting.DoThreadSafeAsync(x => x.SelectedValue = objSetting).ConfigureAwait(false);
                        }

                        if (await cboCharacterSetting.DoThreadSafeFuncAsync(x => x.SelectedIndex).ConfigureAwait(false) == -1
                            && lstCharacterSettings.Count > 0)
                        {
                            await cboCharacterSetting.DoThreadSafeAsync(x => x.SelectedIndex = 0).ConfigureAwait(false);
                        }
                    }

                    await chkIgnoreRules.SetToolTipAsync(
                        await LanguageManager.GetStringAsync("Tip_SelectKarma_IgnoreRules").ConfigureAwait(false)).ConfigureAwait(false);
                    await ProcessGameplayIndexChanged().ConfigureAwait(false);
                }
                finally
                {
                    await this.DoThreadSafeAsync(x => x.ResumeLayout()).ConfigureAwait(false);
                }
            }
            finally
            {
                await objCursorWait.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async void cboGamePlay_SelectedIndexChanged(object sender, EventArgs e)
        {
            CursorWait objCursorWait = await CursorWait.NewAsync(this).ConfigureAwait(false);
            try
            {
                await this.DoThreadSafeAsync(x => x.SuspendLayout()).ConfigureAwait(false);
                try
                {
                    await ProcessGameplayIndexChanged().ConfigureAwait(false);
                }
                finally
                {
                    await this.DoThreadSafeAsync(x => x.ResumeLayout()).ConfigureAwait(false);
                }
            }
            finally
            {
                await objCursorWait.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async ValueTask ProcessGameplayIndexChanged(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            // Load the Priority information.
            if (await cboCharacterSetting.DoThreadSafeFuncAsync(x => x.SelectedValue, token).ConfigureAwait(false) is CharacterSettings objSelectedGameplayOption)
            {
                string strText = await LanguageManager.GetStringAsync("String_" + objSelectedGameplayOption.BuildMethod, token: token).ConfigureAwait(false);
                await lblBuildMethod.DoThreadSafeAsync(x => x.Text = strText, token).ConfigureAwait(false);
                switch (objSelectedGameplayOption.BuildMethod)
                {
                    case CharacterBuildMethod.Priority:
                    {
                        string strText2 = await LanguageManager.GetStringAsync("Label_SelectBP_Priorities", token: token)
                                                       .ConfigureAwait(false);
                        await lblBuildMethodParamLabel.DoThreadSafeAsync(x =>
                        {
                            x.Text = strText2;
                            x.Visible = true;
                        }, token).ConfigureAwait(false);
                        await lblBuildMethodParam.DoThreadSafeAsync(x =>
                        {
                            x.Text = objSelectedGameplayOption.PriorityArray;
                            x.Visible = true;
                        }, token).ConfigureAwait(false);
                        break;
                    }
                    case CharacterBuildMethod.SumtoTen:
                    {
                        string strText2 = await LanguageManager.GetStringAsync("String_SumtoTen", token: token)
                                                       .ConfigureAwait(false);
                        await lblBuildMethodParamLabel.DoThreadSafeAsync(x =>
                        {
                            x.Text = strText2;
                            x.Visible = true;
                        }, token).ConfigureAwait(false);
                        await lblBuildMethodParam.DoThreadSafeAsync(x =>
                        {
                            x.Text = objSelectedGameplayOption.SumtoTen.ToString(GlobalSettings.CultureInfo);
                            x.Visible = true;
                        }, token).ConfigureAwait(false);
                        break;
                    }
                    default:
                        await lblBuildMethodParamLabel.DoThreadSafeAsync(x => x.Visible = false, token).ConfigureAwait(false);
                        await lblBuildMethodParam.DoThreadSafeAsync(x => x.Visible = false, token).ConfigureAwait(false);
                        break;
                }

                string strNone = await LanguageManager.GetStringAsync("String_None", token: token).ConfigureAwait(false);

                await lblMaxAvail.DoThreadSafeAsync(x => x.Text = objSelectedGameplayOption.MaximumAvailability.ToString(GlobalSettings.CultureInfo), token).ConfigureAwait(false);
                await lblKarma.DoThreadSafeAsync(x => x.Text = objSelectedGameplayOption.BuildKarma.ToString(GlobalSettings.CultureInfo), token).ConfigureAwait(false);
                await lblMaxNuyen.DoThreadSafeAsync(x => x.Text = objSelectedGameplayOption.NuyenMaximumBP.ToString(GlobalSettings.CultureInfo), token).ConfigureAwait(false);
                await lblQualityKarma.DoThreadSafeAsync(x => x.Text = objSelectedGameplayOption.QualityKarmaLimit.ToString(GlobalSettings.CultureInfo), token).ConfigureAwait(false);

                await lblBooks.DoThreadSafeAsync(x =>
                {
                    x.Text = _objCharacter.TranslatedBookList(string.Join(";",
                                                                          objSelectedGameplayOption.Books));
                    if (string.IsNullOrEmpty(x.Text))
                        x.Text = strNone;
                }, token).ConfigureAwait(false);

                using (new FetchSafelyFromPool<StringBuilder>(Utils.StringBuilderPool,
                                                              out StringBuilder sbdCustomDataDirectories))
                {
                    foreach (CustomDataDirectoryInfo objLoopInfo in objSelectedGameplayOption
                                 .EnabledCustomDataDirectoryInfos)
                        sbdCustomDataDirectories.AppendLine(objLoopInfo.Name);

                    await lblCustomData.DoThreadSafeAsync(x =>
                    {
                        x.Text = sbdCustomDataDirectories.ToString();
                        if (string.IsNullOrEmpty(x.Text))
                            x.Text = strNone;
                    }, token).ConfigureAwait(false);
                }
            }
        }

        #endregion Control Events
    }
}
