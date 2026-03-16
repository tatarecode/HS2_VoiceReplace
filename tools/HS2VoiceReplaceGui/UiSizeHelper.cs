namespace HS2VoiceReplace;

// Centralizes small layout-sizing rules so button and control sizing stays consistent across dialogs.

internal static class UiSizeHelper
{
    public static void FitButton(Button button, int minWidth, int height, int horizontalPadding = 28)
    {
        if (button == null || button.IsDisposed)
            return;

        var text = button.Text ?? string.Empty;
        var measured = TextRenderer.MeasureText(text, button.Font);
        button.Width = Math.Max(minWidth, measured.Width + horizontalPadding);
        if (height > 0)
            button.Height = height;
    }

    public static void ApplyDialogSize(Form dialog, Size desired, Size minimum, bool fixedSize, int margin = 48)
    {
        if (dialog == null || dialog.IsDisposed)
            return;

        dialog.FormBorderStyle = fixedSize ? FormBorderStyle.FixedDialog : FormBorderStyle.Sizable;
        dialog.MinimizeBox = false;
        dialog.MaximizeBox = !fixedSize;

        var probeClient = new Size(100, 100);
        dialog.ClientSize = probeClient;
        var chrome = new Size(dialog.Width - dialog.ClientSize.Width, dialog.Height - dialog.ClientSize.Height);

        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        var maxOuterWidth = Math.Max(640, workingArea.Width - margin);
        var maxOuterHeight = Math.Max(480, workingArea.Height - margin);
        var maxClientWidth = Math.Max(480, maxOuterWidth - chrome.Width);
        var maxClientHeight = Math.Max(320, maxOuterHeight - chrome.Height);

        var clientWidth = Math.Min(desired.Width, maxClientWidth);
        var clientHeight = Math.Min(desired.Height, maxClientHeight);

        var minClientWidth = Math.Min(minimum.Width, clientWidth);
        var minClientHeight = Math.Min(minimum.Height, clientHeight);

        dialog.ClientSize = new Size(clientWidth, clientHeight);
        var minimumOuter = new Size(minClientWidth + chrome.Width, minClientHeight + chrome.Height);
        dialog.MinimumSize = minimumOuter;
        if (fixedSize)
            dialog.MaximumSize = dialog.Size;
        else
            dialog.MaximumSize = Size.Empty;
    }
}


