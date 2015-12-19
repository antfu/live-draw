using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Threading;

namespace AntFu7.FreeDraw
{
    public partial class MainWindow : Window
    {
        private static readonly Duration Duration1 = (Duration)Application.Current.Resources["Duration1"];
        private static readonly Duration Duration2 = (Duration)Application.Current.Resources["Duration2"];
        private static readonly Duration Duration3 = (Duration)Application.Current.Resources["Duration3"];
        private static readonly Duration Duration4 = (Duration)Application.Current.Resources["Duration4"];
        private static readonly Duration Duration5 = (Duration)Application.Current.Resources["Duration5"];

        private const string ButtonActived = "Actived";
        private const string ButtonUnactived = "";

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
            _history = new Stack<StrokesHistoryNode>();
            _redoHistory = new Stack<StrokesHistoryNode>();
            if (!Directory.Exists("Save"))
                Directory.CreateDirectory("Save");
            InitializeComponent();
            SetColor(DefaultColor);
            SetEnable(true);
            DetailPanel.Opacity = 0;
            MainInkCanvas.Strokes.StrokesChanged += StrokesChanged;
        }
        private static void Exit(object sender, EventArgs e)
        {
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
        private Button _selectedColor;
        private bool _inkVisibility = true;
        private bool _displayDetailPanel;
        private bool _eraserMode;
        private bool _enable;

        private void SetDetailPanel(bool v)
        {
            if (v)
            {
                DetailTogglerRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(180, Duration3));
                ColorPickerController.BeginAnimation(WidthProperty, new DoubleAnimation(40, Duration3));
                DetailPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Duration4));
                PaletteGrip.BeginAnimation(WidthProperty, new DoubleAnimation(130, Duration3));
            }
            else
            {
                DetailTogglerRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, Duration3));
                ColorPickerController.BeginAnimation(WidthProperty, new DoubleAnimation(25, Duration3));
                DetailPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, Duration4));
                PaletteGrip.BeginAnimation(WidthProperty, new DoubleAnimation(80, Duration3));
            }
            _displayDetailPanel = v;
        }
        private void SetInkVisibility(bool v)
        {
            if (v)
            {
                MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Duration3));
                HideButton.Tag = ButtonUnactived;
            }
            else
            {
                MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, Duration3));
                HideButton.Tag = ButtonActived;
            }
            SetEnable(v);
            _inkVisibility = v;
        }
        private void SetEnable(bool b)
        {
            if (b)
            {
                EnableButton.Tag = ButtonUnactived;
                Background = Application.Current.Resources["FakeTransparent"] as Brush;
            }
            else
            {
                EnableButton.Tag = ButtonActived;
                Background = Application.Current.Resources["TrueTransparent"] as Brush;
            }
            _enable = b;
        }
        private void SetColor(Button b)
        {
            if (ReferenceEquals(_selectedColor, b)) return;
            var solidColorBrush = b.Background as SolidColorBrush;
            if (solidColorBrush == null) return;
            MainInkCanvas.DefaultDrawingAttributes.Color = solidColorBrush.Color;
            brushPreview.Background = b.Background;
            b.Tag = "Selected";
            if (_selectedColor != null)
                _selectedColor.Tag = null;
            _selectedColor = b;
        }
        private void SetBrushSize(double s)
        {
            s = s * s;
            MainInkCanvas.DefaultDrawingAttributes.Height = s;
            MainInkCanvas.DefaultDrawingAttributes.Width = s;
            brushPreview.Height = s;
            brushPreview.Width = s;
        }
        private void SetEraserMode(bool v)
        {
            if (_eraserMode)
            {
                MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                EraserButton.Tag = ButtonActived;
                SetStaticInfo("Eraser Mode");
            }
            else
            {
                MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                EraserButton.Tag = null;
                SetStaticInfo("");
            }
            _eraserMode = v;
        }
        #endregion


        #region /---------IO---------/
        private StrokeCollection _preLoadStrokes = null;
        private void QuickSave(string filename = "")
        {
            Save(new FileStream("Save\\" + filename + GenerateFileName(), FileMode.OpenOrCreate));
        }
        private void Save(Stream fs)
        {
            try
            {
                MainInkCanvas.Strokes.Save(fs);
                _saved = true;
                Display("Ink saved");
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
        private void AnimatedLoad(Stream fs)
        {
            _preLoadStrokes = Load(fs);
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
        private static string GenerateFileName()
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".fdw";
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
            if (!PromptToSave()) return;
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
            var border = sender as Button;
            if (border == null) return;
            SetColor(border);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetBrushSize(e.NewValue);
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
            SetEraserMode(!_eraserMode);
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            AnimatedClear();
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
            var dialog = new Microsoft.Win32.SaveFileDialog()
            {
                DefaultExt = ".fdw",
                Filter = "Free Draw Save (*.fdw)|*fdw",
                FileName = GenerateFileName(),
                InitialDirectory = Directory.GetCurrentDirectory() + "Save"
            };
            var r = dialog.ShowDialog();
            if (r == true)
            {
                Save(dialog.OpenFile());
            }
        }
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!PromptToSave()) return;
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                DefaultExt = ".fdw",
                Filter = "Free Draw Save (*.fdw)|*fdw",

            };
            var r = dialog.ShowDialog();
            if (r == true)
            {
                AnimatedLoad(dialog.OpenFile());
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
        }
        #endregion


        #region /---------Dragging---------/
        private Point _lastMousePosition;
        private bool _isDraging;

        private void StartDrag()
        {
            _lastMousePosition = Mouse.GetPosition(this);
            _isDraging = true;
            Palette.Background = new SolidColorBrush(Colors.Transparent);
        }
        private void EndDrag()
        {
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
        { EndDrag(); }
        private void Palette_MouseLeave(object sender, MouseEventArgs e)
        { EndDrag(); }
        #endregion

    }
}
