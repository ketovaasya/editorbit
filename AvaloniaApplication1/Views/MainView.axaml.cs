using Avalonia.Controls;
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
            Buffer.MainControl = this;
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

                for (int row = 0; row < height; row++)
                {
                    tiff.ReadScanline(scanline, row);
                    for (int col = 0; col < width; col++)
                    {
                        int pixelIndex = row * width + col;
                        int baseIndex = col * samplesPerPixel * 2;

                        red16[pixelIndex] = (ushort)((scanline[baseIndex] << 8) | scanline[baseIndex + 1]);
                        green16[pixelIndex] = (ushort)((scanline[baseIndex + 2] << 8) | scanline[baseIndex + 3]);
                        blue16[pixelIndex] = (ushort)((scanline[baseIndex + 4] << 8) | scanline[baseIndex + 5]);
                    }
                }

                // Сохраняем 16-битные значения без преобразования в 8-битные
                openFiles.Add(new OpenFileData
                {
                    Name = Path.GetFileName(filePath),
                    Red16 = red16,  // Изменили поле для 16-битных данных
                    Green16 = green16,  // Изменили поле для 16-битных данных
                    Blue16 = blue16,  // Изменили поле для 16-битных данных
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

            // Создаём TabItem, где Header – это строка (имя файла),
            // и благодаря HeaderTemplate будет сформирован кастомный заголовок с кнопкой "X"
            var tabItem = new TabItem
            {
                Header = fileData.Name,
                HeaderTemplate = (DataTemplate)TabNavigation.Resources["TabHeaderTemplate"], // Добавлено
                Content = redactor,
                BorderThickness = Thickness.Parse("0 0 1 0"),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(0, 3, 0, 0),
            };

            // Добавляем вкладку в UI-потоке
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

                if (redactor.ImageChu.Source is WriteableBitmap writeableBitmap)
                {
                    int width = writeableBitmap.PixelSize.Width;
                    int height = writeableBitmap.PixelSize.Height;
                    int totalPixels = width * height;

                    // Открываем файл для записи в TIFF с 16 битами
                    using (var tiff = Tiff.Open(result, "w"))
                    {
                        if (tiff == null)
                        {
                            Debug.WriteLine("Не удалось создать TIFF файл.");
                            return;
                        }

                        tiff.SetField(TiffTag.IMAGEWIDTH, width);
                        tiff.SetField(TiffTag.IMAGELENGTH, height);
                        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);  // 3 компонента на пиксель (RGB)
                        tiff.SetField(TiffTag.BITSPERSAMPLE, 16);  // 16 бит на канал
                        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
                        tiff.SetField(TiffTag.COMPRESSION, Compression.NONE);
                        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                        tiff.SetField(TiffTag.ROWSPERSTRIP, height);

                        // Буфер для записи строки данных
                        byte[] rowBuffer = new byte[width * 6];  // 6 байтов на пиксель (16 бит на канал для RGB)

                        for (int row = height - 1; row >= 0; row--)
                        {
                            for (int col = 0; col < width; col++)
                            {
                                int index = row * width + col;

                                // Используем 16-битные данные для каждого канала
                                rowBuffer[col * 6] = (byte)(fileData.Red16[index] >> 8);    // Красный, старший байт
                                rowBuffer[col * 6 + 1] = (byte)(fileData.Red16[index] & 0xFF); // Красный, младший байт

                                rowBuffer[col * 6 + 2] = (byte)(fileData.Green16[index] >> 8);  // Зеленый, старший байт
                                rowBuffer[col * 6 + 3] = (byte)(fileData.Green16[index] & 0xFF); // Зеленый, младший байт

                                rowBuffer[col * 6 + 4] = (byte)(fileData.Blue16[index] >> 8);   // Синий, старший байт
                                rowBuffer[col * 6 + 5] = (byte)(fileData.Blue16[index] & 0xFF);  // Синий, младший байт
                            }

                            // Запись строки в TIFF
                            tiff.WriteScanline(rowBuffer, height - row - 1);
                        }

                        tiff.Close();
                    }
                }
            }
        }



        private void CloseFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (TabNavigation.SelectedItem is TabItem tabItem)
            {
                CloseTab(tabItem);
            }
        }

        #endregion КнопкиМеню

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

    }
}



