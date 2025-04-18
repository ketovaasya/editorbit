﻿using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Project.Classes;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using BitMiracle.LibTiff.Classic;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Interactivity;
using System;
using static System.Net.Mime.MediaTypeNames;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace AvaloniaApplication1.Views
{
    public partial class MainView : UserControl
    {
        public class OpenFileData
        {
            public string Name { get; set; }
            public ushort[] Red16 { get; set; }  // Используем 16-битные массивы
            public ushort[] Green16 { get; set; }
            public ushort[] Blue16 { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }


        private List<OpenFileData> openFiles = new List<OpenFileData>();

        public MainView()
        {
            InitializeComponent();
            Project.Classes.Buffer.MainControl = this;
        }

        #region OpenFile

        public async Task LoadImageAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine("Файл не существует.");
                return;
            }

            var tempFilePath = Path.GetTempFileName();
            using (var tempFileStream = File.Create(tempFilePath))
            using (var originalFileStream = File.OpenRead(filePath))
            {
                await originalFileStream.CopyToAsync(tempFileStream);
            }

            using (var tiff = Tiff.Open(tempFilePath, "r"))
            {
                if (tiff == null)
                {
                    Debug.WriteLine("Не удалось открыть TIFF файл.");
                    return;
                }

                var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
                var bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
                var samplesPerPixel = tiff.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();

                if (bitsPerSample != 16 || samplesPerPixel < 3)
                {
                    Debug.WriteLine("Ожидалось 16-битное RGB изображение.");
                    return;
                }

                int totalPixels = width * height;
                ushort[] red16 = new ushort[totalPixels];
                ushort[] green16 = new ushort[totalPixels];
                ushort[] blue16 = new ushort[totalPixels];

                int stride = width * samplesPerPixel * 2; // 2 байта на сэмпл (16 бит)
                byte[] scanline = new byte[stride];
                bool isBigEndian = tiff.IsBigEndian();

                ushort ReadUShort(byte[] buffer, int index)
                {
                    return isBigEndian
                        ? (ushort)((buffer[index] << 8) | buffer[index + 1])
                        : (ushort)((buffer[index + 1] << 8) | buffer[index]);
                }

                for (int row = 0; row < height; row++)
                {
                    tiff.ReadScanline(scanline, row);
                    for (int col = 0; col < width; col++)
                    {
                        int pixelIndex = row * width + col;
                        int baseIndex = col * samplesPerPixel * 2;

                        red16[pixelIndex] = ReadUShort(scanline, baseIndex);
                        green16[pixelIndex] = ReadUShort(scanline, baseIndex + 2);
                        blue16[pixelIndex] = ReadUShort(scanline, baseIndex + 4);
                    }
                }

                // Сохраняем 16-битные значения без преобразования в 8-битные
                openFiles.Add(new OpenFileData
                {
                    Name = Path.GetFileName(filePath),
                    Red16 = red16,
                    Green16 = green16,
                    Blue16 = blue16,
                    Width = width,
                    Height = height
                });
            }

            File.Delete(tempFilePath);
            await LoadImageToControl(openFiles.Last());
        }

        private async Task LoadImageToControl(OpenFileData fileData)
        {
            Redactor redactor = new Redactor();
            redactor.InitializeImage(fileData);

            var tabItem = new TabItem
            {
                Header = fileData.Name,
                HeaderTemplate = (DataTemplate)TabNavigation.Resources["TabHeaderTemplate"],
                Content = redactor,
                BorderThickness = Thickness.Parse("0 0 1 0"),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(0, 3, 0, 0),
            };

            Dispatcher.UIThread.Post(() => TabNavigation.Items.Add(tabItem));
        }

        #endregion OpenFile


        #region КнопкиМеню
        private async void LoadFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog
            {
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "TIFF Images", Extensions = { "tiff", "tif" } }
                }
            };

            var result = await fileDialog.ShowAsync((Window)this.VisualRoot);

            if (result != null && result.Length > 0)
            {
                await LoadImageAsync(result[0]); // Передаем путь к файлу
            }
        }

        private void OpenNewFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Дополнительная логика для открытия нового файла, если требуется
        }


        private async void SaveFile_Click(object? sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                var fileData = openFiles.FirstOrDefault(f => f.Name == redactor.FileName);
                if (fileData == null)
                {
                    Debug.WriteLine("Файл не найден в списке.");
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    DefaultExtension = "tiff",
                    Filters = new List<FileDialogFilter>
            {
                new FileDialogFilter { Name = "TIFF Images", Extensions = { "tiff", "tif" } }
            }
                };

                var result = await saveDialog.ShowAsync((Window)this.VisualRoot);
                if (string.IsNullOrEmpty(result)) return;

                var currentData = redactor.GetCurrentImageData();
                if (currentData == null)
                {
                    Debug.WriteLine("Не удалось получить данные изображения из редактора.");
                    return;
                }

                int width = currentData.Width;
                int height = currentData.Height;

                using (var tiff = Tiff.Open(result, "w"))
                {
                    if (tiff == null)
                    {
                        Debug.WriteLine("Не удалось создать TIFF файл.");
                        return;
                    }

                    // Устанавливаем параметры изображения
                    tiff.SetField(TiffTag.IMAGEWIDTH, width);
                    tiff.SetField(TiffTag.IMAGELENGTH, height);
                    tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);        // RGB
                    tiff.SetField(TiffTag.BITSPERSAMPLE, 16);         // 16 бит на канал
                    tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
                    tiff.SetField(TiffTag.COMPRESSION, Compression.NONE);
                    tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                    tiff.SetField(TiffTag.ROWSPERSTRIP, height);
                    //tiff.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB); // Важно! Устанавливаем порядок байтов явно
                    tiff.SetField(TiffTag.SAMPLEFORMAT, SampleFormat.UINT);

                    byte[] rowBuffer = new byte[width * 6]; // 3 канала * 2 байта на канал

                    for (int row = 0; row < height; row++)
                    {
                        for (int col = 0; col < width; col++)
                        {
                            int index = row * width + col;

                            if (BitConverter.IsLittleEndian)
                            {
                                // Для little-endian: сначала младший байт, потом старший
                                rowBuffer[col * 6] = (byte)(currentData.Red16[index] & 0xFF);
                                rowBuffer[col * 6 + 1] = (byte)(currentData.Red16[index] >> 8);

                                rowBuffer[col * 6 + 2] = (byte)(currentData.Green16[index] & 0xFF);
                                rowBuffer[col * 6 + 3] = (byte)(currentData.Green16[index] >> 8);

                                rowBuffer[col * 6 + 4] = (byte)(currentData.Blue16[index] & 0xFF);
                                rowBuffer[col * 6 + 5] = (byte)(currentData.Blue16[index] >> 8);
                            }
                            else
                            {
                                // Для big-endian — как в оригинальном коде (старший байт вперед)
                                rowBuffer[col * 6] = (byte)(currentData.Red16[index] >> 8);
                                rowBuffer[col * 6 + 1] = (byte)(currentData.Red16[index] & 0xFF);

                                rowBuffer[col * 6 + 2] = (byte)(currentData.Green16[index] >> 8);
                                rowBuffer[col * 6 + 3] = (byte)(currentData.Green16[index] & 0xFF);

                                rowBuffer[col * 6 + 4] = (byte)(currentData.Blue16[index] >> 8);
                                rowBuffer[col * 6 + 5] = (byte)(currentData.Blue16[index] & 0xFF);
                            }
                        }
                        tiff.WriteScanline(rowBuffer, row);
                    }

                }
            }
        }
        #endregion КнопкиМеню

        #region Закрытие
        private void CloseFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem)
            {
                CloseTab(tabItem);
            }
        }



        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string fileName)
            {
                var tabItem = TabNavigation.Items
                    .OfType<TabItem>()
                    .FirstOrDefault(t => t.Header.ToString() == fileName);

                if (tabItem != null)
                {
                    CloseTab(tabItem);
                }
            }
        }

        private void CloseTab(TabItem tabItem)
        {
            if (tabItem != null)
            {
                TabNavigation.Items.Remove(tabItem);

                if (tabItem.Content is Redactor redactor)
                {
                    var fileData = openFiles.FirstOrDefault(f => f.Name == redactor.FileName);
                    if (fileData != null)
                    {
                        openFiles.Remove(fileData);
                    }
                }
            }
        }
        #endregion Закрытие

        private void CropButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                redactor.EnableCropping();
            }
        }

        private void RotateButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                redactor.RotateImage(90);
                var updatedData = redactor.GetCurrentImageData();
                if (updatedData != null)
                {
                    var existingData = openFiles.FirstOrDefault(f => f.Name == updatedData.Name);
                    if (existingData != null)
                    {
                        openFiles.Remove(existingData);
                    }
                    openFiles.Add(updatedData);
                }
            }
        }

        #region Save8bit
        private async void SaveFile8bit_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                var fileData = openFiles.FirstOrDefault(f => f.Name == redactor.FileName);
                if (fileData == null)
                {
                    Debug.WriteLine("Файл не найден в списке.");
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    DefaultExtension = "tiff",
                    Filters = new List<FileDialogFilter>
            {
                new FileDialogFilter { Name = "TIFF Images", Extensions = { "tiff", "tif" } }
            }
                };

                var result = await saveDialog.ShowAsync((Window)this.VisualRoot);
                if (string.IsNullOrEmpty(result)) return;

                var currentData = redactor.GetCurrentImageData();
                if (currentData == null)
                {
                    Debug.WriteLine("Не удалось получить данные изображения из редактора.");
                    return;
                }

                int width = currentData.Width;
                int height = currentData.Height;

                using (var tiff = Tiff.Open(result, "w"))
                {
                    if (tiff == null)
                    {
                        Debug.WriteLine("Не удалось создать TIFF файл.");
                        return;
                    }

                    // Устанавливаем параметры для 8-битного изображения
                    tiff.SetField(TiffTag.IMAGEWIDTH, width);
                    tiff.SetField(TiffTag.IMAGELENGTH, height);
                    tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);        // RGB
                    tiff.SetField(TiffTag.BITSPERSAMPLE, 8);          // 8 бит на канал
                    tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
                    tiff.SetField(TiffTag.COMPRESSION, Compression.NONE);
                    tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                    tiff.SetField(TiffTag.ROWSPERSTRIP, height);

                    byte[] rowBuffer = new byte[width * 3]; // 3 канала * 1 байт на канал

                    for (int row = 0; row < height; row++)
                    {
                        for (int col = 0; col < width; col++)
                        {
                            int index = row * width + col;

                            // Конвертация 16-битных значений в 8-битные (просто берем старший байт)
                            // Для более точного преобразования можно использовать нормализацию:
                            // rowBuffer[col * 3] = (byte)(currentData.Red16[index] / 257);
                            rowBuffer[col * 3] = (byte)(currentData.Red16[index] >> 8);
                            rowBuffer[col * 3 + 1] = (byte)(currentData.Green16[index] >> 8);
                            rowBuffer[col * 3 + 2] = (byte)(currentData.Blue16[index] >> 8);
                        }
                        tiff.WriteScanline(rowBuffer, row);
                    }
                }
            }
        }

        //private async void SaveAs8bitJpeg_Click(object sender, RoutedEventArgs e) => await SaveAs8BitImage("jpeg");
        //private async void SaveAs8bitPng_Click(object sender, RoutedEventArgs e) => await SaveAs8BitImage("png");

        //private async Task SaveAs8BitImage(string format)
        //{
        //    if (TabNavigation.SelectedItem is not TabItem tabItem || tabItem.Content is not Redactor redactor)
        //    {
        //        return;
        //    }

        //    var currentData = redactor.GetCurrentImageData();
        //    if (currentData == null)
        //    {
        //        return;
        //    }

        //    var saveDialog = new SaveFileDialog
        //    {
        //        DefaultExtension = format,
        //        Filters = { new FileDialogFilter {
        //    Name = $"{format.ToUpper()} Files",
        //    Extensions = { format }
        //}}
        //    };

        //    var path = await saveDialog.ShowAsync((Window)this.VisualRoot);
        //    if (string.IsNullOrEmpty(path)) return;

        //    try
        //    {
        //        // Создаем временный файл для безопасной работы с памятью
        //        var tempFile = Path.GetTempFileName();

        //        try
        //        {
        //            // Конвертируем и сохраняем через временный файл
        //            if (format == "jpeg")
        //            {
        //                await ConvertAndSaveAsJpeg(currentData, tempFile);
        //            }
        //            else if (format == "png")
        //            {
        //                await ConvertAndSaveAsPng(currentData, tempFile);
        //            }

        //            // Переносим временный файл в конечное местоположение
        //            File.Move(tempFile, path, overwrite: true);
        //        }
        //        finally
        //        {
        //            if (File.Exists(tempFile))
        //                File.Delete(tempFile);
        //        }
        //    }
        //    catch (Exception ex)
        //    {

        //    }
        //}

        //private async Task ConvertAndSaveAsJpeg(OpenFileData data, string tempPath)
        //{
        //    using var stream = new SKFileWStream(tempPath);
        //    using var pixmap = Create8BitPixmap(data);

        //    var success = pixmap.Encode(stream, SKEncodedImageFormat.Jpeg, quality: 90);
        //    if (!success)
        //        throw new Exception("JPEG encoding failed");
        //}

        //private async Task ConvertAndSaveAsPng(OpenFileData data, string tempPath)
        //{
        //    using var stream = new SKFileWStream(tempPath);
        //    using var pixmap = Create8BitPixmap(data);

        //    var success = pixmap.Encode(stream, SKEncodedImageFormat.Png, quality: 100);
        //    if (!success)
        //        throw new Exception("PNG encoding failed");
        //}

        //private SKPixmap Create8BitPixmap(OpenFileData data)
        //{
        //    byte[] pixels = new byte[data.Width * data.Height * 3];
        //    for (int i = 0; i < data.Red16.Length; i++)
        //    {
        //        pixels[i * 3] = (byte)(data.Red16[i] >> 8);      // R
        //        pixels[i * 3 + 1] = (byte)(data.Green16[i] >> 8); // G
        //        pixels[i * 3 + 2] = (byte)(data.Blue16[i] >> 8);  // B
        //    }

        //    var info = new SKImageInfo(data.Width, data.Height, SKColorType.Rgb888x, SKAlphaType.Opaque);

        //    var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        //    var ptr = handle.AddrOfPinnedObject();
        //    var pixmap = new SKPixmap(info, ptr);

        //    handle.Free();

        //    return pixmap;
        //}

        #endregion

        private void SquareDrawButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                redactor.EnableSquareDrawing();
            }
        }

        private void CircleDrawButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                redactor.EnableCircleDrawing();
            }
        }

        private void LineDrawButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                redactor.EnableLineDrawing();
            }
        }
        private void RedChannelOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                redactor.InitializeImage(openFiles.FirstOrDefault(f => f.Name == redactor.FileName));
            }
        }
        private void GreenChannelOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                redactor.SwapGreenRedChannels();

                redactor.SwapGreenBlueChannels();
            }
        }
        private void BlueChannelOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                // Меняем местами красный и синий каналы
                redactor.SwapRedBlueChannels();
            }
        }
        
    }
}