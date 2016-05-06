using System;
using System.Windows;
using System.Windows.Media;
using System.Drawing.Drawing2D;
using Microsoft.Kinect;
using System.Windows.Shapes;

namespace PantomimeDemo
{
    public partial class MainWindow : Window
    {
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
                if(joint.JointType != JointType.Head)
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
        /// Process game state: Exercising
        /// Shows skeleton and joint data
        /// </summary>
        /// <param name="skeletons"></param>
        private void ProcessPlayerExercising(Skeleton skel, Exercise exercise)
        {
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
                Angle MyAngles = new Angle();
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
                        drawFeedback( dc,skel,Exercise.BicepCurl,ReadyAngles);
                        if (ReadyAngles[0] < _AngExEnd && ReadyAngles[2] < _AngExEnd)
                        {
                            _ExState = true;
                        }
                        if (ReadyAngles[0] > _AngStart && ReadyAngles[2] > _AngStart && _ExState == true)
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

        private void drawFeedback(DrawingContext dc,Skeleton skel,Exercise ex, byte[] ReadyAngles)
        {
            if (ex == Exercise.BicepCurl)
            {
                if (ReadyAngles[1] > 40)
                {
                    Point a1 = this.SkeletonPointToScreen(skel.Joints[JointType.ElbowRight].Position);// new Point(skel.Joints[JointType.ShoulderRight].Position.X, skel.Joints[JointType.ElbowRight].Position.Y);

                    double X1 = a1.X - 40;
                    double Y1 = a1.Y+10;

                    double X2 = a1.X - 70;
                    double Y2 = a1.Y + 30;
                    DrawLinkArrow(dc, new Point(X1, Y1), new Point(X2, Y2));
                    
                }
                if (ReadyAngles[3] > 40)
                {
                    Point a1 = this.SkeletonPointToScreen(skel.Joints[JointType.ElbowLeft].Position);// new Point(skel.Joints[JointType.ShoulderRight].Position.X, skel.Joints[JointType.ElbowRight].Position.Y);

                    double X1 = a1.X + 40;
                    double Y1 = a1.Y + 10;

                    double X2 = a1.X + 70;
                    double Y2 = a1.Y + 30;
                    DrawLinkArrow(dc, new Point(X1, Y1), new Point(X2, Y2));


                }

            }
        }

        private void DrawLinkArrow(DrawingContext dc, Point p1, Point p2)
        {
            GeometryGroup lineGroup = new GeometryGroup();
            double theta = Math.Atan2((p2.Y - p1.Y), (p2.X - p1.X)) * 180 / Math.PI;

            PathGeometry pathGeometry = new PathGeometry();
            PathFigure pathFigure = new PathFigure();
            Point p = new Point(p1.X + ((p2.X - p1.X) / 1.35), p1.Y + ((p2.Y - p1.Y) / 1.35));
            pathFigure.StartPoint = p;

            Point lpoint = new Point(p.X+6, p.Y+15 );//+6,+15
            Point rpoint = new Point(p.X -6, p.Y+15 );//-6,15
            LineSegment seg1 = new LineSegment();
            seg1.Point = lpoint;
            pathFigure.Segments.Add(seg1);

            LineSegment seg2 = new LineSegment();
            seg2.Point = rpoint;
            pathFigure.Segments.Add(seg2);

            LineSegment seg3 = new LineSegment();
            seg3.Point = p;
            pathFigure.Segments.Add(seg3);

            pathGeometry.Figures.Add(pathFigure);
            RotateTransform transform = new RotateTransform();
            transform.Angle = theta + 90;
            transform.CenterX = p.X;
            transform.CenterY = p.Y;
            pathGeometry.Transform = transform;
            lineGroup.Children.Add(pathGeometry);

            LineGeometry connectorGeometry = new LineGeometry();
            connectorGeometry.StartPoint = p1;
            connectorGeometry.EndPoint = p2;
            lineGroup.Children.Add(connectorGeometry);

            dc.DrawGeometry(Brushes.Black, new Pen(Brushes.Red,3), lineGroup);


            //System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
            //path.Data = lineGroup;
            //path.StrokeThickness = 10;
            //path.Stroke = path.Fill = Brushes.Black;

       //     return path;
        }



    }




}
