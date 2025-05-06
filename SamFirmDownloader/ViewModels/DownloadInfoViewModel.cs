using SamFirmDownloader.Models;
using System.ComponentModel;

namespace SamFirmDownloader.ViewModels;

internal class DownloadInfoViewModel : INotifyPropertyChanged
{
    private string _file = string.Empty;
    private string _version = string.Empty;
    private string _size = string.Empty;
    private bool _crc32 = true;
    private bool _decrypt = true;
    private string _speed = string.Empty;
    private double _progress = 0.0d;
    private bool _isIndeterminate = false;
    private bool _isDownloading = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string File
    {
        get => _file;
        set
        {
            if (_file != value)
            {
                _file = value;
                OnPropertyChanged(nameof(File));
            }
        }
    }

    public string Version
    {
        get => _version;
        set
        {
            if (_version != value)
            {
                _version = value;
                OnPropertyChanged(nameof(Version));
            }
        }
    }

    public string Size
    {
        get => _size;
        set
        {
            if (_size != value)
            {
                _size = value;
                OnPropertyChanged(nameof(Size));
            }
        }
    }

    public bool CRC32
    {
        get => _crc32;
        set
        {
            if (_crc32 != value)
            {
                _crc32 = value;
                OnPropertyChanged(nameof(CRC32));
            }
        }
    }

    public bool Decrypt
    {
        get => _decrypt;
        set
        {
            if (_decrypt != value)
            {
                _decrypt = value;
                OnPropertyChanged(nameof(Decrypt));
            }
        }
    }

    public string Speed
    {
        get => _speed;
        set
        {
            if (_speed != value)
            {
                _speed = value;
                OnPropertyChanged(nameof(Speed));
            }
        }
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            if (_isIndeterminate != value)
            {
                _isIndeterminate = value;
                OnPropertyChanged(nameof(IsIndeterminate));
            }
        }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (_isDownloading != value)
            {
                _isDownloading = value;
                OnPropertyChanged(nameof(IsDownloading));
            }
        }
    }

    public Firmware Firmware { get; set; }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}