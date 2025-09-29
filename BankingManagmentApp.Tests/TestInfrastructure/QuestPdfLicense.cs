using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

public static class QuestPdfLicense
{
    [ModuleInitializer]
    public static void Init()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
