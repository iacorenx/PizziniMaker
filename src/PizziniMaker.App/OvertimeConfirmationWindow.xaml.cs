using System.Windows;
using PizziniMaker.Core;

namespace PizziniMaker.App;

public partial class OvertimeConfirmationWindow : Window
{
    public OvertimeConfirmationWindow(DayClosureAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        InitializeComponent();
        var total = ActivityCatalog.FormatDuration(assessment.TotalMinutes);
        var travel = assessment.TravelMinutes > 0
            ? $" Le {ActivityCatalog.FormatDuration(assessment.TravelMinutes)} di viaggio sono contabilizzate a parte."
            : string.Empty;

        if (assessment.Kind == DayClosureKind.OptionalDay)
        {
            Title = "Conferma sabato";
            HeaderText.Text = "Sabato facoltativo";
            MessageText.Text = assessment.OvertimeMinutes > 0
                ? $"Il totale è {total}. Al sabato non serve raggiungere 8 ore: " +
                  $"{ActivityCatalog.FormatDuration(assessment.OvertimeMinutes)} valgono come straordinario.{travel} Vuoi chiudere il giorno?"
                : $"Il totale è {total}.{travel} Vuoi chiudere il giorno?";
            HintText.Visibility = assessment.OvertimeMinutes > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            MessageText.Text = $"Il totale è {total}. Sono presenti " +
                $"{ActivityCatalog.FormatDuration(assessment.OvertimeMinutes)} di straordinario.{travel} Vuoi chiudere il giorno?";
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
