using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
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

        public void InitializeImage(OpenFileData fileData)
        {
            currentFileData = fileData;
            FileName = fileData.Name;
            PrintImageChunk(fileData, ImageChu, 0, 0, fileData.Width, fileData.Height);

            ImageCanvas.Width = fileData.Width;
            ImageCanvas.Height = fileData.Height;

            CenterImage();
        }


        private void InitializeEventHandlers()
        {
            ImageChu.PointerWheelChanged += ImageChu_PointerWheelChanged;
            ImageCanvas.PointerPressed += ImageCanvas_PointerPressedCrop;
            ImageCanvas.PointerMoved += ImageCanvas_PointerMovedCrop;
            ImageCanvas.PointerReleased += ImageCanvas_PointerReleasedCrop;
        }

        #region Масшатбирования и перемещение


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

            // ��������� ������ Canvas
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
                x + width > currentFileData.Width || y + height > currentFileData.Height) return;

            var red16 = new ushort[width * height];
            var green16 = new ushort[width * height];
            var blue16 = new ushort[width * height];

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int srcIndex = (y + row) * currentFileData.Width + (x + col);
                    int dstIndex = row * width + col;

                    red16[dstIndex] = currentFileData.Red16[srcIndex];
                    green16[dstIndex] = currentFileData.Green16[srcIndex];
                    blue16[dstIndex] = currentFileData.Blue16[srcIndex];
                }
            }

            currentFileData = new OpenFileData
            {
                Name = FileName,
                Width = width,
                Height = height,
                Red16 = red16,
                Green16 = green16,
                Blue16 = blue16
            };

            PrintImageChunk(currentFileData, ImageChu, 0, 0, width, height);
            ImageCanvas.Width = width;
            ImageCanvas.Height = height;
            Canvas.SetLeft(ImageChu, 0);
            Canvas.SetTop(ImageChu, 0);
        }


        #endregion

        #region Поворот
        public void RotateImage(double angle)
        {
            int oldWidth = currentFileData.Width;
            int oldHeight = currentFileData.Height;

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

                    red16[newIndex] = currentFileData.Red16[oldIndex];
                    green16[newIndex] = currentFileData.Green16[oldIndex];
                    blue16[newIndex] = currentFileData.Blue16[oldIndex];
                }
            }

            currentFileData = new OpenFileData
            {
                Name = FileName,
                Width = newWidth,
                Height = newHeight,
                Red16 = red16,
                Green16 = green16,
                Blue16 = blue16
            };

            PrintImageChunk(currentFileData, ImageChu, 0, 0, newWidth, newHeight);
            ImageCanvas.Width = newWidth;
            ImageCanvas.Height = newHeight;
            CenterImage();
        }


        public OpenFileData GetCurrentImageData()
        {
            return currentFileData;
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

                    // Вычисляем min и max для автоконтраста
                    ushort minR = ushort.MaxValue, maxR = ushort.MinValue;
                    ushort minG = ushort.MaxValue, maxG = ushort.MinValue;
                    ushort minB = ushort.MaxValue, maxB = ushort.MinValue;

                    for (int i = 0; i < totalPixels; i++)
                    {
                        if (r16[i] < minR) minR = r16[i];
                        if (r16[i] > maxR) maxR = r16[i];
                        if (g16[i] < minG) minG = g16[i];
                        if (g16[i] > maxG) maxG = g16[i];
                        if (b16[i] < minB) minB = b16[i];
                        if (b16[i] > maxB) maxB = b16[i];
                    }

                    double rangeR = maxR - minR;
                    double rangeG = maxG - minG;
                    double rangeB = maxB - minB;

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

                            byte r = (byte)(((r16[index] - minR) / rangeR) * 255.0);
                            byte g = (byte)(((g16[index] - minG) / rangeG) * 255.0);
                            byte b = (byte)(((b16[index] - minB) / rangeB) * 255.0);
                            byte a = 255;

                            buffer[y * chunkWidth + x] = (uint)(a << 24 | b << 16 | g << 8 | r);
                        }
                    }
                }
            }

            imageControl.Source = bitmap;
            UpdateScale();
        }

    }
}