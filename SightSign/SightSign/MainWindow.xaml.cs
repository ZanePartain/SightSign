﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.HandsFree.Mouse;
using Microsoft.Win32;
using System.Windows.Media.Imaging;

namespace SightSign
{
    // This Window hosts two InkCanvases. The InkCanvas that's lower in the z-order shows ink 
    // which is to be traced out by an animating dot. As the dot moves, it leaves a trail of 
    // ink that's added to other InkCanvas. Also as the dot moves, the app moves a robot arm 
    // such that the arm follows the same path as the dot. 
    public partial class MainWindow
    {
        public RobotArm RobotArm { get; }
        private readonly Settings _settings;

        // Related to the animation of the dot.
        private Stroke _strokeBeingAnimated;
        private int _currentAnimatedStrokeIndex;
        private int _currentAnimatedPointIndex;
        private DispatcherTimer _dispatcherTimerDotAnimation;
        private bool _inTimer;

        private bool _stampInProgress;

        public MainWindow()
        {
            InitializeComponent();

            WindowState = WindowState.Maximized;

            // Assume the screen size won't change after the app starts.
            var xScreen = SystemParameters.PrimaryScreenWidth;
            var yScreen = SystemParameters.PrimaryScreenHeight;
            RobotArm = new RobotArm(
                xScreen / 2.0,
                yScreen / 2.0,
                Math.Min(xScreen, yScreen) / 2.0,
                Settings1.Default.RobotType == "Swift" ? ((IArm)new UArmSwiftPro()) : ((IArm)new UArmMetal()));

            _settings = new Settings(RobotArm);
            DataContext = _settings;

            Background = new SolidColorBrush(_settings.BackgroundColor);

            if (_settings.RobotControl)
            {
                RobotArm.Connect();
                RobotArm.ArmDown(false); // Lift the arm.
            }

            SetDrawingAttributesFromSettings(inkCanvas.DefaultDrawingAttributes);

            LoadInkOnStartup();

            this.SetDrawingZoneRectangle(0);  // set the init drawing zone rectangle
            this.areaText.Visibility = Visibility.Collapsed;  // init as collapsed
        }

        private void SetDrawingAttributesFromSettings(DrawingAttributes attributes)
        {
            attributes.Color = _settings.InkColor;
            attributes.Width = _settings.InkWidth;
            attributes.Height = _settings.InkWidth;

            attributes.StylusTip = StylusTip.Ellipse;
        }

        protected override void OnClosed(EventArgs e)
        {
            // Now disconnect the arm.
            RobotArm.Close();

            base.OnClosed(e);
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            GazeMouse.Attach(this);
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            GazeMouse.DetachAll();
        }

        #region LoadInk
        // Load up ink based on the ink that was shown when the app was last run.
        private void LoadInkOnStartup()
        {
            var filename = Settings1.Default.LoadedInkLocation;
            if (string.IsNullOrEmpty(filename))
            {
                // Look for default ink if we can find it in the same folder as the exe.
                filename = AppDomain.CurrentDomain.BaseDirectory + "Signature.isf";
            }

            if (File.Exists(filename))
            {
                AddInkFromFile(filename);
            }
        }

        // Add ink to the InkCanvas, based on the contents of the supplied ISF file.
        private void AddInkFromFile(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return;
            }

            // Remove any existing ink first.
            inkCanvas.Strokes.Clear();

            // Assume the file is valid and accessible.
            var file = new FileStream(filename, FileMode.Open, FileAccess.Read);
            var strokeCollection = new StrokeCollection(file);
            file.Close();

            if (strokeCollection.Count > 0)
            {
                // Add ink to the InkCanvas, similar to the ink loaded from the supplied file,
                // but with evenly distributed points along the strokes.
                GenerateStrokesWithEvenlyDistributedPoints(strokeCollection);

                ApplySettingsToInk();
            }

            // show current area.
            // also establishes  4-dot configuration scale for robot.
            // this.DrawAreaButton_Click(null, null);
        }

        // Apply the current settings to the currently loaded ink.
        private void ApplySettingsToInk()
        {
            if (inkCanvas.Strokes.Count > 0)
            {
                foreach (var stroke in inkCanvas.Strokes)
                {
                    SetDrawingAttributesFromSettings(stroke.DrawingAttributes);
                }
            }
        }

        // Add ink to the InkCanvas, similar to the ink contained in the supplied StrokeCollection,
        // but with evenly distributed points along the strokes. By doing this, when we animate the
        // dot along the ink shown in the InkCanvas, the dot's speed is constant. If ink generated 
        // by the user was used for dot animation, then the dot's speed would vary based on the 
        // speed at which the ink was written.
        private void GenerateStrokesWithEvenlyDistributedPoints(StrokeCollection strokeCollection)
        {
            double baseLength = 0;

            for (var idx = 0; idx < strokeCollection.Count; ++idx)
            {
                var existingStylusPoints = strokeCollection[idx].StylusPoints;
                if (existingStylusPoints.Count > 0)
                {
                    // First create a PathGeometry from all the points making up this stroke.
                    var start = existingStylusPoints[0].ToPoint();

                    var segments = new List<LineSegment>();

                    for (var i = 1; i < existingStylusPoints.Count; i++)
                    {
                        segments.Add(new LineSegment(existingStylusPoints[i].ToPoint(), true));
                    }

                    var figure = new PathFigure(start, segments, false);
                    var pathGeometry = new PathGeometry();
                    pathGeometry.Figures.Add(figure);

                    // Get the length of the PathGeometry. The number of points created along each
                    // stroke will be proportional to the number of points on the first stroke. For 
                    // example, if the first stroke has 100 points along it, then if the second 
                    // stroke is 50% longer, then that stroke will have 150 points. By doing this,
                    // the animating dot's speed will seem constant for all strokes, regardless of
                    // the length of the strokes.
                    var currentLength = GetLength(pathGeometry);
                    if (idx == 0)
                    {
                        baseLength = currentLength;
                    }

                    // Always add at least two points on the stroke.
                    var count = Math.Max(2, (int)((_settings.AnimationPointsOnFirstStroke * currentLength) / baseLength));

                    // Now generate the StylusPointCollection which will be used to add ink to the InkCanvas.
                    var stylusPoints = new StylusPointCollection();

                    for (var i = 0; i < count; ++i)
                    {
                        var distanceFraction = i / (double)count;

                        Point pt;
                        Point ptTangent;

                        pathGeometry.GetPointAtFractionLength(
                            distanceFraction, out pt, out ptTangent);

                        stylusPoints.Add(new StylusPoint(pt.X, pt.Y));
                    }

                    // Now add the new stroke with the evenly distributed points to the InkCanvas.
                    if (stylusPoints.Count > 0)
                    {
                        var stroke = new Stroke(stylusPoints);
                        inkCanvas.Strokes.Add(stroke);
                    }
                }
            }
        }

        // Determine the full length of a PathGeometry, assuming it's composed only of
        // LineSegments and PolylineSegments. 
        public static double GetLength(PathGeometry pathGeometry)
        {
            var length = 0.0;

            foreach (var pf in pathGeometry.Figures)
            {
                var start = pf.StartPoint;

                foreach (var pathSegment in pf.Segments)
                {
                    var lineSegment = pathSegment as LineSegment;
                    if (lineSegment != null)
                    {
                        length += Distance(start, lineSegment.Point);

                        start = lineSegment.Point;
                    }
                    else
                    {
                        var polylineSegment = pathSegment as PolyLineSegment;
                        if (polylineSegment != null)
                        {
                            foreach (var point in polylineSegment.Points)
                            {
                                length += Distance(start, point);

                                start = point;
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Unexpected data - Segment is neither LineSegment or PolylineSegment.");
                        }
                    }
                }
            }

            return length;
        }

        private static double Distance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        #endregion LoadInk

        #region SendInkToRobotAndAnimateDot
        // Show the dot at the start of the ink, and when that's clicked, animate the dot through
        // the entire signature, sending the point data to the robot as the dot progresses.
        private void StampButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop any in-progress writing visuals.
            ResetWriting();

            _stampInProgress = true;

            WriteSignature();
        }

        // Show the dot at the start of the ink, and when that's clicked, animate the dot through
        // the first stroke. If the user then clicks the dot, it will move to the second stroke. 
        // This continues until the user has moved the dot through all strokes. The point data is
        // sent to the robot as the dot progresses through the ink.
        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop any in-progress writing.
            ResetWriting();

            WriteSignature();
        }

        // Reset visuals associated with dot animation and tracing out ink.
        // Note that this does not have any effect on the robot.
        private void ResetWriting()
        {
            _stampInProgress = false;

            if (_dispatcherTimerDotAnimation != null)
            {
                _dispatcherTimerDotAnimation.Stop();
                _dispatcherTimerDotAnimation = null;
            }

            _currentAnimatedPointIndex = 0;
            _currentAnimatedStrokeIndex = 0;

            inkCanvasAnimations.Strokes.Clear();
            inkCanvasAnimations.Visibility = Visibility.Collapsed;

            dot.Visibility = Visibility.Collapsed;

            foreach (var stroke in inkCanvas.Strokes)
            {
                stroke.DrawingAttributes.Color = _settings.InkColor;
            }
        }

        // Show the dot at the start of the first stroke.
        private void WriteSignature()
        {
            if (inkCanvas.Strokes.Count == 0)
            {
                return;
            }

            // If writing is already in progress, do nothing.
            if (_dispatcherTimerDotAnimation != null)
            {
                return;
            }


            // Prevent the robot from writing strokes that are off of the primary screen.
            Rect newBounds = new Rect();
            newBounds.Width = SystemParameters.PrimaryScreenWidth - 125;     // any ink behind the buttons column will be cropped
            newBounds.Height = SystemParameters.PrimaryScreenHeight - 125;  // any ink below the settings button will be cropped
            inkCanvas.Strokes.Clip(newBounds);

            dot.Visibility = Visibility.Visible;
            dot.Opacity = 1.0;

            // When the dot animates, it leaves a trail of ink behind it. That ink is added to 
            // the inkCanvasAnimations InkCanvas, which lies above the InkCanvas showing the ink
            // being traced out.
            inkCanvasAnimations.Visibility = Visibility.Visible;
            inkCanvasAnimations.Strokes.Clear();

            // The dot moves from point to point along each stroke being traced out.
            _currentAnimatedPointIndex = 0;
            _currentAnimatedStrokeIndex = 0;

            // Apply a translucency to the ink being traced out.
            foreach (var stroke in inkCanvas.Strokes)
            {
                stroke.DrawingAttributes.Color = _settings.FadedInkColor;
            }

            // Lift arm up.
            RobotArm.ArmDown(false);

            // Move to the start of the signature.
            var stylusPointFirst = inkCanvas.Strokes[0].StylusPoints[0];
            MoveDotAndRobotToStylusPoint(stylusPointFirst);

            // We'll create the animation stroke once the animation timer has fired.
            _strokeBeingAnimated = null;

            // Begin the timer used for animations.
            _dispatcherTimerDotAnimation = new DispatcherTimer();
            _dispatcherTimerDotAnimation.Tick += dispatcherTimerDotAnimation_Tick;
            _dispatcherTimerDotAnimation.Interval = new TimeSpan(0, 0, 0, 0, _settings.AnimationInterval);
        }

        private void MoveDotAndRobotToStylusPoint(StylusPoint stylusPt)
        {
            var pt = stylusPt.ToPoint();

            if (dot.Visibility == Visibility.Visible)
            {
                dotTranslateTransform.X = pt.X - (inkCanvas.ActualWidth / 2);
                dotTranslateTransform.Y = pt.Y - (inkCanvas.ActualHeight / 2);
            }

            //TODO :: handle case where drawDimensionSize is less than the inkCanvas.Strokes size
            // Apply the scalingFactor to the point that the robot will draw.
            // FOR TESTING
            if (!isShowingDrawZone)
            {
                // get the scaleFactor between the (inkSize + (drawDimensionSize - inkSize))
                // and  the actual inkSize.

                // TODO :: establish a new origin to be the Top Left of the Draw Rectangle
                double scaleFactorX = (drawZoneRect.Width / (inkCanvas.ActualWidth));
                double scaleFactorY = (drawZoneRect.Height / (inkCanvas.ActualHeight));

                pt.X = (pt.X * scaleFactorX) + drawZoneRect.Left;
                pt.Y = (pt.Y * scaleFactorY) + drawZoneRect.Top;
            }
          
            // Send the point to the robot too.
            // Leave the arm in its current down state.
            RobotArm.Move(pt);
        }

        private StylusPoint firstPoint;
        private void dispatcherTimerDotAnimation_Tick(object sender, EventArgs e)
        {
            if (_inTimer)
            {
                return;
            }

            _inTimer = true;

            // Have we created a new stroke for this animation yet?
            if (_strokeBeingAnimated == null)
            {
                // No, so create the first stroke and add th3 first dot to it.
                var firstPt = inkCanvas.Strokes[_currentAnimatedStrokeIndex].StylusPoints[0];
                firstPoint = firstPt;

                RobotArm.ArmDown(true);

                AddFirstPointToNewStroke(firstPt);
            }

            // Move to the next point along the stroke.
            ++_currentAnimatedPointIndex;

            // Have we reached the end of a stroke?
            if (_currentAnimatedPointIndex >=
                inkCanvas.Strokes[_currentAnimatedStrokeIndex].StylusPoints.Count)
            {
                // If the stroke is really short, we'll not ask the user to click the dot 
                // at both the start and end of the stroke. Instead once the dot is clicked 
                // at the start of the stroke, it will animate to the end of it, and then 
                // automatically move to the start of the next stroke.
                var shortStroke = (_currentAnimatedPointIndex < 3);

                // Should the dot automatically move to the start of the next stroke?
                if (_stampInProgress || shortStroke)
                {
                    // Yes, so the next animation will be at the start of a stroke.
                    _currentAnimatedPointIndex = 0;

                    // Move to the next stroke.
                    ++_currentAnimatedStrokeIndex;

                    // Do we have more strokes to write?
                    if (_currentAnimatedStrokeIndex < inkCanvas.Strokes.Count)
                    {
                        // Yes. So move along to the start of the next stroke.
                        MoveToNextStroke();

                        // If we've completed a short stroke, and are to wait for the user 
                        // to click the dot, make the dot opaque and wait for the click.
                        if (!_stampInProgress)
                        {
                            dot.Opacity = 1.0;

                            LiftArmAndStopAnimationTimer();
                        }
                    }
                    else
                    {
                        // We've reached the end of the last stroke.
                        _currentAnimatedStrokeIndex = 0;

                        // Hide the dot now that the entire signature's been written.
                        dot.Visibility = Visibility.Collapsed;

                        LiftArmAndStopAnimationTimer();

                        _dispatcherTimerDotAnimation = null;

                        _stampInProgress = false;
                    }
                }
                else
                {
                    // The dot is to wait at the end of the stroke until it's clicked. 
                    // So stop the animation timer.
                    LiftArmAndStopAnimationTimer();

                    // If we've not reached end of the last stroke, Show an opaque dot 
                    // to indicate that it's waiting to be clicked.
                    if (_currentAnimatedStrokeIndex < inkCanvas.Strokes.Count - 1)
                    {
                        dot.Opacity = 1.0;
                    }
                    else
                    {
                        // We've the end of the last stroke so hide the dot. 
                        dot.Visibility = Visibility.Collapsed;

                        _dispatcherTimerDotAnimation = null;
                    }
                }
            }
            else
            {
                // We're continuing to animate the stroke that we're were already on.
                var stylusPt = inkCanvas.Strokes[_currentAnimatedStrokeIndex].StylusPoints[_currentAnimatedPointIndex];
                var stylusPtPrevious = inkCanvas.Strokes[_currentAnimatedStrokeIndex].StylusPoints[_currentAnimatedPointIndex - 1];

                // Move to a point that's sufficiently far from the point that the dot's currently at.
                const int threshold = 1;

                while ((Math.Abs(stylusPt.X - stylusPtPrevious.X) < threshold) &&
                       (Math.Abs(stylusPt.Y - stylusPtPrevious.Y) < threshold))
                {
                    ++_currentAnimatedPointIndex;

                    if (_currentAnimatedPointIndex >= inkCanvas.Strokes[_currentAnimatedStrokeIndex].StylusPoints.Count)
                    {
                        break;
                    }

                    stylusPt = inkCanvas.Strokes[_currentAnimatedStrokeIndex].StylusPoints[_currentAnimatedPointIndex];
                }

                MoveDotAndRobotToStylusPoint(stylusPt);

                // Extend the ink stroke being drawn out to include the point where the dot is now.
                _strokeBeingAnimated?.StylusPoints.Add(stylusPt);
            }

            _inTimer = false;
        }

        private void LiftArmAndStopAnimationTimer()
        {
            _dispatcherTimerDotAnimation.Stop();

            RobotArm.ArmDown(false);
        }

        private void AddFirstPointToNewStroke(StylusPoint pt)
        {
            // Create a new stroke for the continuing animation.
            var ptCollection = new StylusPointCollection { pt };

            _strokeBeingAnimated = new Stroke(ptCollection);

            SetDrawingAttributesFromSettings(_strokeBeingAnimated.DrawingAttributes);

            inkCanvasAnimations.Strokes.Add(_strokeBeingAnimated);
        }

        private void MoveToNextStroke()
        {
            // Move to the next stroke.
            var stylusPtNext =
                inkCanvas.Strokes[_currentAnimatedStrokeIndex].StylusPoints[_currentAnimatedPointIndex];

            // We'll create the animation stroke after the first interval.
            _strokeBeingAnimated = null;

            // Lift the arm up before moving the dot to the start of the next stroke.
            RobotArm.ArmDown(false);

            MoveDotAndRobotToStylusPoint(stylusPtNext);
        }

        #endregion SendInkToRobotAndAnimateDot

        #region ButtonClickHandlers
        // When the Edit button is clicked, the user can ink directly in the app.
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            ResetWriting();

            //edit
            if (StampButton.Visibility == Visibility.Visible)
            {
                EditButton.ButtonText = "Done";

                StampButton.Visibility = Visibility.Collapsed;
                ClearButton.Visibility = Visibility.Visible;
                ImportButton.Visibility = Visibility.Visible;


                inkCanvas.IsEnabled = true;
            }
            else //done
            {
                EditButton.ButtonText = "Edit";
                LoadButton.ButtonText = "Load";

                SigBank.Visibility = Visibility.Collapsed;
                StampButton.Visibility = Visibility.Visible;
                ClearButton.Visibility = Visibility.Collapsed;
                ImportButton.Visibility = Visibility.Collapsed;
                backGroundd.ImageSource = null;
                inkCanvas.IsEnabled = false;

                // show current area.
                // also establishes  4-dot configuration scale for robot.
                // this.DrawAreaButton_Click(null, null);
            }

            WriteButton.Visibility = StampButton.Visibility;
            AreaButton.Visibility = StampButton.Visibility;

            SaveButton.Visibility = ClearButton.Visibility;
            LoadButton.Visibility = ClearButton.Visibility;
        }

        // Clear all ink from the app.
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.Strokes.Clear();
            inkCanvasAnimations.Strokes.Clear();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
            dialog.Title = "Please select an image file to import.";
            if (dialog.ShowDialog() == true)
            {
                backGroundd.ImageSource = new BitmapImage(new Uri(dialog.FileName));
            }
        }

        // Generate a file path for the saved signature.
        private string Generate_FilePath()
        {
            string fileName = System.IO.Directory.GetCurrentDirectory() + "\\sigBank\\ink\\" + DateTime.Now.ToString("dd_MMM_yyy_HH_mm") + ".isf";
            fileName.Replace(":", "_");
            fileName.Replace(",", "_");
            return fileName;
        }

        // Caputre the screen and save it as a jpeg in sigBank with same file path as fileDest
        private void CaptureScreen(string fileDest)
        {
            //get the virtual screen dimensions w/o left and right navigation grid columns
            double screenLeft = (1.4)*NavGrid.ActualWidth;
            double screenTop = SystemParameters.VirtualScreenTop + NavGrid.ActualWidth/2;
            double screenWidth = canvas.ActualWidth - (1.2)*settingsGrid.ActualWidth;
            double screenHeight = SystemParameters.VirtualScreenHeight - NavGrid.ActualWidth;

            using (System.Drawing.Bitmap bmap = new System.Drawing.Bitmap((int)screenWidth, (int)screenHeight))
            {
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmap))
                {
                    graphics.CopyFromScreen((int)screenLeft, (int)screenTop, 0, 0, bmap.Size);
                }

                //save jpeg of ink file
                string imgFilePath = fileDest.Replace("\\ink", "\\img");
                imgFilePath = imgFilePath.Replace(".isf", ".jpg");
                bmap.Save(imgFilePath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
        }

        private void Create_SaveFile_Directory()
        {
            string inkDir = System.IO.Directory.GetCurrentDirectory() + "\\sigBank\\ink";
            string imgDir = System.IO.Directory.GetCurrentDirectory() + "\\sigBank\\img";
            if (!Directory.Exists(inkDir))
            {
                Directory.CreateDirectory(inkDir);
            }
            if (!Directory.Exists(imgDir))
            {
                Directory.CreateDirectory(imgDir);
            }
        }

        // Save whatever ink is shown in the InkCanvas that the user can ink on, to an ISF file.
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            this.Create_SaveFile_Directory();  // makes sure sigBank directory exists 
            try
            {
                //save ink file
                string fileName = this.Generate_FilePath();
                var file = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                inkCanvas.Strokes.Save(file);
                file.Close();
        
                this.CaptureScreen(fileName);
                
                // This ink will be automatically loaded when the app next starts.
                Settings1.Default.LoadedInkLocation = fileName;
                Settings1.Default.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to save ink: " + ex.Message);
            }
        }

        //Exit out of load pop up
        private void SignatureButton_Click(object sender, RoutedEventArgs e)
        {
            LoadButton.Content = "Load";
            SigBank.Visibility = Visibility.Collapsed;
        }


        // Load up ink from an ISF file that the user selects from the OpenFileDialog.
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            SigBank.Children.Clear();
            if (SigBank.Visibility == Visibility.Collapsed){
                LoadButton.Content = "Close";
                
                // Read all signatures from ..//sigBank
                string sigBankImagePath = System.IO.Directory.GetCurrentDirectory() + "\\sigBank\\img";
                string[] sigImagePaths = Directory.GetFiles(sigBankImagePath);
                string[] recentSigImagePaths = new string[4];

                // Get array with last 4 entries of "sigImagePaths", associate these with the buttons somehow (TODO)
                int count = 0;
                while (count < sigImagePaths.Length && count < 4)
                {
                    recentSigImagePaths[count] = sigImagePaths[(sigImagePaths.Length-1) - count];
                    count++;
                }

                // Iterate over thumbnails files and convert to Image.
                // Then add Image to respective sigBank column & row.
                // NOTE:: if you have time you can dynamically allocate rows and columns for better UI.
                int row = 0;
                for (int i = 0; i < 4 && recentSigImagePaths[i] != null; i++)
                {
                    if (i == 2) { row++; } // go to next row for 3rd and 4th thumbnails

                    // create new image from file path
                    System.Windows.Controls.Image img = new System.Windows.Controls.Image();
                    BitmapImage src = new BitmapImage();
                    src.BeginInit();
                    src.UriSource = new Uri(recentSigImagePaths[i], UriKind.Relative);
                    src.CacheOption = BitmapCacheOption.OnLoad;
                    src.EndInit();
                    img.Source = src;
                    img.Height = Height / 3;  //set image size (consider programatically creating margins)
                    img.Width = Width / 3;
                    img.Stretch = Stretch.Uniform;

                    //replace special characters in path with octal rep. for btn name property
                    string name = recentSigImagePaths[i].Replace(":", "3A").Replace("\\", "5C").Replace(".jpg", "");

                    //create button to wrap image and bind Click event
                    System.Windows.Controls.Button btn = new System.Windows.Controls.Button();
                    btn.Name = name;
                    btn.Click += ThumbnailButton_Click;
                    btn.Content = img;
                    btn.SetValue(System.Windows.Controls.Grid.ColumnProperty, i%2); //assign row & column and row propeties 
                    btn.SetValue(System.Windows.Controls.Grid.RowProperty, row);
                    btn.MaxHeight = img.Height;
                    btn.MaxWidth = img.Width;
                    btn.Background = Brushes.Transparent;
                    btn.BorderBrush = Brushes.Transparent;                  

                    SigBank.Children.Add(btn);
                }

                SigBank.Visibility = Visibility.Visible;               
            }
            else
            {
                LoadButton.Content = "Load";
                SigBank.Visibility = Visibility.Collapsed;
            }

        }


        private void ThumbnailButton_Click(object sender, RoutedEventArgs e)
        {
            //convert sender to button; replace octal 
            System.Windows.Controls.Button btn_clicked = (System.Windows.Controls.Button)sender;
            string fileName = btn_clicked.Name; 
            fileName = fileName.Replace("3A", ":").Replace("5C", "\\").Replace("img","ink") + ".isf";


            AddInkFromFile(fileName);

            LoadButton.Content = "Load";
            SigBank.Visibility = Visibility.Collapsed;

            // This ink will be automatically loaded when the app next starts.
            Settings1.Default.LoadedInkLocation = fileName;
            Settings1.Default.Save();
        }



        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(this, _settings, RobotArm) { Owner = this };

            settingsWindow.ShowDialog();
        }

        private void Dot_OnClick(object sender, RoutedEventArgs e)
        {

            if (_dispatcherTimerDotAnimation != null)
            {
                // Only react to the click on the dot if the the timer's not currently running.
                // If the timer is running, then the dot's already being animated.
                if (!_dispatcherTimerDotAnimation.IsEnabled)
                {
                    // Are we at the end of a stroke?
                    if (_currentAnimatedPointIndex >=
                        inkCanvas.Strokes[_currentAnimatedStrokeIndex].StylusPoints.Count - 1)
                    {
                        // Make sure the robot arm is raised at the end of the stroke.
                        RobotArm.ArmDown(false);

                        // If this isn't the last stroke, move to the next stroke.
                        if (_currentAnimatedStrokeIndex < inkCanvas.Strokes.Count - 1)
                        {
                            // Move to the start of the next stroke.
                            _currentAnimatedPointIndex = 0;

                            ++_currentAnimatedStrokeIndex;

                            MoveToNextStroke();
                        }
                    }
                    else
                    {
                        // We're at the start of a stroke, so start animating the dot.
                        _dispatcherTimerDotAnimation.Start();

                        // Show a translucent dot while it's being animated. If a high contrast theme
                        // is active, keep the dot at 100% opacity to keep it high contrast against
                        // its background.
                        if (!SystemParameters.HighContrast)
                        {
                            dot.Opacity = 0.5;
                        }
                    }
                }
            }
        }


        private void ToggleDrawingAreaButtons(bool show)
        {
            if (show)
            {
                // set the Increase and Drecrease and Draw buttons visibility to visible
                this.IncreaseDrawingAreaButton.Visibility = Visibility.Visible;
                this.DecreaseDrawingAreaButton.Visibility = Visibility.Visible;
                this.DrawAreaButton.Visibility = Visibility.Visible;
                this.DoneDrawingAreaButton.Visibility = Visibility.Visible;
                this.areaText.Visibility = Visibility.Visible;

                // set all of the Drawing buttons visibility to collapsed
                this.StampButton.Visibility = Visibility.Collapsed;
                this.WriteButton.Visibility = Visibility.Collapsed;
                this.AreaButton.Visibility = Visibility.Collapsed;
                this.EditButton.Visibility = Visibility.Collapsed;

            }
            if (!show)
            {
                // set the Increase and Drecrease and Draw buttons visibility to collapsed
                this.IncreaseDrawingAreaButton.Visibility = Visibility.Collapsed;
                this.DecreaseDrawingAreaButton.Visibility = Visibility.Collapsed;
                this.DrawAreaButton.Visibility = Visibility.Collapsed;
                this.DoneDrawingAreaButton.Visibility = Visibility.Collapsed;
                this.areaText.Visibility = Visibility.Collapsed;

                // set all of the Drawing buttons visibility to visible
                this.StampButton.Visibility = Visibility.Visible;
                this.WriteButton.Visibility = Visibility.Visible;
                this.AreaButton.Visibility = Visibility.Visible;
                this.EditButton.Visibility = Visibility.Visible;
            }

        }


        private void AreaButton_Click(object sender, RoutedEventArgs e)
        {
            CircularGazeButton btn = (CircularGazeButton)sender;

            if (btn.ButtonText == "Area")
            {
                this.ToggleDrawingAreaButtons(true);
            }
            else
            {
                this.ToggleDrawingAreaButtons(false);
            }
        }


        private void RobotPlaceDot()
        {
            RobotArm.ArmDown(true);
            RobotArm.ArmDown(false);
        }

        private void MoveRobotToShowDrawZone(StylusPoint[] edgePoints)
        {
            if (edgePoints.Length >= 4)
            {
                MoveDotAndRobotToStylusPoint(edgePoints[0]);  // dot top-left
                RobotPlaceDot();

                MoveDotAndRobotToStylusPoint(edgePoints[1]);  // dot bottom-left
                RobotPlaceDot();

                MoveDotAndRobotToStylusPoint(edgePoints[2]);  // dot bottom-right
                RobotPlaceDot();

                MoveDotAndRobotToStylusPoint(edgePoints[3]);  // dot top-right
                RobotPlaceDot();

                MoveDotAndRobotToStylusPoint(edgePoints[0]);  // move back to start
            }
        }


        bool isShowingDrawZone = false;
        private void DrawAreaButton_Click(object sender, RoutedEventArgs e)
        {
            StylusPoint[] edgePoints = new StylusPoint[4];

            // Set index 0 as the starting top-left corner 
            edgePoints[0].Y = drawZoneRect.Top;
            edgePoints[0].X = drawZoneRect.Left;

            // Set index 1 as the starting bottom-left corner 
            edgePoints[1].Y = drawZoneRect.Bottom;
            edgePoints[1].X = targetArea == 0   // the code below is added adjust the bottom left dot
                ? drawZoneRect.Left + 30
                : targetArea == -1
                ? drawZoneRect.Left + 15
                : targetArea == -2
                ? drawZoneRect.Left + 10
                : drawZoneRect.Left;

            // Set index 2 as the starting bottom-right corner 
            edgePoints[2].Y = drawZoneRect.Bottom;
            edgePoints[2].X = drawZoneRect.Right;

            // Set index 3 as the starting top-right corner 
            edgePoints[3].Y = drawZoneRect.Top;
            edgePoints[3].X = drawZoneRect.Right;

            // flag is true so we dont apply scaling factor
            isShowingDrawZone = true;
            MoveRobotToShowDrawZone(edgePoints);
            isShowingDrawZone = false;
        }


        private double targetArea = 0.0;
        private void AdjustDrawingAreaButton_Click(object sender, RoutedEventArgs e)
        {
            CircularGazeButton btn = (CircularGazeButton)sender;

            // logic to scale the strokes on the inkCanvas by 0.5
            if (btn.ButtonText == "-" && targetArea >= -2)
            {
                targetArea -= 1;
            }
            // logic to scale the strokes on the inkCanvas by 0.5
            else if (btn.ButtonText == "+" && targetArea < 0)
            {
                targetArea += 1;
 
            }

            this.SetDrawingZoneRectangle(targetArea);
        }


        Rect drawZoneRect = new Rect();
        private void SetDrawingZoneRectangle(double targetArea)
        {
            switch (targetArea)
            {
                case 0:
                    drawZoneRect.Height = 685;  // 6in. X 8in.
                    drawZoneRect.Width = 825;
                    drawZoneRect.Location = new Point
                    {
                        X = 100,
                        Y = 50,
                    };
                    this.areaText.Text = "8'' X 6''";
                    this.mainWindowBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 254, 212, 42));
                    break;
                case -1:
                    drawZoneRect.Height = 450;  // 4in. X 6in.
                    drawZoneRect.Width = 620;
                    drawZoneRect.Location = new Point
                    {
                        X = 200,
                        Y = 150,
                    };
                    this.areaText.Text = "6'' X 4''";
                    this.mainWindowBorder.BorderBrush = Brushes.CadetBlue;
                    break;
                case -2:
                    drawZoneRect.Height = 225;  // 4in. X 2in.
                    drawZoneRect.Width = 415;
                    drawZoneRect.Location = new Point
                    {
                        X = 300,
                        Y = 250,
                    };
                    this.areaText.Text = "4'' X 2''";
                    this.mainWindowBorder.BorderBrush = Brushes.Gray;
                    break;
                case -3:
                    drawZoneRect.Height = 100;  // 2in. X 1in.
                    drawZoneRect.Width = 200;
                    drawZoneRect.Location = new Point
                    {
                        X = 400,
                        Y = 315,
                    };
                    this.areaText.Text = "2'' X 1''";
                    this.mainWindowBorder.BorderBrush = Brushes.Crimson;
                    break;
                default:
                    break;
            }

        }

        #endregion ButtonClickHandlers

        #region RobotControl
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.D: // arm down
                    RobotArm.ArmDown(true);
                    break;
                case Key.U: // arm up
                    RobotArm.ArmDown(false);
                    break;
                case Key.H: // arm up
                    RobotArm.Home();
                    break;
                case Key.C:
                    RobotArm.CircleTest();
                    break;
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            RobotArm.ZShift += Math.Sign(e.Delta) * 0.01;
        }

        #endregion RobotControl
    }

    #region ValueConverters

    public class ArmConnectedToContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var armConnected = (bool)value;

            return "\uE99A" + (armConnected ? "\uE10B" : "\uE10A");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ArmConnectedToHelpTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var armConnected = (bool)value;

            return "Robot " + (armConnected ? "connected" : "disconnected");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ArmStateToDotFillConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var armDown = (bool)value;

            var color = armDown ? Settings1.Default.DotDownColor : Settings1.Default.DotColor;

            return new SolidColorBrush(Color.FromArgb(
                color.A, color.R, color.G, color.B));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ArmStateToDotWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var armDown = (bool)value;

            return (armDown ? Settings1.Default.DotDownWidth : Settings1.Default.DotWidth);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion ValueConverters
}