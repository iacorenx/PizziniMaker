using PizziniMaker.Core;

namespace PizziniMaker.App;

public sealed class ActivityEntryViewModel : ObservableObject
{
    private readonly ActivityEntry _model;
    private readonly Action<ActivityEntryViewModel, string?> _changed;
    private bool _isLocked;

    public ActivityEntryViewModel(ActivityEntry model, Action<ActivityEntryViewModel, string?> changed)
    {
        _model = model;
        _changed = changed;
    }

    public Guid Id => _model.Id;

    public IReadOnlyList<ActivityTypeOption> ActivityTypes => ActivityCatalog.ActivityTypes;

    public IReadOnlyList<HourOption> HourOptions => ActivityCatalog.HourOptions;

    public ActivityType Type
    {
        get => _model.Type;
        set
        {
            if (_model.Type == value)
            {
                return;
            }

            _model.Type = value;
            if (value == ActivityType.Scheda)
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
