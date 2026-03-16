using System.ComponentModel;
using System.Globalization;

namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    // Centralized key-based translation keeps UI strings in one catalog and makes
    // it practical to add more languages without touching the form layout code.
    private string T(string key, params object[] args) => UiTextCatalog.Get(_uiLanguage, key, args);

    private static UiLanguage ParseUiLanguageCode(string? code)
        => string.Equals(code?.Trim(), "en", StringComparison.OrdinalIgnoreCase) ? UiLanguage.En : UiLanguage.Ja;

    private static string GetUiLanguageCode(UiLanguage lang) => lang == UiLanguage.En ? "en" : "ja";

    private void ApplyLanguageToSharedControls()
    {
        Text = T("app.title");
        _btnSetup.Text = T("button.setup");
        _btnExtract.Text = T("button.extract");
        _btnDeploy.Text = T("button.deploy");
        _btnUndeploy.Text = T("button.undeploy");
        _btnPreview.Text = T("button.preview");
        _btnCancel.Text = T("button.stop");
        _btnSeedVcSettings.Text = T("button.conversionSettings");
        _btnEditNormalSegment.Text = T("button.range");
        _btnEditEroSegment.Text = T("button.range");
        _btnClearNormalSegment.Text = T("button.clear");
        _btnClearEroSegment.Text = T("button.clear");
        _btnPlayPreviewNormal.Text = T("button.play");
        _btnPlayPreviewEro.Text = T("button.play");
        _chkSkipCompleted.Text = T("checkbox.skipCompleted");
        UiSizeHelper.FitButton(_btnSetup, 160, 38);
        UiSizeHelper.FitButton(_btnExtract, 160, 38);
        UiSizeHelper.FitButton(_btnDeploy, 140, 38);
        UiSizeHelper.FitButton(_btnUndeploy, 160, 38);
        UiSizeHelper.FitButton(_btnPreview, 160, 38);
        UiSizeHelper.FitButton(_btnCancel, 120, 38);
        UiSizeHelper.FitButton(_btnSeedVcSettings, 180, 38);
        UiSizeHelper.FitButton(_btnEditNormalSegment, 110, 34);
        UiSizeHelper.FitButton(_btnClearNormalSegment, 90, 34);
        UiSizeHelper.FitButton(_btnEditEroSegment, 110, 34);
        UiSizeHelper.FitButton(_btnClearEroSegment, 90, 34);
        UiSizeHelper.FitButton(_btnPlayPreviewNormal, 90, 36);
        UiSizeHelper.FitButton(_btnPlayPreviewEro, 90, 36);
    }

    private void ChangeUiLanguage(UiLanguage newLang)
    {
        if (_uiLanguage == newLang)
            return;

        _uiLanguage = newLang;
        LocalizationState.CurrentLanguage = newLang;
        // PropertyGrid caches descriptor metadata, so localized attribute values must be
        // invalidated explicitly when the UI language changes.
        TypeDescriptor.Refresh(typeof(SeedVcUiSettings));
        ApplyLanguageToSharedControls();
        DetachDialogHostedSharedControls();

        if (_basicSettingsDialog is { IsDisposed: false })
        {
            _basicSettingsDialog.Hide();
            _basicSettingsDialog.Dispose();
            _basicSettingsDialog = null;
        }
        if (_sampleAudioDialog is { IsDisposed: false })
        {
            _sampleAudioDialog.Hide();
            _sampleAudioDialog = null;
        }

        BuildLayout();
        ReflowLayout();
        RecreateEmbeddedGrid();
        RefreshSampleSignatureDisplay();
        SetBusyState(_isBusy);
    }

    private void DetachDialogHostedSharedControls()
    {
        static void Detach(Control? control)
        {
            if (control == null || control.IsDisposed)
                return;
            var parent = control.Parent;
            if (parent == null || parent.IsDisposed)
                return;
            parent.Controls.Remove(control);
        }

        Detach(_txtBundleRoot);
        Detach(_txtOutputRoot);
        Detach(_txtExternalToolsRoot);
        Detach(_txtHs2Root);
        Detach(_chkSkipCompleted);
        Detach(_txtPreviewNormal);
        Detach(_txtPreviewEro);
        Detach(_btnPlayPreviewNormal);
        Detach(_btnPlayPreviewEro);
        Detach(_btnPreview);

        if (_btnSampleDialogCancel != null && !_btnSampleDialogCancel.IsDisposed)
            Detach(_btnSampleDialogCancel);

        _lblSampleSignatureInDialog = null;
        _btnSampleDialogCancel = null;
    }


}



