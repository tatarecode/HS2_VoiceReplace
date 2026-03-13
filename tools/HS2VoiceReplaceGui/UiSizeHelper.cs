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
}


