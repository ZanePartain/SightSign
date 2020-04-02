using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SightSign
{
    /// <summary>
    /// Interaction logic for CircularGazeButton.xaml
    /// </summary>
    public partial class CircularGazeButton : UserControl
    {
        private static double GazeTime = 3000.0, IntervolTime = 60.0;
        private int TimerCount = 0;
        public System.Timers.Timer gazeTimer = new System.Timers.Timer(GazeTime);
        public System.Timers.Timer intervolTimer = new System.Timers.Timer(IntervolTime);

        public CircularGazeButton()
        {
            InitializeComponent();
            gazeTimer.Elapsed += OnProgressButton_Gaze;
            intervolTimer.Elapsed += IntervolTimer_Elapsed;
            Angle = 0;
            RenderArc();
        }

        #region DependencyVariables

        public int Radius
        {
            get { return (int)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        public Brush SegmentColor
        {
            get { return (Brush)GetValue(SegmentColorProperty); }
            set { SetValue(SegmentColorProperty, value); }
        }

        public int StrokeThickness
        {
            get { return (int)GetValue(StrokeThicknessProperty); }
            set { SetValue(StrokeThicknessProperty, value); }
        }

        public double Angle
        {
            get { return (double)GetValue(AngleProperty); }
            set { SetValue(AngleProperty, value); }
        }

        public RoutedEventHandler GazeClick
        {
            get { return (RoutedEventHandler)GetValue(GazeClickProperty); }
            set { SetValue(GazeClickProperty, value); }
        }

        public string ButtonText
        {
            get { return (string)GetValue(ButtonTextProperty); }
            set { SetValue(ButtonTextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StrokeThickness.
        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register("StrokeThickness", typeof(int), typeof(CircularGazeButton), new PropertyMetadata(5));

        // Using a DependencyProperty as the backing store for SegmentColor.
        public static readonly DependencyProperty SegmentColorProperty =
            DependencyProperty.Register("SegmentColor", typeof(Brush), typeof(CircularGazeButton), new PropertyMetadata(new SolidColorBrush(Colors.Red)));

        // Using a DependencyProperty as the backing store for Radius.
        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register("Radius", typeof(int), typeof(CircularGazeButton), new PropertyMetadata(25, new PropertyChangedCallback(OnPropertyChanged)));

        // Using a DependencyProperty as the backing store for Angle.
        public static readonly DependencyProperty AngleProperty =
            DependencyProperty.Register("Angle", typeof(double), typeof(CircularGazeButton), new PropertyMetadata(120d, new PropertyChangedCallback(OnPropertyChanged)));

        // Using a DependencyProperty as the backing store for Click.
        public static readonly DependencyProperty GazeClickProperty =
            DependencyProperty.Register("GazeClick", typeof(RoutedEventHandler), typeof(CircularGazeButton), new PropertyMetadata((object)null));

        // Using a DependencyProperty as the backing store for ButtonText.
        public static readonly DependencyProperty ButtonTextProperty =
            DependencyProperty.Register("ButtonText", typeof(string), typeof(CircularGazeButton), new PropertyMetadata(new string("Content".ToCharArray())));

        #endregion DependencyVariables

        private static void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            CircularGazeButton circle = sender as CircularGazeButton;
            circle.RenderArc();
        }

        #region TimerHandlers
        private void OnProgressButton_Gaze(object sender, System.Timers.ElapsedEventArgs args)
        {
            intervolTimer.Enabled = false;
            gazeTimer.Enabled = false;
            this.Dispatcher.Invoke(new Action(() => {
                Angle = 360;
            }));
            this.Dispatcher.Invoke(new Action(() => {
                var handler = GazeClick;
                if (handler != null)
                {
                    handler(this, new RoutedEventArgs());
                }
            }));
        }

        private void IntervolTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            TimerCount++;
            double tempPerc = IntervolTime / (GazeTime - IntervolTime);
            tempPerc = tempPerc > .99 ? .99 : tempPerc;
            this.Dispatcher.Invoke(new Action(() =>
            {
                this.Angle = (tempPerc * TimerCount) * 360;
            }));
        }

        public void OnTimerStart(object sender, System.Windows.Input.MouseEventArgs e)
        {
            intervolTimer.Enabled = true;
            gazeTimer.Enabled = true;
        }

        public void OnTimerEnd(object sender, System.Windows.Input.MouseEventArgs e)
        {
            gazeTimer.Enabled = false;
            intervolTimer.Enabled = false;
            TimerCount = 0;
            Angle = 0;
        }
        #endregion TimerHandlers

        #region MemberFunctions
        public void RenderArc()
        {
            Point startPoint = new Point(Radius, 0);
            Point endPoint = ComputeCartesianCoordinate(Angle, Radius);
            endPoint.X += Radius;
            endPoint.Y += Radius;

            pathRoot.Width = Radius * 2 + StrokeThickness;
            pathRoot.Height = Radius * 2 + StrokeThickness;
            pathRoot.Margin = new Thickness(StrokeThickness, StrokeThickness, 0, 0);

            bool largeArc = Angle > 180.0;

            Size outerArcSize = new Size(Radius, Radius);

            pathFigure.StartPoint = startPoint;

            if (startPoint.X == Math.Round(endPoint.X) && startPoint.Y == Math.Round(endPoint.Y))
                endPoint.X -= 0.01;

            arcSegment.Point = endPoint;
            arcSegment.Size = outerArcSize;
            arcSegment.IsLargeArc = largeArc;
        }

        private Point ComputeCartesianCoordinate(double angle, double radius)
        {
            // convert to radians
            double angleRad = (Math.PI / 180.0) * (angle - 90);

            double x = radius * Math.Cos(angleRad);
            double y = radius * Math.Sin(angleRad);

            return new Point(x, y);
        }
        #endregion MemberFunctions
    }

    #region ValueConverters
    public class ColorToSolidBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var color = (Color)value;

            return new SolidColorBrush(Color.FromArgb(
                color.A, color.R, color.G, color.B));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion ValueConverters
}
