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

        private void DisplayImage(bool applySquares = true, bool applyCircles = true, bool applyLines = true,
                         LineInfo previewLine = null) // Добавляем параметр для предпросмотра линии
        {
            // Создаем копии массивов для отображения
            ushort[] displayRed = new ushort[currentRed16.Length];
            ushort[] displayGreen = new ushort[currentGreen16.Length];
            ushort[] displayBlue = new ushort[currentBlue16.Length];

            Array.Copy(currentRed16, displayRed, currentRed16.Length);
            Array.Copy(currentGreen16, displayGreen, currentGreen16.Length);
            Array.Copy(currentBlue16, displayBlue, currentBlue16.Length);

            // Применяем фигуры
            if (applySquares) DrawSquaresOn16BitImage(displayRed, displayGreen, displayBlue, currentWidth, currentHeight, squares);
            if (applyCircles) DrawCirclesOn16BitImage(displayRed, displayGreen, displayBlue, currentWidth, currentHeight, circles);
            if (applyLines) DrawLinesOn16BitImage(displayRed, displayGreen, displayBlue, currentWidth, currentHeight, lines, previewLine); // Передаем previewLine

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
            else if (isDrawingLine)
            {
                var position = e.GetCurrentPoint(ImageCanvas).Position;
                var imagePosition = new Point(
                    (position.X - Canvas.GetLeft(ImageChu)) / currentScale,
                    (position.Y - Canvas.GetTop(ImageChu)) / currentScale);

                currentLine = new LineInfo
                {
                    StartPoint = imagePosition,
                    EndPoint = imagePosition
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

                UpdateCirclePreview();
            }
            else if (isDrawingLine && currentLine != null && 
                e.GetCurrentPoint(ImageCanvas).Properties.IsLeftButtonPressed)
            {
                var currentPoint = e.GetCurrentPoint(ImageCanvas).Position;
                var imageCurrentPoint = new Point(
                    (currentPoint.X - Canvas.GetLeft(ImageChu)) / currentScale,
                    (currentPoint.Y - Canvas.GetTop(ImageChu)) / currentScale);

                currentLine.EndPoint = imageCurrentPoint;
                DisplayImage(applyLines: true, previewLine: currentLine); // Передаем текущую линию для предпросмотра
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
                previewBitmap?.Dispose();
                previewBitmap = null;
            }
            else if (isDrawingLine && currentLine != null)
            {
                if (currentLine.StartPoint != currentLine.EndPoint)
                {
                    lines.Add(currentLine);
                    DisplayImage();
                }
                currentLine = null;
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
            UpdateCirclePositions();
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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                isShiftPressed = true;
                UpdateLineWithConstraints();
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                isShiftPressed = false;
                UpdateLineWithConstraints();
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

            var newCircles = new List<CircleInfo>();
            foreach (var circle in circles)
            {
                double newCenterX = circle.Center.X - x;
                double newCenterY = circle.Center.Y - y;

                if (newCenterX - circle.Radius >= 0 &&
                    newCenterY - circle.Radius >= 0 &&
                    newCenterX + circle.Radius <= width &&
                    newCenterY + circle.Radius <= height)
                {
                    newCircles.Add(new CircleInfo
                    {
                        Center = new Point(newCenterX, newCenterY),
                        Radius = circle.Radius,
                        StrokeColor = circle.StrokeColor,
                        StrokeThickness = circle.StrokeThickness
                    });
                }
            }
            circles = newCircles;

            var newLines = new List<LineInfo>();
            foreach (var line in lines)
            {
                double newStartX = line.StartPoint.X - x;
                double newStartY = line.StartPoint.Y - y;
                double newEndX = line.EndPoint.X - x;
                double newEndY = line.EndPoint.Y - y;

                if (newStartX >= 0 && newStartY >= 0 && newStartX < width && newStartY < height &&
                    newEndX >= 0 && newEndY >= 0 && newEndX < width && newEndY < height)
                {
                    newLines.Add(new LineInfo
                    {
                        StartPoint = new Point(newStartX, newStartY),
                        EndPoint = new Point(newEndX, newEndY),
                        StrokeColor = line.StrokeColor,
                        StrokeThickness = line.StrokeThickness
                    });
                }
            }
            lines = newLines;

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
                double newX = square.Position.Y;
                double newY = oldWidth - square.Position.X - square.Size;

                newSquares.Add(new SquareInfo
                {
                    Position = new Point(newX, newY),
                    Size = square.Size,
                    StrokeColor = square.StrokeColor,
                    StrokeThickness = square.StrokeThickness
                });
            }
            squares = newSquares;

            var newCircles = new List<CircleInfo>();
            foreach (var circle in circles)
            {
                double newCenterX = circle.Center.Y;
                double newCenterY = oldWidth - circle.Center.X;

                newCircles.Add(new CircleInfo
                {
                    Center = new Point(newCenterX, newCenterY),
                    Radius = circle.Radius,
                    StrokeColor = circle.StrokeColor,
                    StrokeThickness = circle.StrokeThickness
                });
            }
            circles = newCircles;

            var newLines = new List<LineInfo>();
            foreach (var line in lines)
            {
                double newStartX = line.StartPoint.Y;
                double newStartY = oldWidth - line.StartPoint.X;
                double newEndX = line.EndPoint.Y;
                double newEndY = oldWidth - line.EndPoint.X;

                newLines.Add(new LineInfo
                {
                    StartPoint = new Point(newStartX, newStartY),
                    EndPoint = new Point(newEndX, newEndY),
                    StrokeColor = line.StrokeColor,
                    StrokeThickness = line.StrokeThickness
                });
            }
            lines = newLines;


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

                // Рисуем границы квадрата с учетом поворота
                for (int t = 0; t < thickness; t++)
                {
                    // Верхняя и нижняя границы
                    for (int x = x1 - t; x < x1 + size + t; x++)
                    {
                        if (x >= 0 && x < width)
                        {
                            // Верхняя граница
                            int yTop = y1 - t;
                            if (yTop >= 0 && yTop < height)
                            {
                                int index = yTop * width + x;
                                red[index] = redValue;
                                green[index] = greenValue;
                                blue[index] = blueValue;
                            }

                            // Нижняя граница
                            int yBottom = y1 + size - 1 + t;
                            if (yBottom >= 0 && yBottom < height)
                            {
                                int index = yBottom * width + x;
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
                            int xLeft = x1 - t;
                            if (xLeft >= 0 && xLeft < width)
                            {
                                int index = y * width + xLeft;
                                red[index] = redValue;
                                green[index] = greenValue;
                                blue[index] = blueValue;
                            }

                            // Правая граница
                            int xRight = x1 + size - 1 + t;
                            if (xRight >= 0 && xRight < width)
                            {
                                int index = y * width + xRight;
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
            public double StrokeThickness { get; set; } = 1;
        }

        private bool isDrawingCircle = false;
        private CircleInfo currentCircle;
        private List<CircleInfo> circles = new List<CircleInfo>();
        private WriteableBitmap previewBitmap;

        public void EnableCircleDrawing()
        {
            isDrawingCircle = true;
            isDrawingSquare = false;
            isCropping = false;
            CropSelection.IsVisible = false;

            previewBitmap = new WriteableBitmap(
                new PixelSize(currentWidth, currentHeight),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888);
        }

        private void DrawCirclesOn16BitImage(ushort[] red, ushort[] green, ushort[] blue,
                                           int width, int height, List<CircleInfo> circles,
                                           CircleInfo previewCircle = null)
        {
            // Создаем временные массивы для предпросмотра
            ushort[] tempRed = new ushort[red.Length];
            ushort[] tempGreen = new ushort[green.Length];
            ushort[] tempBlue = new ushort[blue.Length];

            Array.Copy(red, tempRed, red.Length);
            Array.Copy(green, tempGreen, green.Length);
            Array.Copy(blue, tempBlue, blue.Length);

            // Рисуем все сохраненные круги
            foreach (var circle in circles)
            {
                DrawSingleCircle(tempRed, tempGreen, tempBlue, width, height, circle);
            }

            // Рисуем круг для предпросмотра (если есть)
            if (previewCircle != null && previewCircle.Radius > 0)
            {
                DrawSingleCircle(tempRed, tempGreen, tempBlue, width, height, previewCircle);
            }

            // Копируем результаты обратно
            Array.Copy(tempRed, red, tempRed.Length);
            Array.Copy(tempGreen, green, tempGreen.Length);
            Array.Copy(tempBlue, blue, tempBlue.Length);
        }

        private void DrawSingleCircle(ushort[] red, ushort[] green, ushort[] blue,
                    int width, int height, CircleInfo circle)
        {
            int centerX = (int)circle.Center.X;
            int centerY = (int)circle.Center.Y;
            int radius = (int)circle.Radius;
            int thickness = (int)circle.StrokeThickness;

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

            for (int r = radius - thickness / 2; r <= radius + thickness / 2; r++)
            {
                if (r <= 0) continue;

                int x = 0;
                int y = r;
                int d = 3 - 2 * r;

                while (y >= x)
                {
                    DrawPixel(centerX + x, centerY + y);
                    DrawPixel(centerX - x, centerY + y);
                    DrawPixel(centerX + x, centerY - y);
                    DrawPixel(centerX - x, centerY - y);
                    DrawPixel(centerX + y, centerY + x);
                    DrawPixel(centerX - y, centerY + x);
                    DrawPixel(centerX + y, centerY - x);
                    DrawPixel(centerX - y, centerY - x);

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

            void DrawPixel(int x, int y)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    int index = y * width + x;
                    red[index] = redValue;
                    green[index] = greenValue;
                    blue[index] = blueValue;
                }
            }
        }

        private void UpdateCirclePreview()
        {
            if (currentCircle == null || currentCircle.Radius <= 0) return;

            // Создаем временные массивы для предпросмотра
            ushort[] displayRed = new ushort[currentRed16.Length];
            ushort[] displayGreen = new ushort[currentGreen16.Length];
            ushort[] displayBlue = new ushort[currentBlue16.Length];

            Array.Copy(currentRed16, displayRed, currentRed16.Length);
            Array.Copy(currentGreen16, displayGreen, currentGreen16.Length);
            Array.Copy(currentBlue16, displayBlue, currentBlue16.Length);

            // Рисуем все сохраненные круги и квадраты
            DrawSquaresOn16BitImage(displayRed, displayGreen, displayBlue,
                                  currentWidth, currentHeight, squares);

            DrawCirclesOn16BitImage(displayRed, displayGreen, displayBlue,
                                  currentWidth, currentHeight, circles, currentCircle);

            // Отображаем предпросмотр
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
        }

        private void UpdateCirclePositions()
        {
            foreach (var circle in circles)
            {
                circle.Center = new Point(circle.Center.X * currentScale, circle.Center.Y * currentScale);

                circle.Radius *= currentScale;
            }
        }

        #endregion

        #region Линия
        public class LineInfo
        {
            public Point StartPoint { get; set; }
            public Point EndPoint { get; set; }
            public Color StrokeColor { get; set; } = Colors.Green; 
            public double StrokeThickness { get; set; } = 2;
        }

        private bool isDrawingLine = false;
        private LineInfo currentLine;
        private List<LineInfo> lines = new List<LineInfo>();
        private bool isShiftPressed = false;

        public void EnableLineDrawing()
        {
            isDrawingLine = true;
            isDrawingSquare = false;
            isDrawingCircle = false;
            isCropping = false;
            CropSelection.IsVisible = false;
        }
        private void DrawLinesOn16BitImage(ushort[] red, ushort[] green, ushort[] blue,
                         int width, int height, List<LineInfo> lines,
                         LineInfo previewLine = null)
        {
            // Создаем временные массивы для предпросмотра
            ushort[] tempRed = new ushort[red.Length];
            ushort[] tempGreen = new ushort[green.Length];
            ushort[] tempBlue = new ushort[blue.Length];

            Array.Copy(red, tempRed, red.Length);
            Array.Copy(green, tempGreen, green.Length);
            Array.Copy(blue, tempBlue, blue.Length);

            // Рисуем все сохраненные линии
            foreach (var line in lines)
            {
                DrawSingleLine(tempRed, tempGreen, tempBlue, width, height, line);
            }

            // Рисуем линию для предпросмотра (если есть)
            if (previewLine != null && previewLine.StartPoint != previewLine.EndPoint)
            {
                DrawSingleLine(tempRed, tempGreen, tempBlue, width, height, previewLine);
            }

            // Копируем результаты обратно
            Array.Copy(tempRed, red, tempRed.Length);
            Array.Copy(tempGreen, green, tempGreen.Length);
            Array.Copy(tempBlue, blue, tempBlue.Length);
        }
        private void DrawSingleLine(ushort[] red, ushort[] green, ushort[] blue,
                                  int width, int height, LineInfo line)
        {
            int x0 = (int)line.StartPoint.X;
            int y0 = (int)line.StartPoint.Y;
            int x1 = (int)line.EndPoint.X;
            int y1 = (int)line.EndPoint.Y;
            int thickness = (int)line.StrokeThickness;

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

            // Алгоритм Брезенхема для рисования линии
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // Рисуем пиксель с учетом толщины линии
                for (int t = -thickness / 2; t <= thickness / 2; t++)
                {
                    for (int s = -thickness / 2; s <= thickness / 2; s++)
                    {
                        int px = x0 + t;
                        int py = y0 + s;
                        if (px >= 0 && px < width && py >= 0 && py < height)
                        {
                            int index = py * width + px;
                            red[index] = redValue;
                            green[index] = greenValue;
                            blue[index] = blueValue;
                        }
                    }
                }

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }
        private void UpdateLineWithConstraints()
        {
            if (currentLine == null) return;

            if (isShiftPressed)
            {
                // Делаем линию горизонтальной, вертикальной или под 45 градусов
                double dx = currentLine.EndPoint.X - currentLine.StartPoint.X;
                double dy = currentLine.EndPoint.Y - currentLine.StartPoint.Y;

                if (Math.Abs(dx) > Math.Abs(dy))
                {
                    // Горизонтальная линия
                    currentLine.EndPoint = new Point(currentLine.EndPoint.X, currentLine.StartPoint.Y);
                }
                else
                {
                    // Вертикальная линия
                    currentLine.EndPoint = new Point(currentLine.StartPoint.X, currentLine.EndPoint.Y);
                }

                // Дополнительно можно добавить проверку на угол 45 градусов
                double angle = Math.Atan2(dy, dx) * (180 / Math.PI);
                if (Math.Abs(angle % 45) < 5) // Если близко к 45 градусам
                {
                    double length = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    currentLine.EndPoint = new Point(
                        currentLine.StartPoint.X + length * Math.Sign(dx),
                        currentLine.StartPoint.Y + length * Math.Sign(dy));
                }
            }
        }
        #endregion

        #region Blue Channel
        private bool isBlueChannelPrimary = false;
        public void SwapRedBlueChannels()
        {
            ushort[] tempRed = currentRed16.ToArray();
            ushort[] tempBlue = currentBlue16.ToArray();

            currentRed16 = tempBlue;
            currentBlue16 = tempRed;


            DisplayImage();
        }
        #endregion

        #region Green Channel 
        public void SwapGreenRedChannels()
        {
            ushort[] tempGreen = currentGreen16.ToArray();
            ushort[] tempRed = currentRed16.ToArray();

            currentGreen16 = tempRed;
            currentRed16 = tempGreen;

            DisplayImage();
        }

        public void SwapGreenBlueChannels()
        {
            ushort[] tempGreen = currentGreen16.ToArray();
            ushort[] tempBlue = currentBlue16.ToArray();

            currentGreen16 = tempBlue;
            currentBlue16 = tempGreen;

            DisplayImage();
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
            ushort[] saveRed = currentRed16.ToArray();
            ushort[] saveGreen = currentGreen16.ToArray();
            ushort[] saveBlue = currentBlue16.ToArray();

            // Применяем все фигуры к данным перед сохранением
            DrawSquaresOn16BitImage(saveRed, saveGreen, saveBlue, currentWidth, currentHeight, squares);
            DrawCirclesOn16BitImage(saveRed, saveGreen, saveBlue, currentWidth, currentHeight, circles);
            DrawLinesOn16BitImage(saveRed, saveGreen, saveBlue, currentWidth, currentHeight, lines);

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