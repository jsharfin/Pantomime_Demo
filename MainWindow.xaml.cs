//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace PantomimeDemo
{
    using System;
    using System.IO;
    using System.Drawing.Imaging;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Media3D;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    //using PQScan.BarcodeScanner;

    public enum GamePhase
    {
        LogIn =0,
        StartScreen = 1,
        Instrucitons = 2,
        Exercise = 3,
        Summary = 4
    }

    public enum Exercise
    {
        BicepCurl = 0,
        LateralRaise = 1
    }



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        private GamePhase _CurrentPhase;
        private Exercise _CurrentExercise;
        private int _CurrentSet;
        private int _CurrentRep;
        private int _ColorImageStride;
        private byte _AngStart;
        private byte _AngExEnd;
        private Boolean _ExState;
        private int dashCount;

        /// <summary>
        /// Reserved for 
        /// </summary>
        private int _InstructionPosition;
        private UIElement[] _InstructionSequence;

        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;
        private WriteableBitmap vidSource;
        private byte[] colorPixels;
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            // _CurrentPhase = GamePhase.LogIn;
            _CurrentPhase = GamePhase.LogIn;
            _CurrentRep = 0;
            _CurrentSet = 0;
            _ExState = false;
            _AngExEnd = 55;
            _AngStart = 150;
            dashCount = 0;
            VidFeed.Visibility = Visibility.Hidden;
            DisplayArea.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);
            

            // Display the drawing using our image control
            DisplayArea.Source = this.imageSource;
            VidFeed.Source = this.vidSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {

                // Set up smoothing parameters to reduce jitter effects
                TransformSmoothParameters smoothingParam = new TransformSmoothParameters();
                {
                    smoothingParam.Smoothing = 0.5f;
                    smoothingParam.Correction = 0.5f;
                    smoothingParam.Prediction = 0.5f;
                    smoothingParam.JitterRadius = 0.05f;
                    smoothingParam.MaxDeviationRadius = 0.04f;
                };

                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable(smoothingParam);

                // Turn on the depth stream
                this.sensor.DepthStream.Enable();

                //Turn on the video stream to receive video frames
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.vidSource = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                VidFeed.Source = vidSource;

                //Add an event handler to be called whenever there is new color frame data
                this.sensor.AllFramesReady += this.SensorAllFramesReady;

                _ColorImageStride = sensor.ColorStream.FrameWidth * sensor.ColorStream.FrameBytesPerPixel;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
            Application.Current.Shutdown();
        }


        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];
            Skeleton primeSkeleton = null;
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(colorPixels);
                }
            }
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                    primeSkeleton = GetPrimarySkeleton(skeletons);
                }
            }

            if(primeSkeleton == null)
            {
                //ChangePhase(GamePhase.LogIn);
                ChangePhase(GamePhase.LogIn);
            }

            else
                {

                    TrackHand(primeSkeleton.Joints[JointType.HandLeft], HandElement, layoutGrid);

                    switch (this._CurrentPhase)
                    {
                        case GamePhase.LogIn:
                            ProcessLogIn(primeSkeleton);
                            break;

                        case GamePhase.StartScreen:
                            ProcessStartScreen(primeSkeleton);
                            break;

                        case GamePhase.Exercise:
                            ProcessPlayerExercising(primeSkeleton, _CurrentExercise);
                              if (GetHitTarget(primeSkeleton.Joints[JointType.HandLeft], FinishWorkout) != null)
                           {

                            ChangePhase(GamePhase.StartScreen);
                            ProcessStartScreen(primeSkeleton);
                          }
                           break;
                    }

                  } 
          
        }



        /// <summary>
        /// Takes in a skeleton array and returns the skeleton closest to the kinect
        /// </summary>
        /// <param name="skeletons"></param>
        /// <returns></returns>
        private static Skeleton GetPrimarySkeleton(Skeleton[] skeletons)
        {
            Skeleton skeleton = null;

            if (skeletons != null)
            {
                //Find closest skeleton
                for (int i = 0; i < skeletons.Length; i++)
                {
                    if (skeletons[i].TrackingState == SkeletonTrackingState.Tracked)
                    {
                        if (skeleton == null)
                        {
                            skeleton = skeletons[i];
                        }
                        else
                        {
                            if (skeleton.Position.Z > skeletons[i].Position.Z)
                            {
                                skeleton = skeletons[i];
                            }
                        }
                    }
                }
            }
            return skeleton;
        }

   
        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

      

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

        /// <summary>
        /// Handles the LogIn phase of the came (scanning barcode to start)
        /// -----------------FINISH THIS---------------------------------
        /// </summary>
        /// <param name="skeleton"></param>
        private void ProcessLogIn(Skeleton skeleton)
        {
           
            _CurrentRep = 0;
            
            if (GetHitTarget(skeleton.Joints[JointType.HandLeft],ScanBarcode) != null)
            {
                string fileName = "barcode.png";

                if(File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                using(FileStream savedSnapshot = new FileStream(fileName, FileMode.CreateNew))
                {
                    BitmapSource image = (BitmapSource)VidFeed.Source;

                    JpegBitmapEncoder jpgEncoder = new JpegBitmapEncoder();
                    jpgEncoder.QualityLevel = 70;
                    jpgEncoder.Frames.Add(BitmapFrame.Create(image));
                    jpgEncoder.Save(savedSnapshot);

                    savedSnapshot.Flush();
                    savedSnapshot.Close();
                    savedSnapshot.Dispose();
                }

                if (File.Exists("barcode.png"))
                {
                    File.Delete("barcode.png");
                    ChangePhase(GamePhase.StartScreen);
                }
            }
        }

        /// <summary> 
        /// Handles the StartScreen phase of the game 
        ///  -----NEED TO SOMEHOW EXPAND SKELETON AREA TO WHOLE SCREEN
        /// </summary>
        private void ProcessStartScreen(Skeleton skeleton)
        {

            _CurrentRep = 0;
            //Determine if the user triggers the start of a new game
            if (GetHitTarget(skeleton.Joints[JointType.HandLeft], BicepCurlBox) != null) //|| GetHitTarget(skeleton.Joints[JointType.HandRight], BicepCurlBox) != null)
            {
               ChangePhase(GamePhase.Exercise);
                _CurrentExercise = Exercise.BicepCurl;
                _CurrentPhase = GamePhase.Exercise;
                ProcessPlayerExercising(skeleton, _CurrentExercise);

            }

            else if (GetHitTarget(skeleton.Joints[JointType.HandLeft], LateralRaiseBox) != null) //|| GetHitTarget(skeleton.Joints[JointType.HandRight], LateralRaiseBox) != null)
            {
                ChangePhase(GamePhase.Exercise);
                _CurrentExercise = Exercise.LateralRaise;
                _CurrentPhase = GamePhase.Exercise;
                ProcessPlayerExercising(skeleton, _CurrentExercise);
            }

            else if((GetHitTarget(skeleton.Joints[JointType.HandLeft], LaunchDashboard) != null) && dashCount < 1)
            {
                System.Diagnostics.Process.Start("http://danielfeusse.com/Cornell-StartupStudio-Client/testapp/daypage.html");
                dashCount++;
                Close();
            }
        }

        private IInputElement GetHitTarget(Joint joint, UIElement target)
        {
            DepthImagePoint point = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position, DepthImageFormat.Resolution640x480Fps30);

            point.X = (int)(point.X * layoutGrid.ActualWidth / sensor.DepthStream.FrameWidth);

            point.Y = (int)(point.Y * layoutGrid.ActualHeight / sensor.DepthStream.FrameHeight);

            Point targetPoint = new Point(point.X, point.Y);
            //Point targetPoint = GetJointPoint(joint, layoutGrid);
            targetPoint = layoutGrid.TranslatePoint(targetPoint, target);
            return target.InputHitTest(targetPoint);
        }

        /// <summary>
        /// maps point in depth stream to grid coordinate system
        /// </summary>
        /// <param name="joint"></param>
        /// <param name="grid"></param>
        /// <returns></returns>
        private Point GetJointPoint(Joint joint, Grid grid)
        {
            DepthImagePoint point = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position, DepthImageFormat.Resolution640x480Fps30);
            point.X *= (int)grid.ActualWidth / sensor.DepthStream.FrameWidth;
            point.Y *= (int)grid.ActualHeight / sensor.DepthStream.FrameHeight;

            return new Point(point.X, point.Y);
        }

        /// <summary>
        /// Trackes the position of the users hand on the screen with a cursor icon
        /// </summary>
        /// <param name="hand"></param>
        /// <param name="handCursorElement"></param>
        /// <param name="grid"></param>
        private void TrackHand(Joint hand, Image handCursorElement, Grid grid)
        {
            if (hand.TrackingState == JointTrackingState.NotTracked)
            {
                handCursorElement.Visibility = Visibility.Collapsed;
            }
            else
            {
                handCursorElement.Visibility = Visibility.Visible;

                float x;
                float y;

                DepthImagePoint point = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(hand.Position, DepthImageFormat.Resolution640x480Fps30);

               point.X = (int)((point.X * grid.ActualWidth / sensor.DepthStream.FrameWidth) - (handCursorElement.ActualWidth / 2.0));

                point.Y = (int)((point.Y * grid.ActualHeight / sensor.DepthStream.FrameHeight) - (handCursorElement.ActualHeight / 2.0));

                x = point.X;
                y = point.Y;

              
                
                Canvas.SetLeft(handCursorElement, x);
                Canvas.SetTop(handCursorElement, y);
            }
        }

     

        private void ChangePhase(GamePhase newPhase)
        {
            if (newPhase != this._CurrentPhase)
            {
                this._CurrentPhase = newPhase;

                switch (this._CurrentPhase)
                {
                    case GamePhase.LogIn:
                        ScanBarcode.Visibility = Visibility.Visible;
                        MenuCanvas.Visibility = Visibility.Collapsed;
                        WorkoutCanvas.Visibility = Visibility.Collapsed;
                        HandElement.Visibility = Visibility.Visible;
                        break;

                    case GamePhase.StartScreen:
                        this._CurrentRep = 0;
                        this._CurrentSet = 0;
                        
                        ScanBarcode.Visibility = Visibility.Hidden;
                        MenuCanvas.Visibility = Visibility.Visible;
                        WorkoutCanvas.Visibility = Visibility.Collapsed;
                        HandElement.Visibility = Visibility.Visible;
                        
                        break;

                    case GamePhase.Instrucitons:
                        /*  this._CurrentLevel++;
                          GameStateElement.Text = string.Format("Level {0}", this._CurrentLevel);
                          ControlCanvas.Visibility = Visibility.Collapsed;
                          GameInstructionsElement.Text = "Watch for Simon's instructions";
                          GenerateInstructions();
                          DisplayInstructions();
                         */
                        break;

                    case GamePhase.Exercise:
                        this._InstructionPosition = 0;
                        ScanBarcode.Visibility = Visibility.Hidden;
                        HandElement.Visibility = Visibility.Visible;
                        MenuCanvas.Visibility = Visibility.Collapsed;
                        VidFeed.Visibility = Visibility.Visible;
                        DisplayArea.Visibility = Visibility.Visible;

                        WorkoutCanvas.Visibility = Visibility.Visible;
                        break;
                }
            }
        }


    }
}