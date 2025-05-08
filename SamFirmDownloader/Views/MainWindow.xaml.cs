using MicaWPF.Controls;
using Microsoft.Win32;
using SamFirmDownloader.Helper;
using SamFirmDownloader.Models;
using SamFirmDownloader.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;

namespace SamFirmDownloader.Views;

public partial class MainWindow : MicaWindow
{
    private readonly FirmwareInfoViewModel? firmwareInfo;
    private readonly DownloadInfoViewModel? downloadInfo;

    private CancellationTokenSource? downloadCancellationTokenSource;

    public MainWindow()
    {
        InitializeComponent();
        InitInvoke();

        firmwareInfo = this.Resources["FirmwareInfo"] as FirmwareInfoViewModel;
        downloadInfo = this.Resources["DownloadInfo"] as DownloadInfoViewModel;
    }

    private void InitInvoke()
    {
        LogHelper.WriteAction += (str) =>
        {
            LogRTbx.Dispatcher.Invoke(() =>
            {
                LogRTbx.AppendText("---[");
                LogRTbx.AppendText(DateTime.Now.ToString());
                LogRTbx.AppendText("]---");
                LogRTbx.AppendText(Environment.NewLine);
                LogRTbx.AppendText(str);
                LogRTbx.AppendText(Environment.NewLine);
                LogRTbx.ScrollToEnd();
            });
        };

        LogHelper.SaveAction += () =>
        {
            var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "SamFirm.log");
            var oldlogFile = logFile + ".old";

            try
            {
                if (LogRTbx.Document == null || !LogRTbx.Document.Blocks.Any())
                    return;

                if (File.Exists(logFile) && new FileInfo(logFile).Length > 2097152L)
                {
                    File.Delete(oldlogFile);
                    File.Move(logFile, oldlogFile);
                }

                using TextWriter textWriter = new StreamWriter(new FileStream(logFile, FileMode.Append));
                textWriter.WriteLine();

                foreach (var block in LogRTbx.Document.Blocks)
                {
                    var textRange = new TextRange(block.ContentStart, block.ContentEnd);
                    textWriter.WriteLine(textRange.Text.Trim());
                }
            }
            catch { }
        };

        WebHelper.SpeedAction += (speed) =>
        {
            if (downloadInfo == null)
                return;
            downloadInfo.Speed = speed;
        };

        WebHelper.ProgressAction += (progress) =>
        {
            if (downloadInfo == null)
                return;
            downloadInfo.IsIndeterminate = false;
            downloadInfo.Progress = progress;
        };

        CryptoHelper.ZipProcess += (progress) =>
        {
            if (downloadInfo == null)
                return;
            downloadInfo.IsIndeterminate = false;
            downloadInfo.Progress = progress;
        };

        CryptoHelper.TransferredProcess += (bytesRead) =>
        {
            if (downloadInfo == null)
                return;
            downloadInfo.Speed = $"{bytesRead / 1024.0 / 1024.0:0.00} MB";
        };
    }

    private bool PreCheck()
    {
        var precheck = false;
        if (string.IsNullOrEmpty(firmwareInfo?.Model))
            LogHelper.WriteLog("Error: Please specify a model");
        else if (string.IsNullOrEmpty(firmwareInfo.Region))
            LogHelper.WriteLog("Error: Please specify a region");
        else if (string.IsNullOrEmpty(firmwareInfo.Serial))
            LogHelper.WriteLog("Error: Please specify an Imei or Serial number");
        else if (!firmwareInfo.Auto && (string.IsNullOrEmpty(firmwareInfo.Serial) || string.IsNullOrEmpty(firmwareInfo.PDA) || string.IsNullOrEmpty(firmwareInfo.CSC) || string.IsNullOrEmpty(firmwareInfo.Phone)))
            LogHelper.WriteLog("Error: Please specify PDA, CSC and Phone version and Imei/Serial or use Auto Method");
        else
            precheck = true;

        return precheck;
    }

    private async Task<Firmware> GetFirmware()
    {
        try
        {
            ArgumentNullException.ThrowIfNull(firmwareInfo);

            var firmware = firmwareInfo.Auto ?
                await FirmwareHelper.UpdateCheck(
                    firmwareInfo.Model.ToUpper(),
                    firmwareInfo.Region.ToUpper(),
                    firmwareInfo.Serial.ToUpper(),
                    firmwareInfo.BN) :
                await FirmwareHelper.UpdateCheck(
                    firmwareInfo.Model.ToUpper(),
                    firmwareInfo.Region.ToUpper(),
                    firmwareInfo.Serial.ToUpper(),
                    firmwareInfo.PDA.ToUpper(),
                    firmwareInfo.CSC.ToUpper(),
                    firmwareInfo.Phone.ToUpper(),
                    firmwareInfo.PDA.ToUpper(),
                    firmwareInfo.BN,
                    false);

            if (firmware.Version == null)
                LogHelper.WriteLog("Error: Could not fetch info for " + firmwareInfo.Model + "/" + firmwareInfo.Region);
            else
                LogHelper.WriteLog("Firmware Version: " + firmware.Version);

            return firmware;
        }
        catch (Exception ex)
        {
            LogHelper.WriteLog("Error: " + ex.Message);
            return new();
        }
    }

    private void GetDownloadInfo(Firmware firmware)
    {
        if (downloadInfo == null || string.IsNullOrEmpty(firmware.Filename))
            return;

        downloadInfo.File = firmware.Filename;
        downloadInfo.Version = firmware.Version;
        downloadInfo.Size = (long.Parse(firmware.Size) / 1024L / 1024L).ToString() + " MB";
        downloadInfo.Firmware = firmware;
    }

    private string GetDownloadFile()
    {
        var destinationfile = string.Empty;
        if (downloadInfo == null)
            return destinationfile;

        if (string.IsNullOrEmpty(downloadInfo.File))
        {
            LogHelper.WriteLog("No file to download. Please check for update first.");
            return destinationfile;
        }

        var str = Path.GetExtension(Path.GetFileNameWithoutExtension(downloadInfo.File)) + Path.GetExtension(downloadInfo.File);
        var saveFileDialog = new SaveFileDialog
        {
            OverwritePrompt = false,
            FileName = downloadInfo.File.Replace(str, string.Empty),
            Filter = "Firmware|*" + str
        };
        var saveFileDialogRtn = saveFileDialog.ShowDialog();
        if (saveFileDialogRtn == null || !saveFileDialogRtn.Value)
            return destinationfile;

        if (!saveFileDialog.FileName.EndsWith(str))
            saveFileDialog.FileName += str;
        else
            saveFileDialog.FileName = saveFileDialog.FileName.Replace(str + str, str);

        destinationfile = saveFileDialog.FileName;
        LogHelper.WriteLog("Filename: " + destinationfile);

        if (File.Exists(destinationfile))
        {
            switch (MessageBox.Show("The destination file already exists.\r\nWould you like to append it (resume download)?", "Question", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
            {
                case MessageBoxResult.Cancel:
                    LogHelper.WriteLog("Aborted.");
                    return string.Empty;

                case MessageBoxResult.No:
                    File.Delete(destinationfile);
                    break;
            }
        }

        if (downloadInfo.File == destinationfile)
            LogHelper.WriteLog($"Trying to download {downloadInfo.File}");
        else
            LogHelper.WriteLog($"Trying to download {downloadInfo.File} to {destinationfile}");

        return destinationfile;
    }

    private async Task<bool> CheckCRC32(string destinationfile, CancellationToken cancellationToken = default)
    {
        if (downloadInfo == null)
            return false;

        if (!downloadInfo.CRC32)
            return true;

        if (string.IsNullOrEmpty(destinationfile))
            return false;

        if (!File.Exists(destinationfile))
        {
            LogHelper.WriteLog($"Error: File {destinationfile} does not exist");
            return false;
        }

        try
        {
            LogHelper.WriteLog("Checking CRC32...");
            downloadInfo.IsIndeterminate = true;
            downloadInfo.Speed = string.Empty;

            using var stream = File.OpenRead(destinationfile);
            var crc32 = new Crc32Helper();
            var crc = await crc32.ComputeHashAsync(stream, cancellationToken);

            var result = crc.Compare(downloadInfo.Firmware.CRC);
            if (!result)
                LogHelper.WriteLog("Error: CRC does not match. Please redownload the file.");
            else
                LogHelper.WriteLog("Checking CRC32 pass!");

            return result;
        }
        catch (OperationCanceledException)
        {
            LogHelper.WriteLog("Error: CRC check canceled.");
            return false;
        }
        finally { downloadInfo.IsIndeterminate = false; }
    }

    private async Task<bool> DecryptFile(string destinationfile, CancellationToken cancellationToken = default)
    {
        if (downloadInfo == null)
            return false;

        if (!downloadInfo.Decrypt)
            return true;

        if (string.IsNullOrEmpty(destinationfile))
            return false;

        if (!File.Exists(destinationfile))
        {
            LogHelper.WriteLog($"Error: File {destinationfile} does not exist");
            return false;
        }

        LogHelper.WriteLog("Decrypting and unzipping firmware...");

        if (destinationfile.EndsWith(".enc2"))
            CryptoHelper.SetDecryptKey(downloadInfo.Firmware.Region, downloadInfo.Firmware.Model, downloadInfo.Firmware.Version);
        else if (destinationfile.EndsWith(".enc4"))
        {
            if (downloadInfo.Firmware.BinaryNature == 1)
                CryptoHelper.SetDecryptKey(downloadInfo.Firmware.Version, downloadInfo.Firmware.LogicValueFactory);
            else
                CryptoHelper.SetDecryptKey(downloadInfo.Firmware.Version, downloadInfo.Firmware.LogicValueHome);
        }
        else
        {
            LogHelper.WriteLog("Error: File is not encrypted");
            return false;
        }

        var outputDirectory = Path.Combine(
            Path.GetDirectoryName(destinationfile) ?? string.Empty,
            Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(destinationfile)));

        downloadInfo.IsIndeterminate = true;
        downloadInfo.Progress = 0d;
        downloadInfo.Speed = string.Empty;

        var unzipRtn = await CryptoHelper.DecryptAndUnzip(destinationfile, outputDirectory, cancellationToken);
        if (unzipRtn == 0 && !cancellationToken.IsCancellationRequested)
        {
            await CryptoHelper.SaveMeta(downloadInfo.Firmware, Path.Combine(outputDirectory, "FirmwareInfo.txt"));
            LogHelper.WriteLog("Decryption finished!");
        }

        downloadInfo.IsIndeterminate = false;
        downloadInfo.Speed = string.Empty;

        return true;
    }

    private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (firmwareInfo == null || downloadInfo == null || !PreCheck())
            return;

        if (downloadInfo.IsDownloading)
        {
            LogHelper.WriteLog("A task is running!");
            return;
        }

        downloadInfo.Progress = 0d;
        downloadInfo.Speed = string.Empty;
        downloadInfo.IsIndeterminate = true;

        var firmware = await GetFirmware();
        GetDownloadInfo(firmware);

        downloadInfo.IsIndeterminate = false;
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (downloadInfo == null)
            return;

        if (downloadInfo.IsDownloading)
        {
            downloadCancellationTokenSource?.Cancel();
            return;
        }

        downloadInfo.IsIndeterminate = true;

        var destinationfile = GetDownloadFile();
        if (string.IsNullOrEmpty(destinationfile))
        {
            downloadInfo.IsIndeterminate = false;
            return;
        }

        await Task.Run(async () =>
        {
            downloadCancellationTokenSource = new();
            downloadInfo.IsDownloading = true;

            var result = await FirmwareHelper.Download(downloadInfo.Firmware, destinationfile, downloadCancellationTokenSource.Token);
            if (result >= 200 && result < 300)
            {
                var crc = await CheckCRC32(destinationfile, downloadCancellationTokenSource.Token);
                if (crc)
                    await DecryptFile(destinationfile, downloadCancellationTokenSource.Token);
            }

            downloadInfo.IsDownloading = false;
        });
    }

    private async void DecryptButton_Click(object sender, RoutedEventArgs e)
    {
        if (downloadInfo == null)
            return;

        if (downloadInfo.IsDownloading)
        {
            LogHelper.WriteLog("A task is running!");
            return;
        }

        downloadCancellationTokenSource = new();
        downloadInfo.IsDownloading = true;

        var destinationfile = GetDownloadFile();
        var crc = await CheckCRC32(destinationfile, downloadCancellationTokenSource.Token);
        if (crc)
            await DecryptFile(destinationfile, downloadCancellationTokenSource.Token);

        downloadInfo.IsDownloading = false;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        LogHelper.WriteAction = null;
        LogHelper.SaveAction = null;
        WebHelper.SpeedAction = null;
        WebHelper.ProgressAction = null;
        CryptoHelper.ZipProcess = null;
        CryptoHelper.TransferredProcess = null;
        downloadCancellationTokenSource?.Cancel();
    }
}