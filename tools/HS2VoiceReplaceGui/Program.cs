namespace HS2VoiceReplace;

// Application entry point for the WinForms executable.

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}


