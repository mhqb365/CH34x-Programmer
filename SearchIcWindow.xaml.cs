using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Ch34xProgrammer;

public partial class SearchIcWindow : Window
{
    private readonly List<IcCandidate> _allCandidates;
    private readonly ObservableCollection<IcCandidate> _visibleCandidates = [];

    public SearchIcWindow(IEnumerable<IcCandidate> candidates, string? jedecId)
    {
        InitializeComponent();
        _allCandidates = candidates.ToList();
        IcGrid.ItemsSource = _visibleCandidates;

        if (!string.IsNullOrWhiteSpace(jedecId))
        {
            FilterBox.Text = jedecId;
            ExampleText.Text = $"JEDEC ID: {jedecId}";
        }

        RefreshFilter();
        if (_visibleCandidates.Count > 0)
        {
            IcGrid.SelectedIndex = 0;
        }
    }

    public IcCandidate? SelectedCandidate { get; private set; }

    private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshFilter();

    private void IcGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => SelectCurrent();

    private void Select_Click(object sender, RoutedEventArgs e) => SelectCurrent();

    private void SelectCurrent()
    {
        if (IcGrid.SelectedItem is not IcCandidate candidate)
        {
            return;
        }

        SelectedCandidate = candidate;
        DialogResult = true;
        Close();
    }

    private void RefreshFilter()
    {
        var filter = FilterBox.Text.Trim();
        var terms = filter.Split([' ', '+', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var filtered = _allCandidates.Where(candidate => Matches(candidate, terms)).ToList();
        _visibleCandidates.Clear();
        foreach (var candidate in filtered)
        {
            _visibleCandidates.Add(candidate);
        }

        FoundText.Text = $"Found: {_visibleCandidates.Count} Chips";
    }

    private static bool Matches(IcCandidate candidate, string[] terms)
    {
        if (terms.Length == 0)
        {
            return true;
        }

        var haystack = $"{candidate.Device} {candidate.Volts} {candidate.Size} {candidate.Page} {candidate.Manuf} {candidate.Type} {candidate.JedecId}".ToUpperInvariant();
        return terms.All(term => WildcardMatch(haystack, term.ToUpperInvariant()));
    }

    private static bool WildcardMatch(string haystack, string term)
    {
        if (!term.Contains('*'))
        {
            return haystack.Contains(term);
        }

        var pieces = term.Split('*', StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        foreach (var piece in pieces)
        {
            var found = haystack.IndexOf(piece, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            index = found + piece.Length;
        }

        return true;
    }
}
