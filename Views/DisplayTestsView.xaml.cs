using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace atp_enterprise_app_wpf.Views
{
    public partial class DisplayTestsView : UserControl
    {
        public DisplayTestsView()
        {
            InitializeComponent();
            LoadDisplayStats();
            BuildGrid();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private void LoadDisplayStats()
        {
            try
            {
                double width = SystemParameters.PrimaryScreenWidth;
                double height = SystemParameters.PrimaryScreenHeight;
                TxtResolution.Text = $"{width:F0} x {height:F0} px";

                // Native color depth query using GDI P/Invoke
                IntPtr hdc = GetDC(IntPtr.Zero);
                int bitsPerPixel = GetDeviceCaps(hdc, 12); // BITSPIXEL = 12
                ReleaseDC(IntPtr.Zero, hdc);
                
                TxtDepth.Text = $"{bitsPerPixel} Bits per Pixel";
            }
            catch
            {
                TxtResolution.Text = "Not Available";
                TxtDepth.Text = "Sensor Not Supported";
            }
        }

        private void BuildGrid()
        {
            DigitizerGrid.Children.Clear();
            for (int i = 0; i < 64; i++)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(24, 28, 38)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(34, 43, 60)),
                    BorderThickness = new Thickness(0.5),
                    Margin = new Thickness(1),
                    Tag = false // False = Not Painted
                };
                DigitizerGrid.Children.Add(border);
            }
        }

        private void ClearGrid_Click(object sender, RoutedEventArgs e)
        {
            BuildGrid();
        }

        private void PaintCellAtPoint(Point pt)
        {
            // Find which cell was clicked or dragged over
            double cellW = DigitizerGrid.ActualWidth / 8.0;
            double cellH = DigitizerGrid.ActualHeight / 8.0;

            if (cellW <= 0 || cellH <= 0) return;

            int col = (int)(pt.X / cellW);
            int row = (int)(pt.Y / cellH);

            if (col >= 0 && col < 8 && row >= 0 && row < 8)
            {
                int index = row * 8 + col;
                if (index >= 0 && index < DigitizerGrid.Children.Count)
                {
                    if (DigitizerGrid.Children[index] is Border border)
                    {
                        border.Background = new SolidColorBrush(Color.FromRgb(82, 196, 26)); // Green
                        border.Tag = true;
                    }
                }
            }
        }

        private void DigitizerGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point pt = e.GetPosition(DigitizerGrid);
                PaintCellAtPoint(pt);
            }
        }

        private void DigitizerGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point pt = e.GetPosition(DigitizerGrid);
            PaintCellAtPoint(pt);
        }

        private void LaunchCycler_Click(object sender, RoutedEventArgs e)
        {
            var cyclerWin = new Window
            {
                WindowStyle = WindowStyle.None,
                WindowState = WindowState.Maximized,
                Topmost = true,
                Background = Brushes.Red,
                Cursor = Cursors.None
            };

            int clickCount = 0;
            Brush[] colors = { Brushes.Green, Brushes.Blue, Brushes.White, Brushes.Black, Brushes.Red };

            cyclerWin.MouseDown += (s, ev) =>
            {
                if (ev.LeftButton == MouseButtonState.Pressed)
                {
                    cyclerWin.Background = colors[clickCount % colors.Length];
                    clickCount++;
                }
            };

            cyclerWin.KeyDown += (s, ev) =>
            {
                if (ev.Key == Key.Escape)
                {
                    cyclerWin.Close();
                }
            };

            cyclerWin.Show();
        }
    }
}
