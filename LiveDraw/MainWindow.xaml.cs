using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace AntFu7.LiveDraw
{
    

    public partial class MainWindow : Window
    {
        public static int EraseByPoint_Flag = 0;
        public enum erase_mode
        {
            NONE = 0,
            ERASER = 1,
            ERASERBYPOINT = 2            
        }
        private static Mutex mutex = new Mutex(true, "LiveDraw");
        private static readonly Duration Duration1 = (Duration)Application.Current.Resources["Duration1"];
        private static readonly Duration Duration2 = (Duration)Application.Current.Resources["Duration2"];
        private static readonly Duration Duration3 = (Duration)Application.Current.Resources["Duration3"];
        private static readonly Duration Duration4 = (Duration)Application.Current.Resources["Duration4"];
        private static readonly Duration Duration5 = (Duration)Application.Current.Resources["Duration5"];
        private static readonly Duration Duration7 = (Duration)Application.Current.Resources["Duration7"];
        private static readonly Duration Duration10 = (Duration)Application.Current.Resources["Duration10"];

        /*#region Mouse Throught

        private const int WsExTransparent = 0x20;
        private const int GwlExstyle = (-20);

        [DllImport("user32", EntryPoint = "SetWindowLong")]
        private static extern uint SetWindowLong(IntPtr hwnd, int nIndex, uint dwNewLong);

        [DllImport("user32", EntryPoint = "GetWindowLong")]
        private static extern uint GetWindowLong(IntPtr hwnd, int nIndex);

        private void SetThrought(bool t)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GwlExstyle);
            if (t)
                SetWindowLong(hwnd, GwlExstyle, extendedStyle | WsExTransparent);
            else
                SetWindowLong(hwnd, GwlExstyle, extendedStyle & ~(uint)WsExTransparent);
        }


        #endregion*/

        #region /---------Lifetime---------/

        public MainWindow()
        {
           
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                _history = new Stack<StrokesHistoryNode>();
                _redoHistory = new Stack<StrokesHistoryNode>();
                if (!Directory.Exists("Save"))
                    Directory.CreateDirectory("Save");

                InitializeComponent();
                SetColor(DefaultColorPicker);
                SetEnable(false);
                SetTopMost(true);
                SetDetailPanel(true);
                SetBrushSize(_brushSizes[_brushIndex]);
                DetailPanel.Opacity = 0;

                MainInkCanvas.Strokes.StrokesChanged += StrokesChanged;
                MainInkCanvas.MouseLeftButtonDown += StartLine;
                MainInkCanvas.MouseLeftButtonUp += EndLine;
                MainInkCanvas.MouseMove += MakeLine;
                MainInkCanvas.MouseWheel += BrushSize;
                //RightDocking();

            }
            else
            {
                Application.Current.Shutdown(0);
            }
        }

        private void Exit(object sender, EventArgs e)
        {
            if (IsUnsaved())
                QuickSave("ExitingAutoSave_");
            
            Application.Current.Shutdown(0);
        }

        #endregion


        #region /---------Judge--------/

        private bool _saved;

        private bool IsUnsaved()
        {
            return MainInkCanvas.Strokes.Count != 0 && !_saved;
        }

        private bool PromptToSave()
        {
            if (!IsUnsaved())
                return true;
            var r = MessageBox.Show("You have unsaved work, do you want to save it now?", "Unsaved data",
                MessageBoxButton.YesNoCancel);
            if (r == MessageBoxResult.Yes || r == MessageBoxResult.OK)
            {
                QuickSave();
                return true;
            }
            if (r == MessageBoxResult.No || r == MessageBoxResult.None)
                return true;
            return false;
        }

        #endregion


        #region /---------Setter---------/

        private ColorPicker _selectedColor;
        private bool _inkVisibility = true;
        private bool _displayDetailPanel;
        private bool _eraserMode;
        private bool _enable;
        private readonly int[] _brushSizes = { 2, 5, 8, 13, 20 };
        private int _brushIndex = 1;
        private bool _displayOrientation;

        private void SetDetailPanel(bool v)
        {
            if (v)
            {
                DetailTogglerRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(180, Duration5));
                //DefaultColorPicker.Size = ColorPickerButtonSize.Middle;
                DetailPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Duration4));
                //PaletteGrip.BeginAnimation(WidthProperty, new DoubleAnimation(130, Duration3));
                //MinimizeButton.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Duration3));
                //MinimizeButton.BeginAnimation(HeightProperty, new DoubleAnimation(0, 25, Duration3));
            }
            else
            {
                DetailTogglerRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, Duration5));
                //DefaultColorPicker.Size = ColorPickerButtonSize.Small;
                DetailPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, Duration4));
                //PaletteGrip.BeginAnimation(WidthProperty, new DoubleAnimation(80, Duration3));
                //MinimizeButton.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, Duration3));
                //MinimizeButton.BeginAnimation(HeightProperty, new DoubleAnimation(25, 0, Duration3));
            }
            _displayDetailPanel = v;
        }
        private void SetInkVisibility(bool v)
        {
            MainInkCanvas.BeginAnimation(OpacityProperty,
                v ? new DoubleAnimation(0, 1, Duration3) : new DoubleAnimation(1, 0, Duration3));
            HideButton.IsActived = !v;
            SetEnable(v);
            _inkVisibility = v;
        }
        private void SetEnable(bool b)
        {
            EnableButton.IsActived = !b;
            Background = Application.Current.Resources[b ? "FakeTransparent" : "TrueTransparent"] as Brush;
            _enable = b;
            MainInkCanvas.UseCustomCursor = false;

            //SetTopMost(false);
            if (_enable == true)
            {
                LineButton.IsActived = false;
                SetStaticInfo("LiveDraw");
                MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
            else
            {
                SetStaticInfo("Locked");
                MainInkCanvas.EditingMode = InkCanvasEditingMode.None; //No inking possible
            }
        }
        private void SetColor(ColorPicker b)
        {
            if (ReferenceEquals(_selectedColor, b)) return;
            var solidColorBrush = b.Background as SolidColorBrush;
            if (solidColorBrush == null) return;

            var ani = new ColorAnimation(solidColorBrush.Color, Duration3);

            MainInkCanvas.DefaultDrawingAttributes.Color = solidColorBrush.Color;
            brushPreview.Background.BeginAnimation(SolidColorBrush.ColorProperty, ani);
            b.IsActived = true;
            if (_selectedColor != null)
                _selectedColor.IsActived = false;
            _selectedColor = b;
        }
        private void SetBrushSize(double s)
        {
            if (MainInkCanvas.EditingMode == InkCanvasEditingMode.Ink)
            {
                MainInkCanvas.DefaultDrawingAttributes.Height = s;
                MainInkCanvas.DefaultDrawingAttributes.Width = s;
                brushPreview?.BeginAnimation(HeightProperty, new DoubleAnimation(s, Duration4));
                brushPreview?.BeginAnimation(WidthProperty, new DoubleAnimation(s, Duration4));
            }
            else if(MainInkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
            {
                MainInkCanvas.EditingMode = InkCanvasEditingMode.GestureOnly;
                MainInkCanvas.EraserShape = new EllipseStylusShape(s, s);
                MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
        }
        private void SetEraserMode(bool v)
        {
            EraserButton.IsActived = v;
            _eraserMode = v;
            MainInkCanvas.UseCustomCursor = false;

            if (_eraserMode)
            {
                MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                SetStaticInfo("Eraser Mode");
            }
            else
            {
                SetEnable(_enable);
            }
        }
        private void SetOrientation(bool v)
        {
            PaletteRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(v ? -90:0, Duration4));
            Palette.BeginAnimation(MinWidthProperty, new DoubleAnimation(v? 90:0, Duration7));
            //PaletteGrip.BeginAnimation(WidthProperty, new DoubleAnimation((double)Application.Current.Resources[v ? "VerticalModeGrip" : "HorizontalModeGrip"], Duration3));
            //BasicButtonPanel.BeginAnimation(WidthProperty, new DoubleAnimation((double)Application.Current.Resources[v ? "VerticalModeFlowPanel" : "HorizontalModeFlowPanel"], Duration3));
            //PaletteFlowPanel.BeginAnimation(WidthProperty, new DoubleAnimation((double)Application.Current.Resources[v ? "VerticalModeFlowPanel" : "HorizontalModeFlowPanel"], Duration3));
            //ColorPickersPanel.BeginAnimation(WidthProperty, new DoubleAnimation((double)Application.Current.Resources[v ? "VerticalModeColorPickersPanel" : "HorizontalModeColorPickersPanel"], Duration3));
            _displayOrientation = v;
        }
        private void SetTopMost(bool v)
        {
            PinButton.IsActived = v;
            Topmost = v;
        }
        #endregion


        #region /---------IO---------/
        private StrokeCollection _preLoadStrokes = null;
        private void QuickSave(string filename = "QuickSave_")
        {
            Save(new FileStream("Save\\" + filename + GenerateFileName(), FileMode.OpenOrCreate));
        }
        private void Save(Stream fs)
        {
            try
            {
                if (fs == Stream.Null) return;
                MainInkCanvas.Strokes.Save(fs);
                _saved = true;
                Display("Ink saved");
                fs.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Display("Fail to save");
            }
        }
        private StrokeCollection Load(Stream fs)
        {
            try
            {
                return new StrokeCollection(fs);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Display("Fail to load");
            }
            return new StrokeCollection();
        }
        private void AnimatedReload(StrokeCollection sc)
        {
            _preLoadStrokes = sc;
            var ani = new DoubleAnimation(0, Duration3);
            ani.Completed += LoadAniCompleted;
            MainInkCanvas.BeginAnimation(OpacityProperty, ani);
        }
        private void LoadAniCompleted(object sender, EventArgs e)
        {
            if (_preLoadStrokes == null) return;
            MainInkCanvas.Strokes = _preLoadStrokes;
            Display("Ink loaded");
            _saved = true;
            ClearHistory();
            MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Duration3));
        }

        private static string[] GetSavePathList()
        {
            return Directory.GetFiles("Save", "*.fdw");
        }
        private static string GetFileNameFromPath(string path)
        {
            return Path.GetFileName(path);
        }
        #endregion


        #region /---------Generator---------/
        private static string GenerateFileName(string fileExt = ".fdw")
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss") + fileExt;
        }
        #endregion


        #region /---------Helper---------/
        private string _staticInfo = "";
        private bool _displayingInfo;

        private async void Display(string info)
        {
            InfoBox.Text = info;
            _displayingInfo = true;
            await InfoDisplayTimeUp(new Progress<string>(box => InfoBox.Text = box));
        }
        private Task InfoDisplayTimeUp(IProgress<string> box)
        {
            return Task.Run(() =>
            {
                Task.Delay(2000).Wait();
                box.Report(_staticInfo);
                _displayingInfo = false;
            });
        }
        private void SetStaticInfo(string info)
        {
            _staticInfo = info;
            if (!_displayingInfo)
                InfoBox.Text = _staticInfo;
        }

        private static Stream SaveDialog(string initFileName, string fileExt = ".fdw", string filter = "Free Draw Save (*.fdw)|*fdw")
        {
            var dialog = new Microsoft.Win32.SaveFileDialog()
            {
                DefaultExt = fileExt,
                Filter = filter,
                FileName = initFileName,
                InitialDirectory = Directory.GetCurrentDirectory() + "Save"
            };
            return dialog.ShowDialog() == true ? dialog.OpenFile() : Stream.Null;
        }
        private static Stream OpenDialog(string fileExt = ".fdw", string filter = "Free Draw Save (*.fdw)|*fdw")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                DefaultExt = fileExt,
                Filter = filter,
            };
            return dialog.ShowDialog() == true ? dialog.OpenFile() : Stream.Null;
        }

        void EraserFunction()
        {
            if (EraseByPoint_Flag == (int)erase_mode.NONE)
            {
                SetEraserMode(!_eraserMode);
                EraserButton.ToolTip = "Toggle eraser (by point) mode (D)";
                EraseByPoint_Flag = (int)erase_mode.ERASER;
            }
            else if (EraseByPoint_Flag == (int)erase_mode.ERASER)
            {
                SetStaticInfo("Eraser Mode (Point)");
                EraserButton.ToolTip = "Toggle eraser - OFF";
                double s = MainInkCanvas.EraserShape.Height;
                MainInkCanvas.EraserShape = new EllipseStylusShape(s, s);           
                MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                EraseByPoint_Flag = (int)erase_mode.ERASERBYPOINT;
            }
            else if (EraseByPoint_Flag == (int)erase_mode.ERASERBYPOINT)
            {
                SetEraserMode(!_eraserMode);
                EraserButton.ToolTip = "Toggle eraser mode (E)";
                EraseByPoint_Flag = (int)erase_mode.NONE;
            }
        }
        #endregion


        #region /---------Ink---------/
        private readonly Stack<StrokesHistoryNode> _history;
        private readonly Stack<StrokesHistoryNode> _redoHistory;
        private bool _ignoreStrokesChange;

        private void Undo()
        {
            if (!CanUndo()) return;
            var last = Pop(_history);
            _ignoreStrokesChange = true;
            if (last.Type == StrokesHistoryNodeType.Added)
                MainInkCanvas.Strokes.Remove(last.Strokes);
            else
                MainInkCanvas.Strokes.Add(last.Strokes);
            _ignoreStrokesChange = false;
            Push(_redoHistory, last);
        }
        private void Redo()
        {
            if (!CanRedo()) return;
            var last = Pop(_redoHistory);
            _ignoreStrokesChange = true;
            if (last.Type == StrokesHistoryNodeType.Removed)
                MainInkCanvas.Strokes.Remove(last.Strokes);
            else
                MainInkCanvas.Strokes.Add(last.Strokes);
            _ignoreStrokesChange = false;
            Push(_history, last);
        }

        private static void Push(Stack<StrokesHistoryNode> collection, StrokesHistoryNode node)
        {
            collection.Push(node);
        }
        private static StrokesHistoryNode Pop(Stack<StrokesHistoryNode> collection)
        {
            return collection.Count == 0 ? null : collection.Pop();
        }
        private bool CanUndo()
        {
            return _history.Count != 0;
        }
        private bool CanRedo()
        {
            return _redoHistory.Count != 0;
        }
        private void StrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            if (_ignoreStrokesChange) return;
            _saved = false;
            if (e.Added.Count != 0)
                Push(_history, new StrokesHistoryNode(e.Added, StrokesHistoryNodeType.Added));
            if (e.Removed.Count != 0)
                Push(_history, new StrokesHistoryNode(e.Removed, StrokesHistoryNodeType.Removed));
            ClearHistory(_redoHistory);
        }

        private void ClearHistory()
        {
            ClearHistory(_history);
            ClearHistory(_redoHistory);
        }
        private static void ClearHistory(Stack<StrokesHistoryNode> collection)
        {
            collection?.Clear();
        }
        private void Clear()
        {
            MainInkCanvas.Strokes.Clear();
            ClearHistory();
        }

        private void AnimatedClear()
        {
            var ani = new DoubleAnimation(0, Duration3);
            ani.Completed += ClearAniComplete; ;
            MainInkCanvas.BeginAnimation(OpacityProperty, ani);
        }
        private void ClearAniComplete(object sender, EventArgs e)
        {
            Clear();
            Display("Cleared");
            MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Duration3));
        }
        #endregion


        #region /---------UI---------/
        private void DetailToggler_Click(object sender, RoutedEventArgs e)
        {
            SetDetailPanel(!_displayDetailPanel);
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = false;
            var anim = new DoubleAnimation(0, Duration3);
            anim.Completed += Exit;
            BeginAnimation(OpacityProperty, anim);
        }

        private void ColorPickers_Click(object sender, RoutedEventArgs e)
        {
            var border = sender as ColorPicker;
            if (border == null) return;
            SetColor(border);

            if(EraseByPoint_Flag != (int)erase_mode.NONE)
            {
                SetEraserMode(false);
                EraseByPoint_Flag = (int)erase_mode.NONE;
                EraserButton.ToolTip = "Toggle eraser mode (E)";
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //SetBrushSize(e.NewValue);
        }

        private void BrushSize(object sender, MouseWheelEventArgs e)
        {
            int delta = e.Delta;
            if (delta < 0)
                _brushIndex--;
            else
                _brushIndex++;

            if (_brushIndex > _brushSizes.Count() - 1)
                _brushIndex = 0;
            else if (_brushIndex < 0)
                _brushIndex = _brushSizes.Count() - 1;

            SetBrushSize(_brushSizes[_brushIndex]);
        }

        private void BrushSwitchButton_Click(object sender, RoutedEventArgs e)
        {
            _brushIndex++;
            if (_brushIndex > _brushSizes.Count() - 1) _brushIndex = 0;
            SetBrushSize(_brushSizes[_brushIndex]);
        }
        private void LineButton_Click(object sender, RoutedEventArgs e)
        {
            LineMode(!_lineMode);
        }
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }
        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            Redo();
        }
        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            if(_enable)
            {
                EraserFunction();                
            }
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            AnimatedClear(); //Warning! to missclick erasermode (confirmation click?)
        }
        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            SetTopMost(!Topmost);
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainInkCanvas.Strokes.Count == 0)
            {
                Display("Nothing to save");
                return;
            }
            QuickSave();
        }
        private void SaveButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (MainInkCanvas.Strokes.Count == 0)
            {
                Display("Nothing to save");
                return;
            }
            Save(SaveDialog(GenerateFileName()));
        }
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!PromptToSave()) return;
            var s = OpenDialog();
            if (s == Stream.Null) return;
            AnimatedReload(Load(s));
        }
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainInkCanvas.Strokes.Count == 0)
            {
                Display("Nothing to save");
                return;
            }
            try
            {
                var s = SaveDialog("ImageExport_" + GenerateFileName(".png"), ".png",
                    "Portable Network Graphics (*png)|*png");
                if (s == Stream.Null) return;
                var rtb = new RenderTargetBitmap((int)MainInkCanvas.ActualWidth, (int)MainInkCanvas.ActualHeight, 96d,
                    96d, PixelFormats.Pbgra32);
                rtb.Render(MainInkCanvas);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(s);
                s.Close();
                Display("Image Exported");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Display("Export failed");
            }
        }
        private delegate void NoArgDelegate();
        private void ExportButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (MainInkCanvas.Strokes.Count == 0)
            {
                Display("Nothing to save");
                return;
            }
            try
            {
                var s = SaveDialog("ImageExportWithBackground_" + GenerateFileName(".png"), ".png", "Portable Network Graphics (*png)|*png");
                if (s == Stream.Null) return;
                Palette.Opacity = 0;
                Palette.Dispatcher.Invoke(DispatcherPriority.Render, (NoArgDelegate)delegate { });
                Thread.Sleep(100);
                var fromHwnd = Graphics.FromHwnd(IntPtr.Zero);
                var w = (int)(SystemParameters.PrimaryScreenWidth * fromHwnd.DpiX / 96.0);
                var h = (int)(SystemParameters.PrimaryScreenHeight * fromHwnd.DpiY / 96.0);
                var image = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                Graphics.FromImage(image).CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(w, h), CopyPixelOperation.SourceCopy);
                image.Save(s, ImageFormat.Png);
                Palette.Opacity = 1;
                s.Close();
                Display("Image Exported");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Display("Export failed");
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            SetInkVisibility(!_inkVisibility);
        }
        private void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            SetEnable(!_enable);
            if(_eraserMode)
            {
                SetEraserMode(!_eraserMode);
                EraserButton.ToolTip = "Toggle eraser mode (E)";
                EraseByPoint_Flag = (int)erase_mode.NONE;
            }
        }
        private void OrientationButton_Click(object sender, RoutedEventArgs e)
        {
            SetOrientation(!_displayOrientation);
        }
        #endregion


        #region  /---------Docking---------/

        enum DockingDirection
        {
            None,
            Top,
            Left,
            Right
        }
        private int _dockingEdgeThreshold = 30;
        private int _dockingAwaitTime = 10000;
        private int _dockingSideIndent = 290;
        private void AnimatedCanvasMoving(UIElement ctr, Point to, Duration dur)
        {
            ctr.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(Canvas.GetTop(ctr), to.Y, dur));
            ctr.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(Canvas.GetLeft(ctr), to.X, dur));
        }

        private DockingDirection CheckDocking()
        {
            var left = Canvas.GetLeft(Palette);
            var right = Canvas.GetRight(Palette);
            var top = Canvas.GetTop(Palette);

            if (left > 0 && left < _dockingEdgeThreshold)
                return DockingDirection.Left;
            if (right > 0 && right < _dockingEdgeThreshold)
                return DockingDirection.Right;
            if (top > 0 && top < _dockingEdgeThreshold)
                return DockingDirection.Top;
            return DockingDirection.None;
        }

        private void RightDocking()
        {
            AnimatedCanvasMoving(Palette, new Point(ActualWidth + _dockingSideIndent, Canvas.GetTop(Palette)), Duration5);
        }
        private void LeftDocking()
        {
            AnimatedCanvasMoving(Palette, new Point(0 - _dockingSideIndent, Canvas.GetTop(Palette)), Duration5);
        }
        private void TopDocking()
        {

        }

        private async void AwaitDocking()
        {

            await Docking();
        }

        private Task Docking()
        {
            return Task.Run(() =>
            {
                Thread.Sleep(_dockingAwaitTime);
                var direction = CheckDocking();
                if (direction == DockingDirection.Left) LeftDocking();
                if (direction == DockingDirection.Right) RightDocking();
                if (direction == DockingDirection.Top) TopDocking();
            });
        }
        #endregion


        #region /---------Dragging---------/
        private Point _lastMousePosition;
        private bool _isDraging;
        private bool _tempEnable;

        private void StartDrag()
        {
            _lastMousePosition = Mouse.GetPosition(this);
            _isDraging = true;
            Palette.Background = new SolidColorBrush(Colors.Transparent);
            _tempEnable = _enable;
            SetEnable(true);
        }
        private void EndDrag()
        {
            if (_isDraging == true)
            {
                SetEnable(_tempEnable);
            }
            _isDraging = false;
            Palette.Background = null;
        }
        private void PaletteGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            StartDrag();
        }
        private void Palette_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraging) return;
            var currentMousePosition = Mouse.GetPosition(this);
            var offset = currentMousePosition - _lastMousePosition;

            Canvas.SetTop(Palette, Canvas.GetTop(Palette) + offset.Y);
            Canvas.SetLeft(Palette, Canvas.GetLeft(Palette) + offset.X);

            _lastMousePosition = currentMousePosition;
        }
        private void Palette_MouseUp(object sender, MouseButtonEventArgs e)
        { 
            EndDrag();
        }
        private void Palette_MouseLeave(object sender, MouseEventArgs e)
        {
            EndDrag();
        }
        #endregion

        #region /--------- Shortcuts --------/
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.R)
            {
                SetEnable(!_enable);
            }
            if (!_enable) 
                return;
            
            switch (e.Key)
            {
                case Key.Z:
                    Undo();
                    break;
                case Key.Y:
                    Redo();
                    break;
                case Key.E:
                    EraserFunction();
                    break;
                case Key.B:
                    if (_eraserMode == true)
                        SetEraserMode(false);
                    SetEnable(true);
                    break;
                case Key.L:
                    if (_eraserMode == true)
                        SetEraserMode(false);
                    LineMode(true);
                    break;

                /*
                case Key.D:
                    if (EraseByPoint_Flag is ((int)erase_mode.NONE) or ((int)erase_mode.ERASER))
                    {
                        SetStaticInfo("Eraser Mode (Point)");
                        MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                        EraseByPoint_Flag = (int)erase_mode.ERASERBYPOINT;
                    }
                    else if (EraseByPoint_Flag == (int)erase_mode.ERASERBYPOINT)
                    {
                        MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                        EraseByPoint_Flag = (int)erase_mode.NONE;
                    }
                    break;
                */
                case Key.Add:
                    _brushIndex++;
                    if (_brushIndex > _brushSizes.Count() - 1)
                        _brushIndex = 0;
                    SetBrushSize(_brushSizes[_brushIndex]);
                    break;
                case Key.Subtract:
                    _brushIndex--;
                    if (_brushIndex < 0)
                        _brushIndex = _brushSizes.Count() - 1;
                    SetBrushSize(_brushSizes[_brushIndex]);
                    break;
            }            
        }
        #endregion

        #region /------ Line Mode -------/
        private bool _isMoving = false;
        private bool _lineMode = false;
        private Point _startPoint;
        private Stroke _lastStroke;
      
        private void LineMode(bool l)
        {
            LineButton.IsActived = l;
            _lineMode = l;
            //EraseByPoint_Flag = (int)erase_mode.NONE;
            if (_lineMode)
            {
                SetStaticInfo("LineMode");
                MainInkCanvas.EditingMode = InkCanvasEditingMode.None;
                MainInkCanvas.UseCustomCursor = true;
            }
            else
                SetEnable(true);
        }
        private void StartLine(object sender, MouseButtonEventArgs e)
        {
            _isMoving = true;
            _startPoint = e.GetPosition(MainInkCanvas);
            _lastStroke = null;
            _ignoreStrokesChange = true;
        }
        private void EndLine(object sender, MouseButtonEventArgs e)
        {
            if (_isMoving == true)
            {
                Point endPoint = e.GetPosition(MainInkCanvas);
                if (_lastStroke != null)
                {
                    StrokeCollection collection = new StrokeCollection();
                    collection.Add(_lastStroke);
                    Push(_history, new StrokesHistoryNode(collection, StrokesHistoryNodeType.Added));
                }

            }
            _isMoving = false;
            _ignoreStrokesChange = false;
        }
        private void MakeLine(object sender, MouseEventArgs e)
        {
            if (_isMoving == false)
                return;

            DrawingAttributes newLine = MainInkCanvas.DefaultDrawingAttributes.Clone();
            Stroke stroke = null;
            newLine.StylusTip = StylusTip.Ellipse;
            newLine.IgnorePressure = true;

            Point _endPoint = e.GetPosition(MainInkCanvas);

            List<Point> pList = new List<Point>
            {
                new Point(_startPoint.X, _startPoint.Y),
                new Point(_endPoint.X, _endPoint.Y),
            };

            StylusPointCollection point = new StylusPointCollection(pList);
            stroke = new Stroke(point) { DrawingAttributes = newLine, };

            if (_lastStroke != null)
                MainInkCanvas.Strokes.Remove(_lastStroke);
            if (stroke != null)
                MainInkCanvas.Strokes.Add(stroke);

            _lastStroke = stroke;
        }
        #endregion
    }
}
