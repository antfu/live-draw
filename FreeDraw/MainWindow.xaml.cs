using System;
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
        private Point _lastMousePosition;
        private bool _isDraging = false;
        private Button _selectedColor;
        private string _staticInfo = "";
        private bool _displayingInfo;
        private bool _displayDetailPanel;
        private bool _inkVisable = true;
        private bool _eraserMode;
        private bool _enable;

        private readonly static Duration AnimationDuration0 = new Duration(new TimeSpan(0, 0, 0, 0, 100));
        private readonly static Duration AnimationDuration1 = new Duration(new TimeSpan(0, 0, 0, 0, 300));
        private readonly static Duration AnimationDuration2 = new Duration(new TimeSpan(0, 0, 0, 0, 500));
        private const string ButtonActived = "Actived";
        private const string ButtonUnactived = "";
        #region Mouse Throught

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


        #endregion

        public MainWindow()
        {
            if (!Directory.Exists("Save"))
                Directory.CreateDirectory("Save");
            InitializeComponent();
            SelectColor(DefaultColor);
            SetEnable(true);
            DetailPanel.Opacity = 0;
        }

        private void ToggleDetail(bool v)
        {
            if (v)
            {
                DetailTogglerRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(90, AnimationDuration1));
                ColorPickerController.BeginAnimation(WidthProperty, new DoubleAnimation(40, AnimationDuration1));
                DetailPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, AnimationDuration2));
                PaletteGrip.BeginAnimation(WidthProperty,new DoubleAnimation(140,AnimationDuration1));
            }
            else
            {
                DetailTogglerRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(270, AnimationDuration1));
                ColorPickerController.BeginAnimation(WidthProperty, new DoubleAnimation(25, AnimationDuration1));
                DetailPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, AnimationDuration2));
                PaletteGrip.BeginAnimation(WidthProperty, new DoubleAnimation(80, AnimationDuration1));
            }
            _displayDetailPanel = v;
        }

        private void SetInkVisible(bool v)
        {
            if (v)
            {
                MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, AnimationDuration1));
                HideButton.Tag = ButtonUnactived;
            }
            else
            {
                
                MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, AnimationDuration1));
                HideButton.Tag = ButtonActived;
            }
            SetEnable(v);
            _inkVisable = v;
        }
        private void SetEnable(bool b)
        {
            if (b)
            {
                EnableButton.Tag = ButtonUnactived;
                Background = Resources["FakeTransparent"] as Brush;
            }
            else
            {
                EnableButton.Tag = ButtonActived;
                Background = Resources["TrueTransparent"] as Brush;
            }
            _enable = b;
        }

        private void SelectColor(Button b)
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

        private static string GenerateFileName()
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".fdw";
        }

        private static string[] GetSavePathList()
        {
            return Directory.GetFiles("Save", "*.fdw");
        }

        private static string GetFileNameFromPath(string path)
        {
            return Path.GetFileName(path);
        }

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

        #region Controls
        private void ColorSelector_MouseDown(object sender, RoutedEventArgs e)
        {
            var border = sender as Button;
            if (border == null) return;
            SelectColor(border);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var s = e.NewValue * e.NewValue;
            MainInkCanvas.DefaultDrawingAttributes.Height = s;
            MainInkCanvas.DefaultDrawingAttributes.Width = s;
            brushPreview.Height = s;
            brushPreview.Width = s;
        }

        private void CloseButton_MouseDown(object sender, RoutedEventArgs e)
        {
            Topmost = false;
            var anim = new DoubleAnimation(0,AnimationDuration1);
            anim.Completed += FadeOutCompleted;
            BeginAnimation(OpacityProperty,anim);
        }

        private void FadeOutCompleted(object sender, EventArgs e)
        {
            Application.Current.Shutdown(0);
        }

        private void EraserButton_MouseDown(object sender, RoutedEventArgs e)
        {
            if (_eraserMode)
            {
                MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                EraserButton.Tag = null;
                _eraserMode = false;
                SetStaticInfo("");
            }
            else
            {
                MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                EraserButton.Tag = "Actived";
                _eraserMode = true;
                SetStaticInfo("Eraser Mode");
            }
        }

        private void UndoButton_MouseDown(object sender, RoutedEventArgs e)
        {
            Display("Function Unfinished");
        }

        private void SaveButton_MouseDown(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog()
            {
                DefaultExt = ".fdw",
                Filter = "Free Draw Save (*.fdw)|*fdw|Extensible Markup Language (*.xml)|*.xml",
                FileName = GenerateFileName(),
                InitialDirectory = Directory.GetCurrentDirectory() + "Save"
            };
            var r = dialog.ShowDialog();
            if (r == true)
            {
                try
                {
                    using (var s = dialog.OpenFile())
                        MainInkCanvas.Strokes.Save(s);
                    Display("Saved to " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    Display("Fail to save");
                }
            }
        }

        private void LoadButton_MouseDown(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                DefaultExt = ".fdw",
                Filter = "Free Draw Save (*.fdw)|*fdw|Extensible Markup Language (*.xml)|*.xml",
                
            };
            var r = dialog.ShowDialog();
            if (r == true)
            {
                try
                {
                    using (var s = dialog.OpenFile())
                        MainInkCanvas.Strokes = new StrokeCollection(s);
                    Display("Loaded from " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    Display("Fail to load");
                }
            }
        }

        private void MinimizeButton_MouseDown(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void ClearButton_MouseDown(object sender, RoutedEventArgs e)
        {
            MainInkCanvas.Strokes.Clear();
            Display("Cleared");
        }

        private void EnableButton_MouseDown(object sender, RoutedEventArgs e)
        {
            SetEnable(!_enable);
        }

        private void DetailToggler_Click(object sender, RoutedEventArgs e)
        {
            ToggleDetail(!_displayDetailPanel);
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            SetInkVisible(!_inkVisable);
        }
        #region Drag
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

        #endregion


    }
}
