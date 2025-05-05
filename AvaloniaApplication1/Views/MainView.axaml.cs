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
using Avalonia.Controls;

namespace AvaloniaApplication1.Views
{
    public partial class MainView : UserControl
    {
        public class OpenFileData
        {
            public string Name { get; set; }
            public ushort[] Red16 { get; set; }  // 16-битные массивы
            public ushort[] Green16 { get; set; }
            public ushort[] Blue16 { get; set; }
            public ushort[] Alpha16 { get; set; } // Добавлен альфа-канал
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

                if (bitsPerSample != 16)
                {
                    Debug.WriteLine("Ожидалось 16-битное изображение.");
                    return;
                }

                int totalPixels = width * height;
                ushort[] red16 = new ushort[totalPixels];
                ushort[] green16 = new ushort[totalPixels];
                ushort[] blue16 = new ushort[totalPixels];
                ushort[] alpha16 = null;

                int stride = width * samplesPerPixel * 2;
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

                        if (samplesPerPixel >= 3) // RGB или RGBA
                        {
                            red16[pixelIndex] = ReadUShort(scanline, baseIndex);
                            green16[pixelIndex] = ReadUShort(scanline, baseIndex + 2);
                            blue16[pixelIndex] = ReadUShort(scanline, baseIndex + 4);
                        }
                        else if (samplesPerPixel == 1) // Grayscale
                        {
                            // Для одноканальных изображений используем один канал для всех трех
                            ushort value = ReadUShort(scanline, baseIndex);
                            red16[pixelIndex] = value;
                            green16[pixelIndex] = value;
                            blue16[pixelIndex] = value;
                        }

                        if (samplesPerPixel == 4) // RGBA
                        {
                            if (alpha16 == null)
                                alpha16 = new ushort[totalPixels];
                            alpha16[pixelIndex] = ReadUShort(scanline, baseIndex + 6);
                        }
                    }
                }

                // Нормализуем каждый канал отдельно, если он полностью нулевой
                NormalizeChannelIfZero(red16);
                NormalizeChannelIfZero(green16);
                NormalizeChannelIfZero(blue16);

                openFiles.Add(new OpenFileData
                {
                    Name = Path.GetFileName(filePath),
                    Red16 = red16,
                    Green16 = green16,
                    Blue16 = blue16,
                    Alpha16 = alpha16,  // Будет null для RGB изображений
                    Width = width,
                    Height = height
                });
            }

            File.Delete(tempFilePath);
            await LoadImageToControl(openFiles.Last());
        }

        // Улучшенная нормализация каналов
        private void NormalizeChannelIfZero(ushort[] channelData)
        {
            bool allZero = true;
            foreach (var value in channelData)
            {
                if (value != 0)
                {
                    allZero = false;
                    break;
                }
            }

            if (allZero)
            {
                for (int i = 0; i < channelData.Length; i++)
                {
                    channelData[i] = ushort.MaxValue;
                }
            }
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

        private async void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                var fileData = openFiles.FirstOrDefault(f => f.Name == redactor.FileName);
                if (fileData == null) return;

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
                SaveTiffFile(result, currentData);
            }
        }

        private void SaveTiffFile(string path, OpenFileData data)
        {
            using (var tiff = Tiff.Open(path, "w"))
            {
                if (tiff == null) return;

                int samplesPerPixel = data.Alpha16 != null ? 4 : 3;

                tiff.SetField(TiffTag.IMAGEWIDTH, data.Width);
                tiff.SetField(TiffTag.IMAGELENGTH, data.Height);
                tiff.SetField(TiffTag.SAMPLESPERPIXEL, samplesPerPixel);
                tiff.SetField(TiffTag.BITSPERSAMPLE, 16);
                tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
                tiff.SetField(TiffTag.COMPRESSION, Compression.NONE);
                tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                tiff.SetField(TiffTag.ROWSPERSTRIP, data.Height);
                tiff.SetField(TiffTag.SAMPLEFORMAT, SampleFormat.UINT);

                byte[] rowBuffer = new byte[data.Width * samplesPerPixel * 2];

                for (int row = 0; row < data.Height; row++)
                {
                    for (int col = 0; col < data.Width; col++)
                    {
                        int index = row * data.Width + col;
                        int baseIndex = col * samplesPerPixel * 2;

                        // Записываем каналы с учетом порядка байтов
                        WriteChannel(data.Red16[index], rowBuffer, baseIndex);
                        WriteChannel(data.Green16[index], rowBuffer, baseIndex + 2);
                        WriteChannel(data.Blue16[index], rowBuffer, baseIndex + 4);

                        if (samplesPerPixel == 4)
                        {
                            WriteChannel(data.Alpha16[index], rowBuffer, baseIndex + 6);
                        }
                    }
                    tiff.WriteScanline(rowBuffer, row);
                }
            }
        }

        private void WriteChannel(ushort value, byte[] buffer, int index)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = (byte)(value & 0xFF);
                buffer[index + 1] = (byte)(value >> 8);
            }
            else
            {
                buffer[index] = (byte)(value >> 8);
                buffer[index + 1] = (byte)(value & 0xFF);
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

        #region Channel Operations
        private async void SwapChannelsButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                var dialog = new ChannelSwapDialog();

                // Center the dialog relative to the main window
                if (VisualRoot is Window parentWindow)
                {
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    dialog.Position = new PixelPoint(
                        parentWindow.Position.X + (int)(parentWindow.Width / 2 - dialog.Width / 2),
                        parentWindow.Position.Y + (int)(parentWindow.Height / 2 - dialog.Height / 2));
                }

                var result = await dialog.ShowDialog<ChannelSwapResult>((Window)VisualRoot);

                if (result != null)
                {
                    redactor.SwapChannels(result.SourceChannel, result.DestinationChannel);

                    // Update the display immediately
                    var currentData = redactor.GetCurrentImageData();
                    if (currentData != null)
                    {
                        var existingData = openFiles.FirstOrDefault(f => f.Name == currentData.Name);
                        if (existingData != null)
                        {
                            openFiles.Remove(existingData);
                        }
                        openFiles.Add(currentData);
                    }
                }
            }
        }


        private void AddAlphaChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                redactor.AddAlphaChannel();
            }
        }

        private void RemoveAlphaChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem && tabItem.Content is Redactor redactor)
            {
                redactor.RemoveAlphaChannel();
            }
        }
        #endregion

    }
}