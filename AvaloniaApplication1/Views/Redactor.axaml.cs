using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Linq;
using Avalonia.Controls.Shapes;
using System.Collections.Generic;
using System.Diagnostics;
using static AvaloniaApplication1.Views.MainView;

namespace AvaloniaApplication1
{
    public partial class Redactor : UserControl
    {
        private double currentScale = 1.0;
        private double minScale = 0.1;
        private double maxScale = 5.0;
        private bool isCropping = false;
        private bool isDrawingSquare = false;
        private Point startPoint;
        private SquareInfo currentSquare;


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
        private List<SquareInfo> squares = new List<SquareInfo>();


        public void InitializeImage(OpenFileData fileData)
        {
            currentFileData = fileData;
            FileName = fileData.Name;
            currentRed16 = fileData.Red16.ToArray();
            currentGreen16 = fileData.Green16.ToArray();
            currentBlue16 = fileData.Blue16.ToArray();
            currentWidth = fileData.Width;
            currentHeight = fileData.Height;
            squares.Clear();
            DisplayImage();
        }

        private void DisplayImage(bool applySquares = true, bool applyCircles = true)
        {
            // Создаем копии массивов для отображения
            ushort[] displayRed = new ushort[currentRed16.Length];
            ushort[] displayGreen = new ushort[currentGreen16.Length];
            ushort[] displayBlue = new ushort[currentBlue16.Length];

            Array.Copy(currentRed16, displayRed, currentRed16.Length);
            Array.Copy(currentGreen16, displayGreen, currentGreen16.Length);
            Array.Copy(currentBlue16, displayBlue, currentBlue16.Length);

            // Применяем квадраты только если нужно
            if (applySquares)
            {
                DrawSquaresOn16BitImage(displayRed, displayGreen, displayBlue,
                                      currentWidth, currentHeight, squares);
            }

            if (applyCircles)
            {
                DrawCirclesOn16BitImage(displayRed, displayGreen, displayBlue,
                                      currentWidth, currentHeight, circles);
            }

            var displayData = new OpenFileData
            {
                Name = FileName,
                Width = currentWidth,
                Height = currentHeight,
                Red16 = displayRed,
                Green16 = displayGreen,
                Blue16 = displayBlue
            };

            PrintImageChunk(displayData, ImageChu, 0, 0, currentWidth, currentHeight);
            ImageCanvas.Width = currentWidth * currentScale;
            ImageCanvas.Height = currentHeight * currentScale;
            CenterImage();
        }

        private void InitializeEventHandlers()
        {
            ImageChu.PointerWheelChanged += ImageChu_PointerWheelChanged;
            ImageCanvas.PointerPressed += ImageCanvas_PointerPressed;
            ImageCanvas.PointerMoved += ImageCanvas_PointerMoved;
            ImageCanvas.PointerReleased += ImageCanvas_PointerReleased;
        }

        #region События
        private void ImageCanvas_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (isCropping)
            {
                startPoint = e.GetCurrentPoint(ImageCanvas).Position;
                CropSelection.Width = 0;
                CropSelection.Height = 0;
                CropSelection.Margin = new Thickness(startPoint.X, startPoint.Y, 0, 0);
            }
            else if (isDrawingSquare)
            {
                var position = e.GetCurrentPoint(ImageCanvas).Position;
                var imagePosition = new Point(
                    (position.X - Canvas.GetLeft(ImageChu)) / currentScale,
                    (position.Y - Canvas.GetTop(ImageChu)) / currentScale);

                currentSquare = new SquareInfo
                {
                    Position = imagePosition,
                    Size = 0
                };
            }
            else if (isDrawingCircle)
            {
                var position = e.GetCurrentPoint(ImageCanvas).Position;
                var imagePosition = new Point(
                    (position.X - Canvas.GetLeft(ImageChu)) / currentScale,
                    (position.Y - Canvas.GetTop(ImageChu)) / currentScale);

                currentCircle = new CircleInfo
                {
                    Center = imagePosition,
                    Radius = 0
                };
            }
        }

        private void ImageCanvas_PointerMoved(object sender, PointerEventArgs e)
        {
            if (isCropping && e.GetCurrentPoint(ImageCanvas).Properties.IsLeftButtonPressed)
            {
                var currentPoint = e.GetCurrentPoint(ImageCanvas).Position;
                double x = Math.Min(startPoint.X, currentPoint.X);
                double y = Math.Min(startPoint.Y, currentPoint.Y);
                double width = Math.Abs(startPoint.X - currentPoint.X);
                double height = Math.Abs(startPoint.Y - currentPoint.Y);

                CropSelection.Margin = new Thickness(x, y, 0, 0);
                CropSelection.Width = width;
                CropSelection.Height = height;
            }
            else if (isDrawingSquare && currentSquare != null &&
                    e.GetCurrentPoint(ImageCanvas).Properties.IsLeftButtonPressed)
            {
                var currentPoint = e.GetCurrentPoint(ImageCanvas).Position;
                var imageCurrentPoint = new Point(
                    (currentPoint.X - Canvas.GetLeft(ImageChu)) / currentScale,
                    (currentPoint.Y - Canvas.GetTop(ImageChu)) / currentScale);

                double width = imageCurrentPoint.X - currentSquare.Position.X;
                double height = imageCurrentPoint.Y - currentSquare.Position.Y;

                currentSquare.Size = Math.Max(Math.Abs(width), Math.Abs(height));
                DisplayImage(); // Обновляем отображение в реальном времени
            }
            else if (isDrawingCircle && currentCircle != null &&
            e.GetCurrentPoint(ImageCanvas).Properties.IsLeftButtonPressed)
            {
                var currentPoint = e.GetCurrentPoint(ImageCanvas).Position;
                var imageCurrentPoint = new Point(
                    (currentPoint.X - Canvas.GetLeft(ImageChu)) / currentScale,
                    (currentPoint.Y - Canvas.GetTop(ImageChu)) / currentScale);

                double dx = imageCurrentPoint.X - currentCircle.Center.X;
                double dy = imageCurrentPoint.Y - currentCircle.Center.Y;
                currentCircle.Radius = Math.Sqrt(dx * dx + dy * dy);

                DisplayImage(); // Обновляем отображение в реальном времени
            }
        }

        private void ImageCanvas_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (isCropping)
            {
                isCropping = false;
                CropImage();
                CropSelection.IsVisible = false;
            }
            else if (isDrawingSquare && currentSquare != null)
            {
                if (currentSquare.Size > 0)
                {
                    squares.Add(currentSquare);
                    DisplayImage();
                }
                currentSquare = null;
            }
            else if (isDrawingCircle && currentCircle != null)
            {
                if (currentCircle.Radius > 0)
                {
                    circles.Add(currentCircle);
                    DisplayImage();
                }
                currentCircle = null;
            }
        }

        private void ImageChu_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            double zoomFactor = 0.1;
            if (e.Delta.Y > 0)
                currentScale = Math.Min(maxScale, currentScale + zoomFactor);
            else
                currentScale = Math.Max(minScale, currentScale - zoomFactor);

            UpdateScale();
            e.Handled = true;
        }

        private void UpdateScale()
        {
            var scaleTransform = new ScaleTransform(currentScale, currentScale);
            ImageChu.RenderTransform = scaleTransform;
            ImageCanvas.Width = currentWidth * currentScale;
            ImageCanvas.Height = currentHeight * currentScale;
            CenterImage();
        }

        private void CenterImage()
        {
            if (ImageChu.Source != null)
            {
                var imageWidth = ImageChu.Source.Size.Width * currentScale;
                var imageHeight = ImageChu.Source.Size.Height * currentScale;

                Canvas.SetLeft(ImageChu, (ImageCanvas.Bounds.Width - imageWidth) / 2);
                Canvas.SetTop(ImageChu, (ImageCanvas.Bounds.Height - imageHeight) / 2);
            }
        }

        #endregion

        #region Обрезка
        public void EnableCropping()
        {
            isCropping = true;
            // Сбрасываем параметры перед началом новой обрезки
            CropSelection.Width = 0;
            CropSelection.Height = 0;
            CropSelection.Margin = new Thickness(0);
            CropSelection.IsVisible = true;
        }

        private void CropImage()
        {
            int x = (int)(CropSelection.Margin.Left / currentScale);
            int y = (int)(CropSelection.Margin.Top / currentScale);
            int width = (int)(CropSelection.Width / currentScale);
            int height = (int)(CropSelection.Height / currentScale);

            if (width <= 0 || height <= 0 || x + width > currentWidth || y + height > currentHeight)
                return;

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

            currentRed16 = red16;
            currentGreen16 = green16;
            currentBlue16 = blue16;
            currentWidth = width;
            currentHeight = height;

            var newSquares = new List<SquareInfo>();
            foreach (var square in squares)
            {
                // Пересчитываем координаты квадрата относительно новой области
                double newX = square.Position.X - x;
                double newY = square.Position.Y - y;

                if (newX >= 0 && newY >= 0 &&
                    newX + square.Size <= width &&
                    newY + square.Size <= height)
                {
                    newSquares.Add(new SquareInfo
                    {
                        Position = new Point(newX, newY),
                        Size = square.Size,
                        StrokeColor = square.StrokeColor,
                        StrokeThickness = square.StrokeThickness
                    });
                }
            }

            squares = newSquares;
            DisplayImage();
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

            currentRed16 = red16;
            currentGreen16 = green16;
            currentBlue16 = blue16;
            currentWidth = newWidth;
            currentHeight = newHeight;

            var newSquares = new List<SquareInfo>();
            foreach (var square in squares)
            {
                // Поворачиваем квадрат на 90 градусов по часовой стрелке
                double newX = currentHeight - square.Position.Y - square.Size;
                double newY = square.Position.X;

                newSquares.Add(new SquareInfo
                {
                    Position = new Point(newX, newY),
                    Size = square.Size,
                    StrokeColor = square.StrokeColor,
                    StrokeThickness = square.StrokeThickness
                });
            }

            squares = newSquares;
            DisplayImage();


        }
        #endregion

        #region Квадрат
        public class SquareInfo
        {
            public Point Position { get; set; }
            public double Size { get; set; }
            public Color StrokeColor { get; set; } = Colors.Red;
            public double StrokeThickness { get; set; } = 2;
        }

        private void DrawSquaresOn16BitImage(ushort[] red, ushort[] green, ushort[] blue,
                                   int width, int height, List<SquareInfo> squares)
        {
            foreach (var square in squares)
            {
                int x1 = (int)square.Position.X;
                int y1 = (int)square.Position.Y;
                int size = (int)square.Size;
                int thickness = (int)square.StrokeThickness;

                // Получаем максимальные значения из текущего изображения для каждого канала
                ushort maxR = currentRed16.Max();
                ushort maxG = currentGreen16.Max();
                ushort maxB = currentBlue16.Max();

                // Устанавливаем значения для квадратов (максимальные значения для каждого канала)
                ushort redValue = maxR;
                ushort greenValue = maxG;
                ushort blueValue = maxB;

                // Если все каналы нулевые (чтобы избежать невидимых квадратов на черном фоне)
                if (maxR == 0 && maxG == 0 && maxB == 0)
                {
                    redValue = 65535;
                    greenValue = 65535;
                    blueValue = 65535;
                }

                // Рисуем границы квадрата
                for (int t = 0; t < thickness; t++)
                {
                    // Верхняя и нижняя границы
                    for (int x = x1 - t; x < x1 + size + t; x++)
                    {
                        if (x >= 0 && x < width)
                        {
                            // Верхняя граница
                            if (y1 - t >= 0 && y1 - t < height)
                            {
                                int index = (y1 - t) * width + x;
                                red[index] = redValue;
                                green[index] = greenValue;
                                blue[index] = blueValue;
                            }

                            // Нижняя граница
                            if (y1 + size - 1 + t >= 0 && y1 + size - 1 + t < height)
                            {
                                int index = (y1 + size - 1 + t) * width + x;
                                red[index] = redValue;
                                green[index] = greenValue;
                                blue[index] = blueValue;
                            }
                        }
                    }

                    // Левая и правая границы
                    for (int y = y1 - t; y < y1 + size + t; y++)
                    {
                        if (y >= 0 && y < height)
                        {
                            // Левая граница
                            if (x1 - t >= 0 && x1 - t < width)
                            {
                                int index = y * width + x1 - t;
                                red[index] = redValue;
                                green[index] = greenValue;
                                blue[index] = blueValue;
                            }

                            // Правая граница
                            if (x1 + size - 1 + t >= 0 && x1 + size - 1 + t < width)
                            {
                                int index = y * width + x1 + size - 1 + t;
                                red[index] = redValue;
                                green[index] = greenValue;
                                blue[index] = blueValue;
                            }
                        }
                    }
                }
            }
        }

        public void EnableSquareDrawing()
        {
            isDrawingSquare = true;
        }
        #endregion

        #region Круг
        public class CircleInfo
        {
            public Point Center { get; set; }
            public double Radius { get; set; }
            public Color StrokeColor { get; set; } = Colors.Blue;
            public double StrokeThickness { get; set; } = 2;
        }

        private bool isDrawingCircle = false;
        private CircleInfo currentCircle;
        private List<CircleInfo> circles = new List<CircleInfo>();

        public void EnableCircleDrawing()
        {
            isDrawingCircle = true;
            isDrawingSquare = false;
            isCropping = false;
            CropSelection.IsVisible = false;
        }

        private void DrawCirclesOn16BitImage(ushort[] red, ushort[] green, ushort[] blue,
                                       int width, int height, List<CircleInfo> circles)
        {
            foreach (var circle in circles)
            {
                int centerX = (int)circle.Center.X;
                int centerY = (int)circle.Center.Y;
                int radius = (int)circle.Radius;
                int thickness = (int)circle.StrokeThickness;

                // Получаем максимальные значения из текущего изображения
                ushort maxR = currentRed16.Max();
                ushort maxG = currentGreen16.Max();
                ushort maxB = currentBlue16.Max();

                ushort redValue = maxR;
                ushort greenValue = maxG;
                ushort blueValue = maxB;

                if (maxR == 0 && maxG == 0 && maxB == 0)
                {
                    redValue = 65535;
                    greenValue = 65535;
                    blueValue = 65535;
                }

                // Алгоритм Брезенхэма для рисования окружности
                void DrawCirclePixel(int x, int y)
                {
                    for (int t = 0; t < thickness; t++)
                    {
                        int currentRadius = radius + t;
                        int xc = centerX + x;
                        int yc = centerY + y;

                        if (xc >= 0 && xc < width && yc >= 0 && yc < height)
                        {
                            int index = yc * width + xc;
                            red[index] = redValue;
                            green[index] = greenValue;
                            blue[index] = blueValue;
                        }
                    }
                }

                int x = 0;
                int y = radius;
                int d = 3 - 2 * radius;

                while (y >= x)
                {
                    // Отрисовка 8 симметричных точек
                    DrawCirclePixel(x, y);
                    DrawCirclePixel(-x, y);
                    DrawCirclePixel(x, -y);
                    DrawCirclePixel(-x, -y);
                    DrawCirclePixel(y, x);
                    DrawCirclePixel(-y, x);
                    DrawCirclePixel(y, -x);
                    DrawCirclePixel(-y, -x);

                    if (d < 0)
                    {
                        d = d + 4 * x + 6;
                    }
                    else
                    {
                        d = d + 4 * (x - y) + 10;
                        y--;
                    }
                    x++;
                }
            }
        }
        #endregion

        #region Файл 
        private void PrintImageChunk(OpenFileData file, Image imageControl, int chunkX, int chunkY, int chunkWidth, int chunkHeight)
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

                    // Автоматическое контрастирование для каждого канала
                    ushort minR = file.Red16.Min();
                    ushort maxR = file.Red16.Max();
                    double rangeR = Math.Max(1, maxR - minR);

                    ushort minG = file.Green16.Min();
                    ushort maxG = file.Green16.Max();
                    double rangeG = Math.Max(1, maxG - minG);

                    ushort minB = file.Blue16.Min();
                    ushort maxB = file.Blue16.Max();
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

                            // Нормализация с сохранением относительной яркости
                            byte r = (byte)((file.Red16[index] - minR) * 255 / rangeR);
                            byte g = (byte)((file.Green16[index] - minG) * 255 / rangeG);
                            byte b = (byte)((file.Blue16[index] - minB) * 255 / rangeB);

                            buffer[y * chunkWidth + x] = (uint)(255 << 24 | b << 16 | g << 8 | r);
                        }
                    }
                }
            }

            imageControl.Source = bitmap;
        }

        public OpenFileData GetCurrentImageData()
        {
            // Создаем копии массивов для сохранения
            ushort[] saveRed = new ushort[currentRed16.Length];
            ushort[] saveGreen = new ushort[currentGreen16.Length];
            ushort[] saveBlue = new ushort[currentBlue16.Length];

            Array.Copy(currentRed16, saveRed, currentRed16.Length);
            Array.Copy(currentGreen16, saveGreen, currentGreen16.Length);
            Array.Copy(currentBlue16, saveBlue, currentBlue16.Length);

            // Применяем квадраты к данным перед сохранением
            DrawSquaresOn16BitImage(saveRed, saveGreen, saveBlue,
                                  currentWidth, currentHeight, squares);

            DrawCirclesOn16BitImage(saveRed, saveGreen, saveBlue,
                         currentWidth, currentHeight, circles);

            return new OpenFileData
            {
                Name = FileName,
                Red16 = saveRed,
                Green16 = saveGreen,
                Blue16 = saveBlue,
                Width = currentWidth,
                Height = currentHeight
            };
        }


        #endregion

    }
}