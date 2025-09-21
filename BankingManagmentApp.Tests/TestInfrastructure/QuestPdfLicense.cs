using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

public static class QuestPdfLicense
{
    // Автоматично се изпълнява при зареждане на тест асемблито
    [ModuleInitializer]
    public static void Init()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
