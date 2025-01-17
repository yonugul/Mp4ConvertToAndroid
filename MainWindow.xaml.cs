﻿using Javi.FFmpeg;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mp4ConvertToAndroid
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Set this to the location of ffmpeg.exe on your machine:
        //private string FFmpegFileName = @"C:\Users\yonug\Desktop\123123\ffmpeg.exe";
        private string FFmpegFileName = AppDomain.CurrentDomain.BaseDirectory + @"\ffmpeg.exe";

        private string InputFile;

        private string convertFileExt = ".mp4";
        // Note that cancellation is only implemented in this demo in method ButtonConvertEAC_Click
        private CancellationTokenSource CancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            totalCpuCount.Content = Environment.ProcessorCount;
            showPercent();
            code = baseCode + $" -threads {useCpuCount.Text}";
            AddMessage("Dönüştürme için kullanılan ffmpeg kodu : " + code, c: Colors.Yellow, sticky: true);
            if (!string.IsNullOrEmpty(Properties.Settings.Default.LastFolder))
            {
                folder.Text = Properties.Settings.Default.LastFolder;
            }

            if (customcode != null)
            {
                customcode.Text = code;
            }
        }
        private void AddMessage(string text, TimeSpan? d = null, Color? c = null, bool clearPrevious = false, double progressB = 0, bool sticky = false)
        {
            var tBlock = sticky ? stickyTexts : progressTexts;
            if (tBlock == null)
            {
                tBlock = progressTexts;
            }
            if (clearPrevious)
            {
                progressTexts.Inlines.Clear();
            }
            else
            {
                tBlock.Inlines.Add(new LineBreak());
            }
            var textcolor = c ?? Colors.Green;

            var newText = new Run
            {
                Text = text,
                Foreground = new SolidColorBrush(textcolor)
            };
            tBlock.Inlines.Add(newText);
            if (d.HasValue)
            {
                var br = d.Value.TotalSeconds < 1 ? Brushes.Green :
                    d.Value.TotalSeconds >= 1 && d.Value.TotalSeconds < 5 ? Brushes.Yellow : Brushes.Red;
                tBlock.Inlines.Add(new Run { Text = $"işlem süresi : {d.Value.TotalSeconds:0.####} s", Foreground = br });

            }

            if (progressB > 0)
            {
                progressBar.Value = progressB;

            }

            Application.Current.Dispatcher?.Invoke(DispatcherPriority.Background, new Action(UpdateLayout));
            if (allTime != null)
            {
                lblProcessTime.Content = allTime.Elapsed.ToString(@"hh\:mm\:ss");
            }
            scrollViewer?.ScrollToBottom();
            stickyScroll?.ScrollToBottom();

        }

        private const string baseCode = "-c:v libx264 -profile:v baseline -level 3.0 -preset:v veryfast";
        private const string webmbaseCode = "-vf scale=1680:-2 -r 40 -max_muxing_queue_size 1024 -preset:v veryfast";
        private static string code;
        private static List<string> filePaths;
        private bool state = true;
        private int counter = 0;
        private int success = 0, error = 0;
        private double completed = 0, all = 0;
        private Stopwatch allTime;
        private void ButtonRun_Click(object sender, RoutedEventArgs e)
        {


            try
            {
                buttonConvert.IsEnabled = false;
                btnPause.IsEnabled = true;
                Properties.Settings.Default.LastFolder = folder.Text;
                Properties.Settings.Default.Save();
                //DirSearch(folder.Text);
                filePaths = Directory.GetFiles(folder.Text, "*" + convertFileExt + "*", SearchOption.AllDirectories).ToList();
                var convertedBeforeList = Properties.Settings.Default.ConvertedVideos.Cast<string>().ToList();
                var intersect = filePaths.Intersect(convertedBeforeList).Count();
                if (intersect > 0)
                {
                    AddMessage($"{intersect} önceden dönüştürülmüş toplam {filePaths.Count} dosya", sticky: true);
                    filePaths = filePaths.Except(convertedBeforeList).ToList();

                }

                allTime = Stopwatch.StartNew();
                all = filePaths.Count;
                convert();


            }
            catch (Exception exception)
            {
                AddMessage("hata oluştu :" + exception.Message, c: Colors.Red);
                buttonConvert.IsEnabled = true;
            }



        }

        private void convert()
        {

            AddMessage($"Dönüştürülecek Video Sayısı : {all - completed}", sticky: true);



            while (counter < all && state)
            {
                var inputFile = filePaths[counter];
                try
                {
                    //AddMessage("***Start FFmpeg");
                    //var inputFile = "s1old.mp4";
                    //var outputFile = "s1.mp4";
                    var outputFile = inputFile.Replace(convertFileExt, "_new.mp4");
                    var backupFile = inputFile.Replace(convertFileExt, ".backup");
                    var convert = false;
                    var thisVideoStart = allTime.Elapsed;
                    if (!File.Exists(backupFile))
                    {
                        if (inputFile.EndsWith(convertFileExt))
                        {
                            if (onlyBigFiles.IsChecked.HasValue && onlyBigFiles.IsChecked.Value)
                            {
                                var size = new FileInfo(inputFile).Length / (double)(1024 * 1024);
                                if (size > 10)
                                {
                                    //convert
                                    convert = true;
                                }
                                else
                                {
                                    //do not convert
                                    AddMessage($" || {inputFile} dönüştürülmedi, dosya büyük değil ", allTime.Elapsed.Subtract(thisVideoStart));
                                    error++;
                                }
                            }
                            else
                            {
                                //convert
                                convert = true;
                            }

                            if (convert)
                            {
                                using (var ffmpeg = new FFmpeg(FFmpegFileName))
                                {
                                    try
                                    {
                                        AddMessage($" > {inputFile} dönüştürme işlemi başladı ");
                                        var commandLine = string.Format($"-i \"{inputFile}\" {code} \"{outputFile}\"");
                                        ffmpeg.Run(inputFile, outputFile, commandLine);
                                        AddMessage($" > {outputFile} dosyası başarıyla oluşturuldu");
                                    }
                                    catch (Exception ex)
                                    {
                                        AddMessage($"Hata: {ex.Message}");
                                        // Gerekli loglama veya hata yönetim işlemleri
                                    }
                                    //AddMessage($" > {inputFile} dönüştürme işlemi başladı ");
                                    //var commandLine = string.Format($"-i \"{inputFile}\" {code} \"{outputFile}\"");
                                    //ffmpeg.Run(inputFile, outputFile, commandLine);
                                }
                                AddMessage($" || {inputFile} dönüştürüldü ", allTime.Elapsed.Subtract(thisVideoStart));
                                success++;
                                //eski video ile yeni video yer değiştiriliyor
                                MoveWithReplace(inputFile, backupFile);
                                File.Delete(inputFile); // Delete the existing file if exists
                                MoveWithReplace(outputFile, Path.ChangeExtension(inputFile, ".mp4"));
                                if (checkBox.IsChecked.HasValue && checkBox.IsChecked.Value)
                                {
                                    File.Delete(backupFile);
                                }
                            }
                        }
                        else
                        {
                            //bu dosyanın video çözüm öğretmenleri tarafından silindiği düşünülerek silinmesi sağlanacak
                            File.Delete(inputFile);
                            AddMessage($"{inputFile} silindi - çözüm öğretmeni tarafından silinen video", c: Colors.Red, sticky: true);
                            //File.Move(inputFile, Path.ChangeExtension(inputFile,".deleted"));
                        }

                    }
                    else
                    {
                        AddMessage($"{inputFile} önceden dönüştürülmüş ");
                        success++;
                    }
                    //Properties.Settings.Default.ConvertedVideos.Add(inputFile);

                }
                catch (Exception ex)
                {
                    AddMessage($" ! {inputFile} dönüştürülemedi  : " + ex.Message, c: Colors.Red, sticky: true);
                    //Properties.Settings.Default.errors.Add(inputFile);
                    error++;
                }
                //Properties.Settings.Default.Save();
                completed++;
                counter++;
                var percent = completed / all * 100;
                AddMessage($"ilerleme {completed} / {all} % {percent:0.####}", progressB: percent, clearPrevious: true);
            }

            if (completed == all)
            {
                allTime.Stop();
                AddMessage($" >>> Tüm videolar başarıyla dönüştürüldü <<< ", allTime.Elapsed);
                if (success > 0)
                {
                    AddMessage($"Başarılı  : {success}", c: Colors.Green);

                }
                if (error > 0)
                {
                    AddMessage($"Başarısız : {error}", c: Colors.Red);

                }
                counter = 0;
                completed = 0;
                all = 0;
                success = 0;
                error = 0;
                buttonConvert.IsEnabled = true;
                btnPlay.IsEnabled = false;
                btnPause.IsEnabled = false;
            }

        }
        public static void MoveWithReplace(string sourceFileName, string destFileName)
        {
            //first, delete target file if exists, as File.Move() does not support overwrite
            if (File.Exists(destFileName))
            {
                File.Delete(destFileName);
            }

            File.Move(sourceFileName, destFileName);

        }
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            progressTexts.Inlines.Clear();
        }

        private void showPercent()
        {
            if (int.TryParse(useCpuCount.Text, out var useCpu))
            {
                cpuUsePercent.Content = "% " + (int)((decimal)((decimal)useCpu / (decimal)Environment.ProcessorCount) * 100);
            }
        }

        private void UseCpuCount_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (totalCpuCount != null)
            {
                showPercent();
                //code = baseCode + useCpuCount.Text;
                code = baseCode + $" -threads {useCpuCount.Text}";
                AddMessage("Dönüştürme için kullanılan ffmpeg kodu : " + code, c: Colors.Yellow, sticky: true);
                if (customcode != null)
                {
                    customcode.Text = code;
                }
            }

        }

        private void Play(object sender, RoutedEventArgs e)
        {
            state = true;
            btnPause.IsEnabled = true;
            btnPlay.IsEnabled = false;
            allTime = Stopwatch.StartNew();
            convert();
        }
        private void Pause(object sender, RoutedEventArgs e)
        {
            allTime.Stop();
            state = false;
            btnPause.IsEnabled = false;
            btnPlay.IsEnabled = true;
        }

        private void ClearBackup(object sender, RoutedEventArgs e)
        {
            var backupFiles = Directory.GetFiles(folder.Text, "*.backup*", SearchOption.AllDirectories).ToList();
            AddMessage($"silinecek backup sayısı : {backupFiles.Count}");
            backupFiles.ForEach(File.Delete);
            AddMessage($"tüm dosyalar silindi");
        }

        private void mp4_OnSelected(object sender, RoutedEventArgs e)
        {
            convertFileExt = ".mp4";
            code = baseCode + $" -threads {useCpuCount.Text}";
            AddMessage("Dönüştürme için kullanılan ffmpeg kodu : " + code, c: Colors.Yellow, sticky: true);
            if (customcode != null)
            {
                customcode.Text = code;
            }
        }

        private void webm_OnSelected(object sender, RoutedEventArgs e)
        {
            convertFileExt = ".webm";
            code = webmbaseCode + $" -threads {useCpuCount.Text}";
            AddMessage("Dönüştürme için kullanılan ffmpeg kodu : " + code, c: Colors.Yellow, sticky: true);
            if (customcode != null)
            {
                customcode.Text = code;
            }
        }
        private void CustomCode_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            code = customcode.Text;
        }

        private void Clear_History(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ConvertedVideos.Clear();
            Properties.Settings.Default.errors.Clear();
            Properties.Settings.Default.Save();
        }
    }
}

