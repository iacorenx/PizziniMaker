using PizziniMaker.Core;

namespace PizziniMaker.App;

public sealed class ActivityEntryViewModel : ObservableObject
{
    private readonly ActivityEntry _model;
    private readonly Action<ActivityEntryViewModel, string?> _changed;
    private readonly List<ActivitySelection> _activityTypes;
    private bool _isLocked;

    public ActivityEntryViewModel(
        ActivityEntry model,
        IEnumerable<CustomActivityDefinition> customActivities,
        Action<ActivityEntryViewModel, string?> changed)
    {
        _model = model;
        _changed = changed;
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

            _model.Type = selection.Type;
            _model.CustomActivityName = selection.CustomName;
            _model.CustomActivityIcon = selection.Type == ActivityType.Custom ? selection.Icon : string.Empty;
            if (selection.Type == ActivityType.Scheda)
            {
                _model.IsCad = true;
            }
            else
            {
                _model.WorkCardNumber = string.Empty;
                _model.IsCad = false;
                _model.IsPaperRecorded = false;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(IsScheda));
            OnPropertyChanged(nameof(WorkCardNumber));
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

    public string WorkCardNumber
    {
        get => _model.WorkCardNumber;
        set
        {
            value ??= string.Empty;
            if (_model.WorkCardNumber != value)
            {
                _model.WorkCardNumber = value;
                OnPropertyChanged();
                _changed(this, nameof(WorkCardNumber));
            }
        }
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

    public bool IsScheda => Type == ActivityType.Scheda;

    public bool IsEditable => !_isLocked;

    public void SetLocked(bool isLocked)
    {
        if (_isLocked != isLocked)
        {
            _isLocked = isLocked;
            OnPropertyChanged(nameof(IsEditable));
        }
    }
}
