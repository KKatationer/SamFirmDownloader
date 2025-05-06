using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SamFirmDownloader.ViewModels;

internal class FirmwareInfoViewModel : INotifyPropertyChanged
{
    private static readonly string[] _models = ["SM-S9260", "SM-G960F", "SM-G973F", "SM-S901B", "SM-G991B", "SM-G998B", "SM-S911B", "SM-T395", "SM-T545", "SM-T575"];
    private static readonly string[] _regions = ["BRI", "NEE", "EUX"];

    private string _model = string.Empty;
    private string _region = string.Empty;
    private string _serial = "R5CX51AAEBF";
    private bool _auto = true;
    private string _pda = string.Empty;
    private string _csc = string.Empty;
    private string _phone = string.Empty;
    private bool _bn = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Model
    {
        get => _model;
        set
        {
            if (_model != value)
            {
                _model = value;
                OnPropertyChanged(nameof(Model));
            }
        }
    }

    public string Region
    {
        get => _region;
        set
        {
            if (_region != value)
            {
                _region = value;
                OnPropertyChanged(nameof(Region));
            }
        }
    }

    public string Serial
    {
        get => _serial;
        set
        {
            if (_serial != value)
            {
                _serial = value;
                OnPropertyChanged(nameof(Serial));
            }
        }
    }

    public bool Auto
    {
        get => _auto;
        set
        {
            if (_auto != value)
            {
                _auto = value;
                OnPropertyChanged(nameof(Auto));
            }
        }
    }

    public string PDA
    {
        get => _pda;
        set
        {
            if (_pda != value)
            {
                _pda = value;
                OnPropertyChanged(nameof(PDA));
            }
        }
    }

    public string CSC
    {
        get => _csc;
        set
        {
            if (_csc != value)
            {
                _csc = value;
                OnPropertyChanged(nameof(CSC));
            }
        }
    }

    public string Phone
    {
        get => _phone;
        set
        {
            if (_phone != value)
            {
                _phone = value;
                OnPropertyChanged(nameof(Phone));
            }
        }
    }

    public bool BN
    {
        get => _bn;
        set
        {
            if (_bn != value)
            {
                _bn = value;
                OnPropertyChanged(nameof(BN));
            }
        }
    }

    [JsonIgnore]
    public string[] Models
    {
        get => _models;
    }

    [JsonIgnore]
    public string[] Regions
    {
        get => _regions;
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}