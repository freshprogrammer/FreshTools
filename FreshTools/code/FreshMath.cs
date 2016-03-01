using System;
using System.Drawing;

namespace FreshTools
{
    class FreshMath
    {
        public static double DegreesToRadians(double angleInDegrees)
        {
            return angleInDegrees * Math.PI / 180;
        }

        public static double RadiansToDegrees(double angleInRadians)
        {
            return angleInRadians * 180 / Math.PI;
        }

        /// <summary>
        /// Calculates angle from Point 1, to Point 2. Result returned in degrees
        /// </summary>
        /// <param name="x1">X cord of Point 1</param>
        /// <param name="y1">Y cord of Point 1</param>
        /// <param name="x2">X cord of Point 2</param>
        /// <param name="y2">Y cord of Point 2</param>
        /// <returns>Resulting angle in degrees</returns>
        public static float CalcHeading(float x1, float y1, float x2, float y2, bool degrees, bool cartesian)
        {
            if (x1 == x2 && y1 == y2)
                return 0;

            float xDist = x2 - x1;
            float yDist = y2 - y1;

            float xDif = Math.Abs(xDist);
            float yDif = Math.Abs(yDist);

            float result;
            if (degrees)
                result = (float)(Math.Atan(yDif / xDif) * 180 / Math.PI);
            else
                result = (float)Math.Atan(yDif / xDif);

            if (cartesian)
                result = ConvertAngleToCartersian(xDist, yDist, result, degrees);

            return result;
        }

        /// <summary>
        /// Converts angle where 0=Up and counter-clockwise to 0=Right and  clockwise.
        /// </summary>
        /// <param name="xDelta">xDir</param>
        /// <param name="yDelta">yDir</param>
        /// <param name="angle">current Angle</param>
        /// <param name="degrees">True for degrees, false for radians</param>
        /// <returns>new anlge</returns>
        public static float ConvertAngleToCartersian(float xDelta, float yDelta, float angle, bool degrees)
        {
            double quarterTurn;
            if (degrees)
                quarterTurn = 90;
            else
                quarterTurn = Math.PI / 2;

            double result = angle;
            if (yDelta < 0)
            {
                if (xDelta < 0)
                {
                    //System.out.println("Q3");
                    //quadrent 3
                    result += quarterTurn * 3;
                }
                else
                {
                    //System.out.println("Q4");
                    //quadrent 4

                    result = quarterTurn - angle;
                    //perfect already
                }
            }
            else
            {
                if (xDelta < 0)
                {
                    //System.out.println("Q2");
                    //quadrent 2
                    result = quarterTurn - angle;
                    result += quarterTurn * 2;
                }
                else
                {
                    //System.out.println("Q1");
                    //quadrent 1
                    result += quarterTurn * 1;
                }
            }
            return (float)result;
        }

        public static double Distance(Point a, Point b)
        {
            return Distance(a.X, a.Y, b.X, b.Y);
        }

        public static double Distance(double x1, double y1, double x2, double y2)
        {
            double a = x2 - x1;
            double b = y2 - y1;
            return Math.Sqrt(a * a + b * b);
        }
    }
}
