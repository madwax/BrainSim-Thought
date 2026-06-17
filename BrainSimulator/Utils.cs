/*
 * Brain Simulator Thought
 *
 * Copyright (c) 2026 Charles Simon
 *
 * This file is part of Brain Simulator Thought and is licensed under
 * the MIT License. You may use, copy, modify, merge, publish, distribute,
 * sublicense, and/or sell copies of this software under the terms of
 * the MIT License.
 *
 * See the LICENSE file in the project root for full license information.
 */
 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using BrainSimulator.Modules;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using static System.Math;


namespace BrainSimulator
{
    public static class IReadOnlyListExtensions
    {
        public static T FindFirst<T>(this IReadOnlyList<T> source, Func<T, bool> condition)
        {
            foreach (T item in source)
                if (condition(item))
                    return item;
            return default(T);
        }
        public static List<T> FindAll<T>(this IReadOnlyList<T> source, Func<T, bool> condition)
        {
            List<T> theList = new List<T>();
            if (source is null) return theList;
            foreach (T item in source)
                if (condition(item))
                    theList.Add(item);
            return theList;
        }
    }

    //This is not used
    class Range
    {
        float minX;
        float minY;
        float maxX;
        float maxY;
        public Range(Point loc, Angle angle, float length)
        {
            minX = (float)loc.X;
            minY = (float)loc.Y;
            maxX = minX + (float)Cos(angle) * length;
            maxY = minY + (float)Sin(angle) * length;
            if (minX > maxX)
            {
                float temp = minX;
                minX = maxX;
                maxX = temp;
            }
            if (minY > maxY)
            {
                float temp = minY;
                minY = maxY;
                maxY = temp;
            }
            //                minX -= 1; maxX += 1; minY -= 1; maxY += 1;
        }
        public bool Overlaps(Range r2, float minOverlap = 0)
        {
            if (r2.minX > maxX + minOverlap) return false;
            if (r2.minY > maxY + minOverlap) return false;
            if (r2.maxX < minX - minOverlap) return false;
            if (r2.maxY < minY - minOverlap) return false;
            return true;
        }
    }

    public class HSLColor
    {
        public float hue; //[0359]
        public float saturation; //[0,1]
        public float luminance;//[0,1]
        public HSLColor() { }
        public HSLColor(float h, float s, float l)
        {
            hue = h;
            saturation = s;
            luminance = l;
        }
        public HSLColor(Dictionary<string, float> values)
        {
            hue = 0;
            saturation = 0;
            luminance = 0;
            try
            {
                hue = values["Hue+"];
                saturation = values["Sat+"];
                luminance = values["Lum+"];
            }
            catch { }
        }
        public HSLColor(byte a, byte r, byte g, byte b)
        {
            System.Drawing.Color c1 = System.Drawing.Color.FromArgb(255, r, g, b);
            hue = c1.GetHue();
            saturation = c1.GetSaturation();
            luminance = c1.GetBrightness();
        }
        public HSLColor(Color c)
        {
            System.Drawing.Color c1 = System.Drawing.Color.FromArgb(255, c.R, c.G, c.B);
            hue = c1.GetHue();
            saturation = c1.GetSaturation();
            luminance = c1.GetBrightness();
        }

        public HSLColor(System.Drawing.Color c)
        {
            hue = c.GetHue();
            saturation = c.GetSaturation();
            luminance = c.GetBrightness();
        }
        public HSLColor(HSLColor c)
        {
            if (c is null) return;
            hue = c.hue;
            saturation = c.saturation;
            luminance = c.luminance;
        }

        public override string ToString()
        { return "H:" + hue.ToString("f2") + " S:" + saturation.ToString("f2") + " L:" + luminance.ToString("f2"); }

        public static float operator -(HSLColor c1, HSLColor c2)
        {
            //any lum > .95 is white  \
            //any lum < .15 is black    -set hue to .5
            //any sat  < .1 is gray   /
            if (c1.luminance > 0.95) c1.hue = 0.5f;
            else if (c1.luminance < .1) c1.hue = 0.5f;
            else if (c1.saturation < .1) c1.hue = 0.5f;
            if (c2.luminance > 0.95) c2.hue = 0.5f;
            else if (c2.luminance < .1) c2.hue = 0.5f;
            else if (c2.saturation < .1) c2.hue = 0.5f;
            float diff = Abs(c1.hue - c2.hue) * 5 + Abs(c1.saturation - c2.saturation) + Abs(c1.luminance - c2.luminance);
            diff /= 7;
            return diff;
        }
        public Color ToColor()
        {
            Color c1 = ColorFromHSL2();
            return c1;
        }
        // the Color Converter
        Color ColorFromHSL()
        {
            if (saturation == 0)
            {
                byte L = (byte)(luminance * 255);
                return Color.FromArgb(255, L, L, L);
            }

            double min, max;

            max = luminance < 0.5d ? luminance * (1 + saturation) : (luminance + saturation) - (luminance * saturation);
            min = (luminance * 2d) - max;

            Color c = Color.FromArgb(255, (byte)(255 * RGBChannelFromHue(min, max, ((int)hue) + 1 / 3d)),
                                          (byte)(255 * RGBChannelFromHue(min, max, ((int)hue))),
                                          (byte)(255 * RGBChannelFromHue(min, max, ((int)hue) - 1 / 3d)));
            //Debug.WriteLine(this + "\t" + c);
            return c;
        }

        Color ColorFromHSL2()
        {
            double C = (1 - Abs(2 * luminance - 1)) * saturation;
            double X = C * (1 - Abs((hue / 60) % 2 - 1));
            double m = luminance - C / 2;

            Color c = default!;

            if (000 <= hue && hue < 060) c = Color.FromArgb(255, (byte)((C + m) * 255), (byte)((X + m) * 255), (byte)((0 + m) * 255));
            if (060 <= hue && hue < 120) c = Color.FromArgb(255, (byte)((X + m) * 255), (byte)((C + m) * 255), (byte)((0 + m) * 255));
            if (120 <= hue && hue < 180) c = Color.FromArgb(255, (byte)((0 + m) * 255), (byte)((C + m) * 255), (byte)((X + m) * 255));
            if (180 <= hue && hue < 240) c = Color.FromArgb(255, (byte)((0 + m) * 255), (byte)((X + m) * 255), (byte)((C + m) * 255));
            if (240 <= hue && hue < 300) c = Color.FromArgb(255, (byte)((X + m) * 255), (byte)((0 + m) * 255), (byte)((C + m) * 255));
            if (300 <= hue && hue < 345) c = Color.FromArgb(255, (byte)((C + m) * 255), (byte)((0 + m) * 255), (byte)((C + m) * 255));
            if (hue > 345) c = Color.FromArgb(255, (byte)((C + m) * 255), (byte)((X + m) * 255), (byte)((0 + m) * 255));

            return c;
        }

        static double RGBChannelFromHue(double m1, double m2, double h)
        {
            h = (h + 1d) % 1d;
            if (h < 0) h += 1;
            if (h * 6 < 1) return m1 + (m2 - m1) * 6 * h;
            else if (h * 2 < 1) return m2;
            else if (h * 3 < 2) return m1 + (m2 - m1) * 6 * (2d / 3d - h);
            else return m1;
        }

        public bool Equals(HSLColor c1)
        {
            if (c1 is null) return false;
            if (luminance < .05)
            {
                if (c1.luminance < .05) return true;
            }
            if (luminance > .95)
            {
                if (c1.luminance > .95) return true;
            }
            float absHueDiff = Abs(hue - c1.hue);
            if (absHueDiff < 5 || absHueDiff > 355)
                return true;
            return false;
        }
    }


    public static class Utils
    {


#if NOTUSED_WINDOWS
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern void GetSystemTimePreciseAsFileTime(out long filetime);

        public static long GetPreciseTime()
        { 
            GetSystemTimePreciseAsFileTime(out long fileTime);
            return fileTime;
        }
#endif

        public static float RoundToSignificantDigits( this float d, int digits )
        {
            if( d == 0 )
                return 0;

            double scale = Math.Pow( 10, Math.Floor( Math.Log10( Math.Abs( d ) ) ) + 1 );
            return ( float )( scale * Math.Round( d / scale, digits ) );
        }

        //this searches a control tree to find a control by name so you can retrieve its value
        public static Control FindByName( Visual v, string name )
        {
            foreach( Object o in Avalonia.LogicalTree.LogicalExtensions.GetLogicalChildren( v ) )
            {
                if( o is Visual v3 )
                {
                    if( v3 is Control c1 )
                    {
                        if( c1.Name == name )
                            return c1;
                    }
                    try
                    {
                        Control c2 = FindByName( v3, name );
                        if( c2 is not null )
                            return c2;
                    }
                    catch { }
                }
            }
            return null;
        }

#if NOT_USED

        public static float Rad(float degrees)
        {
            return (float)(degrees * Math.PI / 180);
        }

        public static System.Drawing.Color IntToDrawingColor(int theColor)
        {
            Color c1 = IntToColor(theColor);
            System.Drawing.Color c = System.Drawing.Color.FromArgb(c1.A, c1.R, c1.G, c1.B);
            return c;
        }

        public static Color IntToColor(int theColor)
        {
            Color c = new Color();
            c.A = 255;
            c.B = (byte)(theColor & 0xff);
            c.G = (byte)(theColor >> 8 & 0xff);
            c.R = (byte)(theColor >> 16 & 0xff);
            return c;
        }
#endif

        public static int ColorToInt( Color theColor )
        {
            int retVal = 0;
            //retVal += theColor.A << 24; do we need "a" value?
            retVal += theColor.R << 16;
            retVal += theColor.G << 8;
            retVal += theColor.B;
            return retVal;
        }

        public static bool Close( int a, int b )
        {
            if( Math.Abs( a - b ) < 4 ) return true;
            return false;
        }
        public static bool ColorClose( Color c1, Color c2 )
        {
            if( Close( c1.R, c2.R ) && Close( c1.G, c2.G ) && Close( c1.B, c2.B ) ) return true;
            return false;
        }

        public static string GetColorName( Color col )
        {
            PropertyInfo[] p1 = typeof( Colors ).GetProperties();
            foreach( PropertyInfo p in p1 )
            {
                Color c = ( Color )p.GetValue( null );
                if( ColorClose( c, col ) )
                    return p.Name;
            }
            return "0x" + col.R.ToString( "X2" ) + col.G.ToString( "X2" ) + col.B.ToString( "X2" );
        }
        public static double FindDistanceToSegment( Segment s )
        {
            if( s is null ) return 0;
            return FindDistanceToSegment( new Point( 0, 0 ), s.P1.P, s.P2.P, out Point closest );
        }

        public static double FindDistanceToSegment( Segment s, out Point closest )
        {
            return FindDistanceToSegment( new Point( 0, 0 ), s.P1.P, s.P2.P, out closest );
        }

        public static float DistancePointToSegment( Segment s, PointPlus PIn )
        {
            Point A = s.P1;
            Point B = s.P2;
            Point P = PIn;
            Vector AP = P - A;       //Vector from A to P   
            Vector AB = B - A;    //Vector from A to B  

            float magnitudeAB = ( float )( AB.Length * AB.Length );     //Magnitude of AB vector (it's length squared)     
            float ABAPproduct = ( float )Vector.Multiply( AP, AB ).Length;    //The DOT product of a_to_p and a_to_b--projection of P onto AB     
            float distance = ABAPproduct / magnitudeAB; //The normalized "distance" from a to your closest point  

            if( distance < 0 )     //Check if P projection is over vectorAB     
            {
                return ( float )AP.Length;
            }
            else if( distance > 1 )
            {
                var t = ( P - B );
                return ( float )( t.X * t.Y );
            }
            else
            {
                PointPlus closest = A + AB * distance;
                return ( closest - PIn ).R;
            }
        }

        public static float DistanceBetweenTwoSegments( Segment s1, Segment s2 )
        {
            float retVal = float.MaxValue;
            double d1 = DistancePointToSegment( s1, s2.P1 );
            if( d1 < retVal )
                retVal = ( float )d1;
            d1 = DistancePointToSegment( s1, s2.P2 );
            if( d1 < retVal )
                retVal = ( float )d1;
            d1 = DistancePointToSegment( s2, s1.P1 );
            if( d1 < retVal )
                retVal = ( float )d1;
            d1 = DistancePointToSegment( s2, s1.P1 );
            if( d1 < retVal )
                retVal = ( float )d1;
            return retVal;
        }

        public static float FindDistanceToSegment( Point pt, Segment s )
        {
            return ( float )FindDistanceToSegment( pt, s.P1, s.P2, out Point closest );
        }
        // Calculate the distance between
        // point pt and the segment p1 --> p2.
        public static double FindDistanceToSegment(
            Point pt, Point p1, Point p2, out Point closest )
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            if( ( dx == 0 ) && ( dy == 0 ) )
            {
                // It's a point not a line segment.
                closest = p1;
                dx = pt.X - p1.X;
                dy = pt.Y - p1.Y;
                return Math.Sqrt( dx * dx + dy * dy );
            }

            // Calculate the t that minimizes the distance.
            double t = ( ( pt.X - p1.X ) * dx + ( pt.Y - p1.Y ) * dy ) /
                ( dx * dx + dy * dy );

            // See if this represents one of the segment's
            // end points or a point in the middle.
            if( t < 0 )
            {
                closest = new Point( p1.X, p1.Y );
                dx = pt.X - p1.X;
                dy = pt.Y - p1.Y;
            }
            else if( t > 1 )
            {
                closest = new Point( p2.X, p2.Y );
                dx = pt.X - p2.X;
                dy = pt.Y - p2.Y;
            }
            else
            {
                closest = new Point( p1.X + t * dx, p1.Y + t * dy );
                dx = pt.X - closest.X;
                dy = pt.Y - closest.Y;
            }

            return Sqrt( dx * dx + dy * dy );
        }
        public static bool SegmentsIntersect( Point p1, Point p2, Point p3, Point p4 )
        {
            FindIntersection( p1, p2, p3, p4,
            out bool lines_intersect, out bool segments_intersect,
            out Point intersection,
            out Point close_p1, out Point close_p2,
            out Angle collisionAngle );
            return segments_intersect;
        }

        // Find the point of intersection between
        // the lines p1 --> p2 and p3 --> p4.
        public static bool FindIntersection( Segment s1, Segment s2, out PointPlus intersection, out Angle angle )
        {
            bool retVal = FindIntersection( s1.P1, s1.P2, s2.P1, s2.P2, out Point intersectionPt, out angle );
            intersection = intersectionPt;
            return retVal;
        }
        public static bool FindIntersection(
            Point p1, Point p2, Point p3, Point p4,
            out Point intersection, out Angle angle
            )
        {
            FindIntersection( p1, p2, p3, p4,
            out bool lines_intersect, out bool segments_intersect,
            out intersection,
            out Point close_p1, out Point close_p2,
            out angle );
            return segments_intersect;
        }
        public static bool LinesIntersect( Segment s1, Segment s2, out PointPlus intersection )
        {
            FindIntersection( s1.P1, s1.P2, s2.P1, s2.P2,
            out bool lines_intersect, out bool segments_intersect,
            out Point intersection1,
            out Point close_p1, out Point close_p2,
            out Angle angle );
            intersection = intersection1;
            return lines_intersect;
        }

        public static void FindIntersection( Point p1, Point p2,
                                            Point p3, Point p4,
                                            out bool lines_intersect,
                                            out bool segments_intersect,
                                            out Point intersection,
                                            out Point close_p1,
                                            out Point close_p2,
                                            out Angle collisionAngle )
        {
            // Get the segments' parameters.
            double dx12 = p2.X - p1.X;
            double dy12 = p2.Y - p1.Y;
            double dx34 = p4.X - p3.X;
            double dy34 = p4.Y - p3.Y;

            double theta1 = Math.Atan2( dy12, dx12 ); //obstacle
            double theta2 = Math.Atan2( dy34, dx34 ); //motion attempt
            collisionAngle = theta2 - theta1; //angle between the two

            // Solve for t1 and t2
            double denominator = ( dy12 * dx34 - dx12 * dy34 );

            double t1 = ( ( p1.X - p3.X ) * dy34 + ( p3.Y - p1.Y ) * dx34 ) / denominator;

            if( double.IsNaN( t1 ) )
            {
                // The lines are parallel (or close enough to it).
                lines_intersect = false;
                segments_intersect = false;
                intersection = new Point( float.NaN, float.NaN );
                close_p1 = new Point( float.NaN, float.NaN );
                close_p2 = new Point( float.NaN, float.NaN );
                return;
            }
            lines_intersect = true;

            double t2 = ( ( p3.X - p1.X ) * dy12 + ( p1.Y - p3.Y ) * dx12 ) / -denominator;

            // Find the point of intersection.
            intersection = new Point( p1.X + dx12 * t1, p1.Y + dy12 * t1 );

            // The segments intersect if t1 and t2 are between 0 and 1.
            segments_intersect =
                ( ( t1 >= 0 ) && ( t1 <= 1 ) &&
                 ( t2 >= 0 ) && ( t2 <= 1 ) );
            //segments_intersect =
            //    ((t1 >= -.09) && (t1 <= 1.09) &&
            //     (t2 >= -.09) && (t2 <= 1.09));

            // Find the closest points on the segments.
            if( t1 < 0 )
            {
                t1 = 0;
            }
            else if( t1 > 1 )
            {
                t1 = 1;
            }

            if( t2 < 0 )
            {
                t2 = 0;
            }
            else if( t2 > 1 )
            {
                t2 = 1;
            }

            close_p1 = new Point( p1.X + dx12 * t1, p1.Y + dy12 * t1 );
            close_p2 = new Point( p3.X + dx34 * t2, p3.Y + dy34 * t2 );
        }
        public static float DistancePointToLine( Point P, Point P1, Point P2 )
        {
            double distance = Abs( ( P2.X - P1.X ) * ( P1.Y - P.Y ) - ( P1.X - P.X ) * ( P2.Y - P1.Y ) ) /
                    Sqrt( Pow( P2.X - P1.X, 2 ) + Math.Pow( P2.Y - P1.Y, 2 ) );
            return ( float )distance;
        }
        public static float DistancePointToLine2( Point P, Point P1, Point P2 )
        {
            double distance = ( ( P2.X - P1.X ) * ( P1.Y - P.Y ) - ( P1.X - P.X ) * ( P2.Y - P1.Y ) ) /
                    Sqrt( Pow( P2.X - P1.X, 2 ) + Math.Pow( P2.Y - P1.Y, 2 ) );
            return ( float )distance;
        }
        public static Segment ExtendSegment( Segment s, float dist )
        {
            Segment retVal = new Segment();
            retVal.P1 = ExtendSegment( s.P1, s.P2, dist, true );
            retVal.P2 = ExtendSegment( s.P1, s.P2, dist, false );
            return retVal;
        }


        //find a point which is dist off the end of a line segment
        public static PointPlus ExtendSegment( Point P1, Point P2, float dist, bool firstPt )
        {
            if( firstPt )
            {
                Vector v = P2 - P1;
                double changeLength = ( v.Length + dist ) / v.Length;
                v = Vector.Multiply( v, changeLength );
                PointPlus newPoint = new PointPlus { P = P2 - v };
                return newPoint;
            }
            else
            {
                Vector v = P1 - P2;
                double changeLength = ( v.Length + dist ) / v.Length;
                v = Vector.Multiply( v, changeLength );

                PointPlus newPoint = new PointPlus { P = P1 - v };
                return newPoint;
            }
        }

        /// <summary>
        /// Determines if the given point is inside the polygon
        /// </summary>
        /// <param name="polygon">the vertices of polygon</param>
        /// <param name="testPoint">the given point</param>
        /// <returns>true if the point is inside the polygon; otherwise, false</returns>
        public static bool IsPointInPolygon( Point[] polygon, Point testPoint )
        {
            bool result = false;
            if( polygon is null ) return false;
            if( polygon.Count() == 2 )
            {
                float dist = DistancePointToLine( testPoint, polygon[ 0 ], polygon[ 1 ] );
                if( dist < 0.1f ) return true;
                return false;
            }
            int j = polygon.Count() - 1;
            if( polygon.Contains( testPoint ) ) return true;
            for( int i = 0; i < polygon.Count(); i++ )
            {
                if( polygon[ i ].Y < testPoint.Y && polygon[ j ].Y >= testPoint.Y || polygon[ j ].Y < testPoint.Y && polygon[ i ].Y >= testPoint.Y )
                {
                    if( polygon[ i ].X + ( testPoint.Y - polygon[ i ].Y ) / ( polygon[ j ].Y - polygon[ i ].Y ) * ( polygon[ j ].X - polygon[ i ].X ) < testPoint.X )
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }



        /// <summary>
        /// Method to compute the centroid of a polygon. This does NOT work for a complex polygon.
        /// </summary>
        /// <param name="poly">points that define the polygon</param>
        /// <returns>centroid point, or PointF.Empty if somethought wrong</returns>
        public static Point GetCentroid( List<Point> poly )
        {
            double accumulatedArea = 0.0f;
            double centerX = 0.0f;
            double centerY = 0.0f;

            if( poly.Count == 2 )
            {
                return new Point( ( poly[ 0 ].X + poly[ 1 ].X ) / 2f, ( poly[ 0 ].Y + poly[ 1 ].Y ) / 2f );
            }


            for( int i = 0, j = poly.Count - 1; i < poly.Count; j = i++ )
            {
                double temp = poly[ i ].X * poly[ j ].Y - poly[ j ].X * poly[ i ].Y;
                accumulatedArea += temp;
                centerX += ( poly[ i ].X + poly[ j ].X ) * temp;
                centerY += ( poly[ i ].Y + poly[ j ].Y ) * temp;
            }

            if( Math.Abs( accumulatedArea ) < 1E-7f )
                return new Point( 0, 0 );  // Avoid division by zero

            accumulatedArea *= 3f;
            return new Point( centerX / accumulatedArea, centerY / accumulatedArea );
        }

        public static List<Type> GetListOfExistingCSharpModuleTypes()
        {
            var listOfBs = ( from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                             from assemblyType in domainAssembly.GetTypes()
                             where typeof( ModuleBase ).IsAssignableFrom( assemblyType )
                             orderby assemblyType.Name
                             select assemblyType
                ).ToArray();
            List<Type> retVal = new List<Type>();
            foreach( var t in listOfBs )
            {
                if( t.Name != "ModuleBase" )
                    retVal.Add( t );
            }
            return retVal;
        }

        static Random randomGenerator = new Random();

        public static string Random( int min, int max )
        {
            int newRandom = randomGenerator.Next( min, max );
            string result = newRandom.ToString();
            return result;
        }

        // Constant strings related to file dialogs. 
        public const string UKSContentFolder = "UKSContent";

        public static FilePickerFileType FilterXMLs { get; } = new( "XML Files" )
        {
            Patterns = new[] { "*.xml" },
            AppleUniformTypeIdentifiers = new[] { "XML Files" },
            MimeTypes = new[] { "application/xml" }
        };

        public static FilePickerFileType FilterTextFile { get; } = new( "Text Files" )
        {
            Patterns = new[] { "*.txt" },
            AppleUniformTypeIdentifiers = new[] { "Text Files" },
            MimeTypes = new[] { "text/plain" }
        };

        public static FilePickerFileType FilterWordListFile { get; } = new( "Word List Files" )
        {
            Patterns = new[] { "*.txt", "*.*" },
            AppleUniformTypeIdentifiers = new[] { "Text Files", "All Files" },
            MimeTypes = new[] { "text/plain", "application/octet-stream" }
        };

        public const string TitleBrainSimLoadWordList = "Select a Brain Simulator Word List File";
        public const string TitleBrainSimImport = "Select a Brain Simulator file to Import";
        public const string TitleBrainSimExport = "Select a Brain Simulator file to Export";
        public const string TitleUKSFileLoad = "Select a Brain UKS Content File to Load";
        public const string TitleUKSFileSave = "Select a Brain UKS Content File to Save";

        public async static Task<string?> OpenFileDialog( Visual? owner, string title, FilePickerFileType filter, string pathToStartIn = "" )
        {
            if( owner is null )
            {
                return null;
            }

            var topLevel = TopLevel.GetTopLevel( owner );

            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = Utils.TitleUKSFileLoad,
                FileTypeFilter = new[] { filter }
            };

            if( pathToStartIn.Length > 0 )
            {
                options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync( new System.Uri( pathToStartIn ) );
            }

            var filepathToOpen = await topLevel.StorageProvider.OpenFilePickerAsync( options );

            if( filepathToOpen is null || filepathToOpen.Count == 0 )
            {
                return null;
            }

            return filepathToOpen[ 0 ].Path.AbsolutePath;
        }

        public async static Task<string?> SaveFileDialog( Visual? owner, string title, FilePickerFileType filter, string pathToStartIn = "" )
        {
            if( owner is null )
            {
                return null;
            }

            var topLevel = TopLevel.GetTopLevel( owner );

            var options = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = Utils.TitleUKSFileLoad,
                FileTypeChoices = new[] { filter }
            };
            if( pathToStartIn.Length > 0 )
            {
                options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync( new System.Uri( pathToStartIn ) );
            }

            var filepathToSaveTo = await topLevel.StorageProvider.SaveFilePickerAsync( options );

            if( filepathToSaveTo is null )
            {
                return null;
            }

            var r = filepathToSaveTo.Path.AbsolutePath;
            return r;
        }

        public static string GetOrAddDocumentsSubFolder( string subfolder )
        {
            string basepath = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );
            string fiafolder = System.IO.Path.Combine( basepath, "FutureAI" );
            System.IO.Directory.CreateDirectory( fiafolder );
            string progfolder = System.IO.Path.Combine( fiafolder, "BrainSimulatorIII" );
            System.IO.Directory.CreateDirectory( progfolder );
            string returnfolder = System.IO.Path.Combine( progfolder, subfolder );
            System.IO.Directory.CreateDirectory( returnfolder );
            return returnfolder;
        }

        public static string RebaseFolderToCurrentDevEnvironment( string fullPath )
        {
            int index = fullPath.ToLower().IndexOf( "\\networks\\" );
            if( index != -1 )
            {
                fullPath = fullPath.Substring( index );
                string Path1 = Path.GetFullPath( "." );
                string Path2 = Path1.Replace( "\\bin\\Debug\\net6.0-windows", "" );
                fullPath = Path2 + fullPath;
            }
            return fullPath;
        }

        public static string CleanAndRecreateDocumentsSubFolder( string subFolder )
        {
            string Folder = Utils.GetOrAddDocumentsSubFolder( subFolder );
            Directory.Delete( Folder, true );
            return Utils.GetOrAddDocumentsSubFolder( subFolder );
        }

        public static string GetOrAddLocalSubFolder( string subfolder )
        {
            string Path1 = Path.GetFullPath( "." );
            string Path2 = Path1.Replace( "\\bin\\Debug\\net6.0-windows", "" );
            string Path3 = Path.Combine( Path2, "..\\BrainSimulator" );
            string Path4 = Path3.Replace( "\\BrainSimulator\\..\\BrainSimulator", "\\BrainSimulator" );
            string Path5 = Path4.Replace( "\\ModuleTester\\..\\BrainSimulator", "\\BrainSimulator" );
            string defaultPath = Path.Combine( Path5, subfolder );
            try
            {
                if( !Directory.Exists( defaultPath ) )
                {
                    Directory.CreateDirectory( defaultPath );
                }
            }
            catch
            {
                defaultPath = "";
            }

            return defaultPath;
        }

        public static string GetOrAddFilenameInLocalSubFolder( string subfolder, string filename )
        {
            string filePath = GetOrAddLocalSubFolder( subfolder );
            return Path.Combine( filePath, filename );
        }

        // Builds a filename of the form 20220313_100357_197_0_0.0_0_0_.jpg from time, turn, move, pan, tilt and extension
        public static string BuildAnnotatedImageFileName( string folder, Angle deltaTurn, double deltaMove, Angle cameraPan, Angle cameraTilt, string extension )
        {
            if( cameraPan is null ) cameraPan = Angle.FromDegrees( 0 );
            if( cameraTilt is null ) cameraTilt = Angle.FromDegrees( 0 );
            DateTime now = DateTime.Now;
            string filename = now.ToString( "yyyyMMdd_HHmmss_fff" ) + "_" +
                              ( int )deltaTurn.Degrees + "_" + deltaMove.ToString( "F1" ) + "_" +
                              ( int )cameraPan.Degrees + "_" + ( int )cameraTilt.Degrees + "_." + extension;
            return System.IO.Path.Combine( folder, filename );
        }

        // Rather than checking them all separately, check if there is no movement by
        // looking for the string "_0_0.0_0_0_" in the filename...
        public static bool ImageHasMovement( string filename )
        {
            // check first if it has enough parts
            // If not, return false since we cannot determine movement
            filename = Path.GetFileNameWithoutExtension( filename );
            if( filename.Split( "_" ).Length != 8 ) return false;
            // Else, check if it contains the "no movement" string
            // CAUTTION: don't check for pan and tilt,
            // since they are no deltas
            return filename.Contains( "_0_0.0_" ) == false;
        }

        // Extracts Turn delta from a filename of the form 20220313_100357_197_0_0.0_0_0_.jpg
        public static Angle GetTurnDeltaFromAnnotatedImageFileName( string filename )
        {
            filename = Path.GetFileName( filename );
            string[] parts = filename.Split( '_' );
            if( parts.Count() <= 3 ) return Angle.FromDegrees( 0 );
            if( !int.TryParse( parts[ 3 ], out int a ) ) return Angle.FromDegrees( 0 );
            return Angle.FromDegrees( a );
        }

        // Extracts Move delta from a filename of the form 20220313_100357_197_0_0.0_0_0_.jpg
        public static double GetMoveDeltaFromAnnotatedImageFileName( string filename )
        {
            filename = Path.GetFileName( filename );
            string[] parts = filename.Split( '_' );
            if( parts.Count() <= 4 ) return 0.0;
            if( !Double.TryParse( parts[ 4 ], out double a ) ) return 0.0;
            return a;
        }

        // Extracts Camera Pan delta from a filename of the form 20220313_100357_197_0_0.0_0_0_.jpg
        public static Angle GetCameraPanFromAnnotatedImageFileName( string filename )
        {
            filename = Path.GetFileName( filename );
            string[] parts = filename.Split( '_' );
            if( parts.Count() <= 5 ) return Angle.FromDegrees( 0 );
            if( !int.TryParse( parts[ 5 ], out int a ) ) return Angle.FromDegrees( 0 );
            return Angle.FromDegrees( a );
        }

        // Extracts Camera Tilt delta from a filename of the form 20220313_100357_197_0_0.0_0_0_.jpg
        public static Angle GetCameraTiltFromAnnotatedImageFileName( string filename )
        {
            filename = Path.GetFileName( filename );
            string[] parts = filename.Split( '_' );
            if( parts.Count() <= 6 ) return Angle.FromDegrees( 0 );
            if( !int.TryParse( parts[ 6 ], out int a ) ) return Angle.FromDegrees( 0 );
            return Angle.FromDegrees( a );
        }

        static int trackid = 1000;

        public static string NewTrackID()
        {
            return ( ++trackid ).ToString( "####" );
        }

        public static string? FindFile( string filename, string path, bool recursive = false )
        {
            foreach( var file in Directory.EnumerateFiles( path ) )
            {
                if( file.EndsWith( filename ) )
                {
                    return file;
                }
            }

            if( recursive == true )
            {
                foreach( var subDir in Directory.EnumerateDirectories( path ) )
                {
                    var r = FindFile( filename, subDir, true );
                    if( r != null )
                    {
                        return r;
                    }
                }
            }
            return null;
        }


    }
}
