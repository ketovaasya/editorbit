using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Linq;
using static AvaloniaApplication1.Views.MainView;

namespace AvaloniaApplication1
{
    public partial class Redactor : UserControl
    {
        private bool isDragging;
        private Point lastPosition;
        private double currentScale = 1.0;
        private double minScale = 0.1;
        private double maxScale = 5.0;
        private Point startPoint;
        private bool isCropping = false;

        public Redactor()
        {
            InitializeComponent();
            InitializeEventHandlers();
        }

        public string FileName { get; private set; }
        private OpenFileData currentFileData;
        private ushort[] currentRed16;
        private ushort[] currentGreen16;
        private ushort[] currentBlue16;
        private int currentWidth;
        private int currentHeight;

        public void InitializeImage(OpenFileData fileData)
        {
            currentFileData = fileData;
            FileName = fileData.Name;

            // Сохраняем оригинальные данные без изменений
            currentRed16 = fileData.Red16.ToArray(); // Копируем массивы
            currentGreen16 = fileData.Green16.ToArray();
            currentBlue16 = fileData.Blue16.ToArray();
            currentWidth = fileData.Width;
            currentHeight = fileData.Height;

            // Для отображения создаем копию с нормализацией
            DisplayImage();
        }

        private void DisplayImage()
        {
            var displayData = new OpenFileData
            {
                Name = FileName,
                Width = currentWidth,
                Height = currentHeight,
                Red16 = currentRed16,
                Green16 = currentGreen16,
                Blue16 = currentBlue16
            };

            PrintImageChunk(displayData, ImageChu, 0, 0, currentWidth, currentHeight);
            ImageCanvas.Width = currentWidth;
            ImageCanvas.Height = currentHeight;
            CenterImage();
        }

        private void InitializeEventHandlers()
        {
            ImageChu.PointerWheelChanged += ImageChu_PointerWheelChanged;
            ImageCanvas.PointerPressed += ImageCanvas_PointerPressedCrop;
            ImageCanvas.PointerMoved += ImageCanvas_PointerMovedCrop;
            ImageCanvas.PointerReleased += ImageCanvas_PointerReleasedCrop;
        }

        #region Масштабирование и перемещение

        private void ImageChu_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            double zoomFactor = 0.1;
            var oldScale = currentScale;

            if (e.Delta.Y > 0)
            {
                currentScale = Math.Min(maxScale, currentScale + zoomFactor);
            }
            else
            {
                currentScale = Math.Max(minScale, currentScale - zoomFactor);
            }

            UpdateScale();
            e.Handled = true;
        }

        private void UpdateScale()
        {
            var scaleTransform = new ScaleTransform(currentScale, currentScale);
            ImageChu.RenderTransform = scaleTransform;

            ImageCanvas.Width = ImageChu.Source.Size.Width * currentScale;
            ImageCanvas.Height = ImageChu.Source.Size.Height * currentScale;

            CenterImage();
        }

        private void CenterImage()
        {
            if (ImageChu.Source != null)
            {
                var imageWidth = ImageChu.Source.Size.Width * currentScale;
                var imageHeight = ImageChu.Source.Size.Height * currentScale;

                Canvas.SetLeft(ImageChu, (ImageCanvas.Width - imageWidth) / 2);
                Canvas.SetTop(ImageChu, (ImageCanvas.Height - imageHeight) / 2);
            }
        }

        #endregion

        #region Обрезка

        public void EnableCropping()
        {
            isCropping = true;
            CropSelection.IsVisible = true;
        }

        private void ImageCanvas_PointerPressedCrop(object sender, PointerPressedEventArgs e)
        {
            if (!isCropping) return;

            startPoint = e.GetCurrentPoint(ImageCanvas).Position;
            CropSelection.Width = 0;
            CropSelection.Height = 0;
            CropSelection.Margin = new Thickness(startPoint.X, startPoint.Y, 0, 0);
        }

        private void ImageCanvas_PointerMovedCrop(object sender, PointerEventArgs e)
        {
            if (!isCropping || e.GetCurrentPoint(ImageCanvas).Properties.IsLeftButtonPressed == false) return;

            var currentPoint = e.GetCurrentPoint(ImageCanvas).Position;
            double x = Math.Min(startPoint.X, currentPoint.X);
            double y = Math.Min(startPoint.Y, currentPoint.Y);
            double width = Math.Abs(startPoint.X - currentPoint.X);
            double height = Math.Abs(startPoint.Y - currentPoint.Y);

            CropSelection.Margin = new Thickness(x, y, 0, 0);
            CropSelection.Width = width;
            CropSelection.Height = height;
        }

        private void ImageCanvas_PointerReleasedCrop(object sender, PointerReleasedEventArgs e)
        {
            if (!isCropping) return;

            isCropping = false;
            CropImage();
            CropSelection.IsVisible = false;
        }

        private void CropImage()
        {
            int x = (int)(CropSelection.Margin.Left / currentScale);
            int y = (int)(CropSelection.Margin.Top / currentScale);
            int width = (int)(CropSelection.Width / currentScale);
            int height = (int)(CropSelection.Height / currentScale);

            if (width <= 0 || height <= 0 ||
                x + width > currentWidth || y + height > currentHeight) return;

            var red16 = new ushort[width * height];
            var green16 = new ushort[width * height];
            var blue16 = new ushort[width * height];

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int srcIndex = (y + row) * currentWidth + (x + col);
                    int dstIndex = row * width + col;

                    red16[dstIndex] = currentRed16[srcIndex];
                    green16[dstIndex] = currentGreen16[srcIndex];
                    blue16[dstIndex] = currentBlue16[srcIndex];
                }
            }

            // Обновляем текущие данные
            currentRed16 = red16;
            currentGreen16 = green16;
            currentBlue16 = blue16;
            currentWidth = width;
            currentHeight = height;

            // Обновляем отображение
            var updatedData = new OpenFileData
            {
                Name = FileName,
                Width = width,
                Height = height,
                Red16 = red16,
                Green16 = green16,
                Blue16 = blue16
            };

            PrintImageChunk(updatedData, ImageChu, 0, 0, width, height);
            ImageCanvas.Width = width;
            ImageCanvas.Height = height;
            Canvas.SetLeft(ImageChu, 0);
            Canvas.SetTop(ImageChu, 0);
        }

        #endregion

        #region Поворот

        public void RotateImage(double angle)
        {
            int oldWidth = currentWidth;
            int oldHeight = currentHeight;

            int newWidth = oldHeight;
            int newHeight = oldWidth;

            var red16 = new ushort[newWidth * newHeight];
            var green16 = new ushort[newWidth * newHeight];
            var blue16 = new ushort[newWidth * newHeight];

            for (int y = 0; y < oldHeight; y++)
            {
                for (int x = 0; x < oldWidth; x++)
                {
                    int oldIndex = y * oldWidth + x;
                    int newX = y;
                    int newY = newHeight - x - 1;
                    int newIndex = newY * newWidth + newX;

                    red16[newIndex] = currentRed16[oldIndex];
                    green16[newIndex] = currentGreen16[oldIndex];
                    blue16[newIndex] = currentBlue16[oldIndex];
                }
            }

            // Обновляем текущие данные
            currentRed16 = red16;
            currentGreen16 = green16;
            currentBlue16 = blue16;
            currentWidth = newWidth;
            currentHeight = newHeight;

            // Обновляем отображение
            var updatedData = new OpenFileData
            {
                Name = FileName,
                Width = newWidth,
                Height = newHeight,
                Red16 = red16,
                Green16 = green16,
                Blue16 = blue16
            };

            PrintImageChunk(updatedData, ImageChu, 0, 0, newWidth, newHeight);
            ImageCanvas.Width = newWidth;
            ImageCanvas.Height = newHeight;
            CenterImage();
        }

        public OpenFileData GetCurrentImageData()
        {
            return new OpenFileData
            {
                Name = this.FileName,
                Red16 = this.currentRed16,
                Green16 = this.currentGreen16,
                Blue16 = this.currentBlue16,
                Width = this.currentWidth,
                Height = this.currentHeight
            };
        }

        #endregion

        public void PrintImageChunk(OpenFileData file, Image imageControl, int chunkX, int chunkY, int chunkWidth, int chunkHeight)
        {
            var bitmap = new WriteableBitmap(
                new PixelSize(chunkWidth, chunkHeight),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888);

            using (var framebuffer = bitmap.Lock())
            {
                unsafe
                {
                    var buffer = (uint*)framebuffer.Address;
                    int totalPixels = file.Width * file.Height;

                    ushort[] r16 = file.Red16;
                    ushort[] g16 = file.Green16;
                    ushort[] b16 = file.Blue16;

                    // Автоматическое контрастирование для отображения
                    ushort minR = r16.Min();
                    ushort maxR = r16.Max();
                    ushort minG = g16.Min();
                    ushort maxG = g16.Max();
                    ushort minB = b16.Min();
                    ushort maxB = b16.Max();

                    double rangeR = Math.Max(1, maxR - minR);
                    double rangeG = Math.Max(1, maxG - minG);
                    double rangeB = Math.Max(1, maxB - minB);

                    for (int y = 0; y < chunkHeight; y++)
                    {
                        for (int x = 0; x < chunkWidth; x++)
                        {
                            int srcX = chunkX + x;
                            int srcY = chunkY + y;

                            if (srcX >= file.Width || srcY >= file.Height)
                            {
                                buffer[y * chunkWidth + x] = 0xFF000000;
                                continue;
                            }

                            int index = srcY * file.Width + srcX;

                            // Нормализация только для отображения
                            byte r = (byte)((r16[index] - minR) * 255 / rangeR);
                            byte g = (byte)((g16[index] - minG) * 255 / rangeG);
                            byte b = (byte)((b16[index] - minB) * 255 / rangeB);

                            buffer[y * chunkWidth + x] = (uint)(255 << 24 | b << 16 | g << 8 | r);
                        }
                    }
                }
            }

            imageControl.Source = bitmap;
            UpdateScale();
        }
    }
}