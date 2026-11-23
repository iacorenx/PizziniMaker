using System.Collections.ObjectModel;
using System.Globalization;
using PizziniMaker.Core;

namespace PizziniMaker.App;

public sealed class ActivityEntryViewModel : ObservableObject
{
    private const int MaximumSuggestions = 8;
    private readonly ActivityEntry _model;
    private readonly Action<ActivityEntryViewModel, string?> _changed;
    private readonly Func<WorkCardHistory> _history;
    private readonly List<ActivitySelection> _activityTypes;
    private readonly int _referenceYear;
    private bool _isLocked;
    private bool _isSuggestionOpen;
    private int _selectedSuggestionIndex = -1;

    public ActivityEntryViewModel(
        ActivityEntry model,
        IEnumerable<CustomActivityDefinition> customActivities,
        Action<ActivityEntryViewModel, string?> changed,
        Func<WorkCardHistory>? history = null,
        int? referenceYear = null)
    {
        _model = model;
        _model.WorkCardComment ??= string.Empty;
        _model.WorkCardNumber ??= string.Empty;
        _changed = changed;
        _history = history ?? (() => new WorkCardHistory());
        _referenceYear = referenceYear ?? DateTime.Today.Year;
        _activityTypes = ActivityCatalog.GetActivityOptions(customActivities).ToList();
        if (model.Type == ActivityType.Custom && !string.IsNullOrWhiteSpace(model.CustomActivityName) &&
            _activityTypes.All(option => !string.Equals(option.CustomName, model.CustomActivityName, StringComparison.OrdinalIgnoreCase)))
        {
            _activityTypes.Add(new ActivitySelection(
                $"custom:{model.CustomActivityName}", ActivityType.Custom,
                model.CustomActivityName, model.CustomActivityIcon, model.CustomActivityName));
        }
    }

    public Guid Id => _model.Id;

    public IReadOnlyList<ActivitySelection> ActivityTypes => _activityTypes;

    public IReadOnlyList<HourOption> HourOptions => ActivityCatalog.HourOptions;

    public ActivityType Type => _model.Type;

    public string SelectedActivityDisplayName =>
        _activityTypes.FirstOrDefault(option => option.Key.Equals(SelectedActivityKey, StringComparison.OrdinalIgnoreCase))?.DisplayName
        ?? string.Empty;

    public string SelectedActivityKey
    {
        get => Type == ActivityType.Custom ? $"custom:{_model.CustomActivityName}" : Type.ToString();
        set
        {
            var selection = _activityTypes.SingleOrDefault(option =>
                string.Equals(option.Key, value, StringComparison.OrdinalIgnoreCase));
            if (selection is null || selection.Key.Equals(SelectedActivityKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var previousType = _model.Type;
            _model.Type = selection.Type;
            _model.CustomActivityName = selection.CustomName;
            _model.CustomActivityIcon = selection.Type == ActivityType.Custom ? selection.Icon : string.Empty;
            if (selection.Type.HasWorkCard())
            {
                if (previousType != selection.Type)
                {
                    // I formati dei riferimenti sono diversi: il numero ricomincia, il commento resta.
                    _model.WorkCardNumber = selection.Type == ActivityType.Preventivo
                        ? WorkDayValidator.ComposeReference(ActivityType.Preventivo, string.Empty, DefaultYearText)
                        : string.Empty;
                }
                if (!previousType.HasWorkCard())
                {
                    _model.IsCad = true;
                }
                CloseSuggestions();
            }
            else
            {
                _model.WorkCardNumber = string.Empty;
                _model.WorkCardComment = string.Empty;
                _model.IsCad = false;
                _model.IsPaperRecorded = false;
                CloseSuggestions();
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(SelectedActivityDisplayName));
            OnPropertyChanged(nameof(HasWorkCard));
            OnPropertyChanged(nameof(IsScheda));
            OnPropertyChanged(nameof(IsReference));
            OnPropertyChanged(nameof(ReferenceSuffixMaxLength));
            OnPropertyChanged(nameof(ReferenceToolTip));
            OnPropertyChanged(nameof(ReferenceSuffixToolTip));
            NotifyNumberChanged();
            OnPropertyChanged(nameof(WorkCardComment));
            OnPropertyChanged(nameof(IsCad));
            OnPropertyChanged(nameof(IsPaperRecorded));
            _changed(this, nameof(Type));
        }
    }

    public int Minutes
    {
        get => _model.Minutes;
        set
        {
            if (_model.Minutes != value)
            {
                _model.Minutes = value;
                OnPropertyChanged();
                _changed(this, nameof(Minutes));
            }
        }
    }

    /// <summary>Numero completo come salvato: <c>8092</c>, <c>123/X</c>, <c>1234/2026</c>.</summary>
    public string WorkCardNumber
    {
        get => _model.WorkCardNumber;
        set => SetWorkCardNumber(value);
    }

    /// <summary>Parte numerica di mandato e preventivo.</summary>
    public string ReferenceNumber
    {
        get => WorkDayValidator.SplitReference(_model.WorkCardNumber).Number;
        set => SetWorkCardNumber(WorkDayValidator.ComposeReference(Type, value, ReferenceSuffix));
    }

    /// <summary>Lettera del mandato o anno del preventivo.</summary>
    public string ReferenceSuffix
    {
        get => WorkDayValidator.SplitReference(_model.WorkCardNumber).Suffix;
        set => SetWorkCardNumber(WorkDayValidator.ComposeReference(Type, ReferenceNumber, value));
    }

    public int ReferenceSuffixMaxLength => Type == ActivityType.Mandato ? 1 : 4;

    public string ReferenceToolTip => Type == ActivityType.Mandato
        ? "Numero del mandato (es. 123 in 123/X)"
        : "Numero del preventivo (es. 1234 in 1234/2026)";

    public string ReferenceSuffixToolTip => Type == ActivityType.Mandato
        ? "Lettera del mandato (es. X in 123/X)"
        : "Anno del preventivo: proposto l'anno della giornata, modificabile";

    private string DefaultYearText => _referenceYear.ToString(CultureInfo.InvariantCulture);

    public string WorkCardComment
    {
        get => _model.WorkCardComment;
        set => SetComment(value, updateSuggestions: true);
    }

    public ObservableCollection<WorkCardHistoryEntry> Suggestions { get; } = [];

    public bool IsSuggestionOpen
    {
        get => _isSuggestionOpen;
        set => SetProperty(ref _isSuggestionOpen, value);
    }

    public int SelectedSuggestionIndex
    {
        get => _selectedSuggestionIndex;
        set => SetProperty(ref _selectedSuggestionIndex, value);
    }

    public bool IsCad
    {
        get => _model.IsCad;
        set
        {
            if (_model.IsCad != value)
            {
                _model.IsCad = value;
                OnPropertyChanged();
                _changed(this, nameof(IsCad));
            }
        }
    }

    public bool IsPaperRecorded
    {
        get => _model.IsPaperRecorded;
        set
        {
            if (_model.IsPaperRecorded != value)
            {
                _model.IsPaperRecorded = value;
                OnPropertyChanged();
                _changed(this, nameof(IsPaperRecorded));
            }
        }
    }

    /// <summary>Scheda, mandato o preventivo: numero, CAD, commento e scheda cartacea.</summary>
    public bool HasWorkCard => Type.HasWorkCard();

    public bool IsScheda => Type == ActivityType.Scheda;

    /// <summary>Mandato o preventivo: numero in due caselle separate da "/".</summary>
    public bool IsReference => Type is ActivityType.Mandato or ActivityType.Preventivo;

    public bool IsEditable => !_isLocked;

    public void SetLocked(bool isLocked)
    {
        if (_isLocked != isLocked)
        {
            _isLocked = isLocked;
            OnPropertyChanged(nameof(IsEditable));
            if (isLocked)
            {
                CloseSuggestions();
            }
        }
    }

    public void MoveSuggestionSelection(int delta)
    {
        if (Suggestions.Count == 0)
        {
            return;
        }

        IsSuggestionOpen = true;
        var index = SelectedSuggestionIndex + delta;
        SelectedSuggestionIndex = index < 0 ? Suggestions.Count - 1 : index >= Suggestions.Count ? 0 : index;
    }

    /// <summary>Applica il suggerimento evidenziato; restituisce false se non c'era nulla da applicare.</summary>
    public bool ApplySelectedSuggestion()
    {
        if (!IsSuggestionOpen || SelectedSuggestionIndex < 0 || SelectedSuggestionIndex >= Suggestions.Count)
        {
            return false;
        }

        ApplySuggestion(Suggestions[SelectedSuggestionIndex]);
        return true;
    }

    public void ApplySuggestion(WorkCardHistoryEntry suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        CloseSuggestions();
        if (!IsEditable || suggestion.Type != Type)
        {
            return;
        }

        if (_model.WorkCardNumber != suggestion.Number)
        {
            _model.WorkCardNumber = suggestion.Number;
            NotifyNumberChanged();
        }
        SetComment(suggestion.Comment, updateSuggestions: false);
        _changed(this, nameof(WorkCardNumber));
    }

    public void CloseSuggestions()
    {
        IsSuggestionOpen = false;
        SelectedSuggestionIndex = -1;
    }

    private void SetWorkCardNumber(string? value)
    {
        value ??= string.Empty;
        if (_model.WorkCardNumber == value)
        {
            return;
        }

        _model.WorkCardNumber = value;
        NotifyNumberChanged();

        // Riferimento già noto e commento vuoto: il commento viene proposto dallo storico.
        if (_model.WorkCardComment.Length == 0 && WorkDayValidator.IsValidWorkCardNumber(Type, value) &&
            _history().Find(Type, value) is { Comment.Length: > 0 } known)
        {
            _model.WorkCardComment = known.Comment;
            OnPropertyChanged(nameof(WorkCardComment));
        }

        _changed(this, nameof(WorkCardNumber));
    }

    private void NotifyNumberChanged()
    {
        OnPropertyChanged(nameof(WorkCardNumber));
        OnPropertyChanged(nameof(ReferenceNumber));
        OnPropertyChanged(nameof(ReferenceSuffix));
    }

    private void SetComment(string? value, bool updateSuggestions)
    {
        value ??= string.Empty;
        if (value.Length > WorkCardHistory.MaximumCommentLength)
        {
            value = value[..WorkCardHistory.MaximumCommentLength];
        }

        if (_model.WorkCardComment != value)
        {
            _model.WorkCardComment = value;
            OnPropertyChanged(nameof(WorkCardComment));
            _changed(this, nameof(WorkCardComment));
        }

        if (updateSuggestions)
        {
            RefreshSuggestions();
        }
    }

    private void RefreshSuggestions()
    {
        var results = IsEditable && HasWorkCard
            ? _history().Search(_model.WorkCardComment, MaximumSuggestions, Type)
            : [];
        Suggestions.Clear();
        foreach (var result in results)
        {
            // Inutile proporre la combinazione già presente nella riga.
            if (result.Number == _model.WorkCardNumber &&
                string.Equals(result.Comment, _model.WorkCardComment.Trim(), StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            Suggestions.Add(result);
        }

        SelectedSuggestionIndex = Suggestions.Count > 0 ? 0 : -1;
        IsSuggestionOpen = Suggestions.Count > 0;
    }
}
