using System.Text;
using System.Windows;
using PizziniMaker.Core;

namespace PizziniMaker.App;

public partial class ImportWindow : Window
{
    private const int MaximumListedItems = 30;

    /// <summary>Anteprima: mostra cosa verrà importato e chiede conferma.</summary>
    public ImportWindow(ImportPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        InitializeComponent();
        SourceText.Text = $"Cartella di origine: {preview.SourceRoot}";
        DetailsText.Text = BuildPreviewText(preview);

        var hasSettings = preview.SourceSettings is not null;
        ImportSettingsCheckBox.IsEnabled = hasSettings;
        if (hasSettings && preview.NewCustomActivities.Count > 0)
        {
            ImportSettingsLabel.Text = "Importa anche le attività personalizzate (" +
                string.Join(", ", preview.NewCustomActivities.Select(activity => activity.Name)) +
                ") e la data di inizio controllo (se precedente)";
        }

        if (!preview.HasImportableData && preview.NewCustomActivities.Count == 0)
        {
            ConfirmButton.IsEnabled = false;
            ConfirmButton.ToolTip = "Non ci sono dati nuovi da importare.";
        }
    }

    /// <summary>Rapporto finale dopo l'importazione.</summary>
    public ImportWindow(ImportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        InitializeComponent();
        Title = "Importazione completata - PizziniMaker";
        HeaderText.Text = "Importazione completata";
        SourceText.Text = $"Backup dei dati precedenti all'importazione: {report.BackupPath}";
        DetailsText.Text = BuildReportText(report);
        OptionsPanel.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Collapsed;
        ConfirmButton.Content = "Chiudi";
        ConfirmButton.IsCancel = true;
    }

    public bool ImportSettings => ImportSettingsCheckBox.IsChecked == true;

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private static string BuildPreviewText(ImportPreview preview)
    {
        var text = new StringBuilder();
        text.AppendLine($"✔ Giornate da importare: {preview.NewDays.Count}");
        AppendDates(text, preview.NewDays.Select(day => day.Date));
        text.AppendLine($"⇄ Giornate già presenti (verrà mantenuta quella attuale): {preview.ConflictingDays.Count}");
        AppendDates(text, preview.ConflictingDays);
        text.AppendLine($"📄 Log da importare: {preview.LogsToImport.Count}");
        text.AppendLine($"🔢 Nuove schede nello storico: {preview.NewHistoryEntries}");
        if (preview.SourceSettings is null)
        {
            text.AppendLine("⚙ Impostazioni: non trovate nella cartella di origine");
        }
        AppendIssues(text, preview.InvalidItems, "⚠ Elementi non validi che verranno ignorati");
        if (!preview.HasImportableData)
        {
            text.AppendLine();
            text.AppendLine("Non ci sono giornate, log o schede nuove da importare.");
        }
        return text.ToString().TrimEnd();
    }

    private static string BuildReportText(ImportReport report)
    {
        var text = new StringBuilder();
        text.AppendLine($"✔ Giornate importate: {report.ImportedDays.Count}");
        AppendDates(text, report.ImportedDays);
        text.AppendLine($"⇄ Giornate mantenute (già presenti): {report.KeptDays.Count}");
        AppendDates(text, report.KeptDays);
        text.AppendLine($"📄 Log importati: {report.ImportedLogs}");
        text.AppendLine($"🔢 Schede aggiunte allo storico: {report.HistoryEntriesAdded}");
        if (report.CustomActivitiesAdded > 0)
        {
            text.AppendLine($"⚙ Attività personalizzate aggiunte: {report.CustomActivitiesAdded}");
        }
        if (report.SettingsUpdated)
        {
            text.AppendLine("⚙ Impostazioni aggiornate");
        }
        AppendIssues(text, report.SkippedItems, "⚠ Elementi ignorati");
        return text.ToString().TrimEnd();
    }

    private static void AppendDates(StringBuilder text, IEnumerable<DateOnly> dates)
    {
        var list = dates.Order().ToArray();
        if (list.Length == 0)
        {
            return;
        }

        var shown = list.Take(MaximumListedItems).Select(date => date.ToString("dd/MM/yyyy"));
        var more = list.Length > MaximumListedItems ? $" … e altre {list.Length - MaximumListedItems}" : string.Empty;
        text.AppendLine("     " + string.Join(", ", shown) + more);
    }

    private static void AppendIssues(StringBuilder text, IReadOnlyCollection<ImportIssue> issues, string title)
    {
        if (issues.Count == 0)
        {
            return;
        }

        text.AppendLine($"{title}: {issues.Count}");
        foreach (var issue in issues.Take(MaximumListedItems))
        {
            text.AppendLine($"     {issue.Item}: {issue.Reason}");
        }
        if (issues.Count > MaximumListedItems)
        {
            text.AppendLine($"     … e altri {issues.Count - MaximumListedItems}");
        }
    }
}
