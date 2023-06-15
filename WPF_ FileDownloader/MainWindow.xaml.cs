// the application downloads files from the specified link
// додаток скачує файли за вказаним линком 

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WPF__FileDownloader
{
    //  https://bank.gov.ua/files/Exchange_r.xls
    //  https://w.forfun.com/fetch/db/dbfbb3f95b834df945c0bb6657cdad76.jpeg?h=900&r=0.5
    //  https://w.forfun.com/fetch/af/af336a2ad0a6968c2a2b50de60e87eae.jpeg?h=450&r=0.5
    //  https://sefon.pro/api/mp3_download/direct/263673/3vUCAFjZuKFe4fRkiicNvYdAY0m1TO-DMlivqlpQ614wm7kWvdPLMwECKuZ_RsHbpqHU6riZe6xT6-Rg_akNu2RXo3bZcgrciswWNTpMbRblVo_4JKSuWhidGVIyRLoZjjdYqe-JEzVIQ4L4wUfVTbk5c1h64iDgI8lyyWg/
    //  https://sefon.pro/api/mp3_download/direct/117027/3vUCAGfV3Wt6y9DUUEWgY6IphWzToiLvQwTWWzyt5IJac0XYm1uhjq-aMrn4pHnMjS440pyTXxqpq0pw6mC-7gh6Ak5FKx5_NmTDejS1gJGpwlVNST1VMAI2oos8JbY4FRCGzTljH5TbCNE3UA343fpn9GB-Ux9kf2ufeMw/
    public partial class MainWindow : Window
    {      
        public string Status { get; set; }
        SiteViewModel siteViewModel;
        public enum downloadStatus
        {
            Preparation,
            Downloading,
            Completed,
            Canceled,
            Peused,
            Failed
        }

        private static readonly HttpClient client = new HttpClient();
        private static CancellationTokenSource cancellationTokenSource;
        private static long downloadedBytes;

        public MainWindow()
        {
            InitializeComponent();
            TextManager.TxblStatus = txblStatus;
            siteViewModel = new SiteViewModel();
            DataContext = siteViewModel;
            txblStatus.Text = downloadStatus.Preparation.ToString() +": Введіть дані в поля";
            StatusD.P = StatusD.R = StatusD.C = false;
        }

        public async Task ProgressDownload(long totalBytes)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextManager.TxblStatus.Text += ($"Downloaded {downloadedBytes} of {totalBytes} bytes\n");
                progressStatus.Value = (int)downloadedBytes;
                
            });
        }
        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            progressStatus.IsIndeterminate = true;
            TextManager.TxblStatus.Text = downloadStatus.Downloading.ToString();
            await Task.Delay(500);
            string fileUrl = siteViewModel.Url;
            string destinationPath = siteViewModel.SavePath;
            DownloadFile(fileUrl, destinationPath);
            TextManager.TxblStatus.Text = downloadStatus.Completed.ToString();
            await Task.Delay(500);
        }
        
        public async void DownloadFile(string fileUrl, string destinationPath)
        {
            cancellationTokenSource = new CancellationTokenSource();
            downloadedBytes = 0;
            try
            {
                using (var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;      // кількість байт отриманих в відповіді з сервера
                                                                                       
                    TextManager.TxblStatus.Text += $": {totalBytes} байтів\n";
                    await Task.Delay(1000);
                    double stBar = double.Parse(totalBytes.ToString());
                    progressStatus.IsIndeterminate = false;
                    progressStatus.Maximum = totalBytes;
                    progressStatus.Value = 0;
                    while (true)
                    {
                        cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            //TextManager.TxblStatus.Text = downloadStatus.Completed.ToString();                           
                            break;
                        }
                        if (downloadedBytes < totalBytes)
                        {
                            if (StatusD.P == true || StatusD.C == true)
                                await PauseCancel();                          
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;
                            await Task.Delay(10);
                            await ProgressDownload(totalBytes);                                                            
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextManager.TxblStatus.Text = downloadStatus.Failed.ToString();
                    progressStatus.Value = 0;
                    Task.Delay(100);
                });
            }
        }

        public async Task PauseCancel()
        {
            await Task.Run(async () =>
            {
                while (true)
                {
                    if (StatusD.R == true)
                    {
                        StatusD.R = false;  break;
                    }
                    else if (StatusD.C == true)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            TextManager.TxblStatus.Text = downloadStatus.Canceled.ToString();
                            progressStatus.Value = 0;
                            Task.Delay(100);
                        });
                        cancellationTokenSource.Cancel();
                        return;
                    }
                    await Task.Delay(500);
                }
            });
        }
        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            StatusD.P = true;
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            StatusD.C = true;
        }

        private void btnResume_Click(object sender, RoutedEventArgs e)
        {
            StatusD.R = true;
            StatusD.P = false;
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string selectedFile = openFileDialog.FileName;

                try
                {
                    File.Delete(selectedFile);
                    MessageBox.Show("Файл видалено успішно.");
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Помилка під час видалення файлу: {ex.Message}");
                }
            }
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {          
            OpenFileDialog openFileDialog = new OpenFileDialog();
            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string selectedFile = openFileDialog.FileName;

                try
                {                    
                    MessageBox.Show(selectedFile);
                    Process.Start(selectedFile);

                    //ProcessStartInfo processStartInfo = new ProcessStartInfo(selectedFile);
                    //Process.Start(processStartInfo);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    MessageBox.Show($"Помилка під час відкриття файлу: {ex.Message}");
                }
            }     
        }


    }
}

