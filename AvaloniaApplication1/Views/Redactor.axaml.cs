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

        public void InitializeImage(OpenFileData fileData)
        {
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

        #region Маштабирование и Перемещение


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

            // Обновляем размер Canvas
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
            CropSelection.IsVisible = true; // Показываем элемент
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
            CropSelection.IsVisible = false; // Скрываем выделение после обрезки
        }

        private void CropImage()
        {
            if (ImageChu.Source is WriteableBitmap sourceBitmap)
            {
                int sourceWidth = sourceBitmap.PixelSize.Width;
                int sourceHeight = sourceBitmap.PixelSize.Height;

                // Получаем координаты выделенной области с учётом масштаба
                int x = (int)(CropSelection.Margin.Left / currentScale);
                int y = (int)(CropSelection.Margin.Top / currentScale);
                int width = (int)(CropSelection.Width / currentScale);
                int height = (int)(CropSelection.Height / currentScale);

                // Убедимся, что размеры валидны
                if (width <= 0 || height <= 0 || x + width > sourceWidth || y + height > sourceHeight) return;

                // Создаём новый WriteableBitmap для обрезанного изображения
                var croppedBitmap = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888); // Используем Bgra8888 для 32-битного цвета

                using (var framebuffer = croppedBitmap.Lock())
                using (var sourceFrameBuffer = sourceBitmap.Lock())
                {
                    unsafe
                    {
                        var sourceBuffer = (byte*)sourceFrameBuffer.Address;
                        var targetBuffer = (byte*)framebuffer.Address;

                        int sourceStride = sourceBitmap.PixelSize.Width * 4; // Страйд для исходного изображения
                        int targetStride = width * 4; // Страйд для обрезанного изображения

                        // Копируем пиксели из исходного изображения в обрезанное
                        for (int row = 0; row < height; row++)
                        {
                            for (int col = 0; col < width; col++)
                            {
                                int sourceIndex = ((y + row) * sourceStride) + ((x + col) * 4);
                                int targetIndex = (row * targetStride) + (col * 4);

                                // Копируем каждый компонент цвета (BGRA)
                                targetBuffer[targetIndex] = sourceBuffer[sourceIndex]; // Blue
                                targetBuffer[targetIndex + 1] = sourceBuffer[sourceIndex + 1]; // Green
                                targetBuffer[targetIndex + 2] = sourceBuffer[sourceIndex + 2]; // Red
                                targetBuffer[targetIndex + 3] = sourceBuffer[sourceIndex + 3]; // Alpha
                            }
                        }
                    }
                }

                // Обновляем изображение в редакторе
                ImageChu.Source = croppedBitmap;

                // Обновляем размер канваса и позицию изображения
                ImageCanvas.Width = width;
                ImageCanvas.Height = height;
                Canvas.SetLeft(ImageChu, 0);
                Canvas.SetTop(ImageChu, 0);
            }
        }

        #endregion

        #region Поворот
        public void RotateImage(double angle)
        {
            if (ImageChu.Source is WriteableBitmap sourceBitmap)
            {
                var newBitmap = new WriteableBitmap(
                    new PixelSize(sourceBitmap.PixelSize.Height, sourceBitmap.PixelSize.Width),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888);

                using (var framebuffer = newBitmap.Lock())
                using (var sourceFrameBuffer = sourceBitmap.Lock())
                {
                    unsafe
                    {
                        var sourceBuffer = (byte*)sourceFrameBuffer.Address;
                        var targetBuffer = (byte*)framebuffer.Address;

                        int sourceWidth = sourceBitmap.PixelSize.Width;
                        int sourceHeight = sourceBitmap.PixelSize.Height;
                        int sourceStride = sourceWidth * 4;
                        int targetStride = sourceHeight * 4;

                        for (int y = 0; y < sourceHeight; y++)
                        {
                            for (int x = 0; x < sourceWidth; x++)
                            {
                                int sourceIndex = (y * sourceStride) + (x * 4);
                                int targetIndex = ((x * targetStride) + ((sourceHeight - y - 1) * 4));

                                targetBuffer[targetIndex] = sourceBuffer[sourceIndex]; // Blue
                                targetBuffer[targetIndex + 1] = sourceBuffer[sourceIndex + 1]; // Green
                                targetBuffer[targetIndex + 2] = sourceBuffer[sourceIndex + 2]; // Red
                                targetBuffer[targetIndex + 3] = sourceBuffer[sourceIndex + 3]; // Alpha
                            }
                        }
                    }
                }

                ImageChu.Source = newBitmap;
                ImageCanvas.Width = newBitmap.PixelSize.Width;
                ImageCanvas.Height = newBitmap.PixelSize.Height;
                CenterImage();
            }
        }

        public OpenFileData GetCurrentImageData()
        {
            if (ImageChu.Source is WriteableBitmap writeableBitmap)
            {
                int width = writeableBitmap.PixelSize.Width;
                int height = writeableBitmap.PixelSize.Height;
                int totalPixels = width * height;

                byte[] red = new byte[totalPixels];
                byte[] green = new byte[totalPixels];
                byte[] blue = new byte[totalPixels];

                // Читаем пиксели из WriteableBitmap
                using (var fb = writeableBitmap.Lock())
                {
                    IntPtr buffer = fb.Address;
                    int stride = fb.RowBytes;

                    unsafe
                    {
                        byte* ptr = (byte*)buffer;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int index = y * width + x;
                                int pixelIndex = y * stride + x * 4;

                                blue[index] = ptr[pixelIndex];      // B
                                green[index] = ptr[pixelIndex + 1]; // G
                                red[index] = ptr[pixelIndex + 2];   // R
                            }
                        }
                    }
                }

                return new OpenFileData
                {
                    Name = FileName,
                    Red = red,
                    Green = green,
                    Blue = blue,
                    Width = width,
                    Height = height
                };
            }

            return null;
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

                    for (int y = 0; y < chunkHeight; y++)
                    {
                        for (int x = 0; x < chunkWidth; x++)
                        {
                            int index = (chunkY + y) * chunkWidth + chunkX + x;

                            if (index >= 0 && index < chunkWidth * chunkHeight)
                            {
                                var currentFileData = file;
                                byte r = currentFileData.Red[index];
                                byte g = currentFileData.Green[index];
                                byte b = currentFileData.Blue[index];
                                byte a = 255;

                                buffer[y * chunkWidth + x] = (uint)(a << 24 | b << 16 | g << 8 | r);
                            }
                            else
                            {
                                buffer[y * chunkWidth + x] = 0xFF000000;
                            }
                        }
                    }
                }
            }

            imageControl.Source = bitmap;
            UpdateScale(); // Устанавливаем начальный масштаб
        }
    }
}
