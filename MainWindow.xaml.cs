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

    public class Angles
    {
        public double AngleBetweenTwoVectors(Vector3D vectorA, Vector3D vectorB)
        {
            double dotProduct;
            vectorA.Normalize();
            vectorB.Normalize();
            dotProduct = Vector3D.DotProduct(vectorA, vectorB);

            return (double)Math.Acos(dotProduct) / Math.PI * 180;
        }

        public byte[] GetVector(Skeleton skeleton)
        {
            Vector3D ShoulderCenter = new Vector3D(skeleton.Joints[JointType.ShoulderCenter].Position.X, skeleton.Joints[JointType.ShoulderCenter].Position.Y, skeleton.Joints[JointType.ShoulderCenter].Position.Z);
            Vector3D RightShoulder = new Vector3D(skeleton.Joints[JointType.ShoulderRight].Position.X, skeleton.Joints[JointType.ShoulderRight].Position.Y, skeleton.Joints[JointType.ShoulderRight].Position.Z);
            Vector3D LeftShoulder = new Vector3D(skeleton.Joints[JointType.ShoulderLeft].Position.X, skeleton.Joints[JointType.ShoulderLeft].Position.Y, skeleton.Joints[JointType.ShoulderLeft].Position.Z);
            Vector3D RightElbow = new Vector3D(skeleton.Joints[JointType.ElbowRight].Position.X, skeleton.Joints[JointType.ElbowRight].Position.Y, skeleton.Joints[JointType.ElbowRight].Position.Z);
            Vector3D LeftElbow = new Vector3D(skeleton.Joints[JointType.ElbowLeft].Position.X, skeleton.Joints[JointType.ElbowLeft].Position.Y, skeleton.Joints[JointType.ElbowLeft].Position.Z);
            Vector3D RightWrist = new Vector3D(skeleton.Joints[JointType.WristRight].Position.X, skeleton.Joints[JointType.WristRight].Position.Y, skeleton.Joints[JointType.WristRight].Position.Z);
            Vector3D LeftWrist = new Vector3D(skeleton.Joints[JointType.WristLeft].Position.X, skeleton.Joints[JointType.WristLeft].Position.Y, skeleton.Joints[JointType.WristLeft].Position.Z);
            Vector3D UpVector = new Vector3D(0.0, 1.0, 0.0);

            double AngleRightElbow = AngleBetweenTwoVectors(RightElbow - RightShoulder, RightElbow - RightWrist);
            double AngleRightShoulder = AngleBetweenTwoVectors(UpVector, RightShoulder - RightElbow);
            double AngleLeftElbow = AngleBetweenTwoVectors(LeftElbow - LeftShoulder, LeftElbow - LeftWrist);
            double AngleLeftShoulder = AngleBetweenTwoVectors(UpVector, LeftShoulder - LeftElbow);


            byte[] Angles = { Convert.ToByte(AngleRightElbow), Convert.ToByte(AngleRightShoulder), Convert.ToByte(AngleLeftElbow), Convert.ToByte(AngleLeftShoulder) };
            return Angles;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
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
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext, Byte[] ReadyAngles)
        {
            // Render Torso
           // this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    Brush jointBrush = null;
                    if (ReadyAngles[0] < 50)
                    {
                        jointBrush = Brushes.LawnGreen;
                    }
                    else
                    {
                        jointBrush = Brushes.Red;
                    }
                    FormattedText RE = new FormattedText(ReadyAngles[0].ToString(), System.Globalization.CultureInfo.GetCultureInfo("en-us"), System.Windows.FlowDirection.LeftToRight, new Typeface("Tahoma"), 20, jointBrush);
                    FormattedText LE = new FormattedText(ReadyAngles[2].ToString(), System.Globalization.CultureInfo.GetCultureInfo("en-us"), System.Windows.FlowDirection.LeftToRight, new Typeface("Tahoma"), 20, jointBrush);
                    byte[] SequenceStart = { 255 };
                    if (joint.JointType == JointType.ElbowRight)
                    {
                        drawingContext.DrawText(RE, this.SkeletonPointToScreen(joint.Position));
                    }
                    else if (joint.JointType == JointType.ElbowLeft)
                    {
                        drawingContext.DrawText(LE, this.SkeletonPointToScreen(joint.Position));
                    }
                    else
                    {
                        drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                    }
                }
            }
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
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
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
            HandElement.Visibility = Visibility.Visible;
            BicepCurlBox.Visibility = Visibility.Hidden;
            LateralRaiseBox.Visibility = Visibility.Hidden;
            VidFeed.Visibility = Visibility.Hidden;
            DisplayArea.Visibility = Visibility.Hidden;
            ScanBarcode.Visibility = Visibility;
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
            HandElement.Visibility = Visibility.Visible;
            BicepCurlBox.Visibility = Visibility.Visible;
            LateralRaiseBox.Visibility = Visibility.Visible;
            ScanBarcode.Visibility = Visibility.Hidden;
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

        /// <summary>
        /// Process game state: Exercising
        /// Shows skeleton and joint data
        /// </summary>
        /// <param name="skeletons"></param>
        private void ProcessPlayerExercising(Skeleton skel, Exercise exercise)
        {
            HandElement.Visibility = Visibility.Collapsed;
            BicepCurlBox.Visibility = Visibility.Hidden;
            LateralRaiseBox.Visibility = Visibility.Hidden;
            ScanBarcode.Visibility = Visibility.Hidden;

            // Write the pixel data into our bitmap
            this.vidSource.WritePixels(
                new Int32Rect(0, 0, this.vidSource.PixelWidth, this.vidSource.PixelHeight),
                this.colorPixels,
                this.vidSource.PixelWidth * sizeof(int),
                0);

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));
                           
                RenderClippedEdges(skel, dc);
                Angles MyAngles = new Angles();
                byte[] ReadyAngles = MyAngles.GetVector(skel);

                if (skel.TrackingState == SkeletonTrackingState.Tracked)
                {
                    this.DrawBonesAndJoints(skel, dc, ReadyAngles);
                }
                else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                {
                    dc.DrawEllipse(
                    this.centerPointBrush,
                    null,
                    this.SkeletonPointToScreen(skel.Position),
                    BodyCenterThickness,
                    BodyCenterThickness);
                }
                switch (exercise)
                {
                    case Exercise.BicepCurl:
                        if(ReadyAngles[0] < _AngExEnd && ReadyAngles[2] < _AngExEnd)
                        {
                            _ExState = true;
                        }
                        if(ReadyAngles[0] > _AngStart && ReadyAngles[2] > _AngStart && _ExState == true)
                        {
                            _ExState = false;
                            _CurrentRep += 1;                          
                            RepCountBox.Text = _CurrentRep.ToString();
                        }
                        break;

                    case Exercise.LateralRaise:
                        break;
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));  
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
                        BicepCurlBox.Visibility = Visibility.Hidden;
                        LateralRaiseBox.Visibility = Visibility.Hidden;
                        HandElement.Visibility = Visibility.Visible;
                        VidFeed.Visibility = Visibility.Hidden;
                        DisplayArea.Visibility = Visibility.Hidden;
                        LaunchDashboard.Visibility = Visibility.Hidden;
                        SetCountBox.Visibility = Visibility.Hidden;
                        SetCountLabel.Visibility = Visibility.Hidden;
                        RepCountBox.Visibility = Visibility.Hidden;
                        RepCountLabel.Visibility = Visibility.Hidden;
                        FinishWorkout.Visibility = Visibility.Hidden;
                        break;

                    case GamePhase.StartScreen:
                        this._CurrentRep = 0;
                        this._CurrentSet = 0;
                        ScanBarcode.Visibility = Visibility.Hidden;
                        BicepCurlBox.Visibility = Visibility.Visible;
                        LateralRaiseBox.Visibility = Visibility.Visible;
                        LaunchDashboard.Visibility = Visibility.Visible;
                        HandElement.Visibility = Visibility.Visible;
                        VidFeed.Visibility = Visibility.Hidden;
                        DisplayArea.Visibility = Visibility.Hidden;
                        SetCountBox.Visibility = Visibility.Hidden;
                        SetCountLabel.Visibility = Visibility.Hidden;
                        RepCountBox.Visibility = Visibility.Hidden;
                        RepCountLabel.Visibility = Visibility.Hidden;
                        FinishWorkout.Visibility = Visibility.Hidden;
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
                        VidFeed.Visibility = Visibility.Visible;
                        DisplayArea.Visibility = Visibility.Visible;
                        ScanBarcode.Visibility = Visibility.Hidden;
                        HandElement.Visibility = Visibility.Hidden;
                        BicepCurlBox.Visibility = Visibility.Hidden;
                        LateralRaiseBox.Visibility = Visibility.Hidden;
                        LaunchDashboard.Visibility = Visibility.Hidden;
                        SetCountBox.Visibility = Visibility.Visible;
                        SetCountLabel.Visibility = Visibility.Visible;
                        RepCountBox.Visibility = Visibility.Visible;
                        RepCountLabel.Visibility = Visibility.Visible;
                        FinishWorkout.Visibility = Visibility.Visible;
                        break;
                }
            }
        }


    }
}