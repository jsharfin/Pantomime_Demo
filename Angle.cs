using System;

using System.Windows.Media.Media3D;
using Microsoft.Kinect;

namespace PantomimeDemo
{

    public class Angle
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
}
