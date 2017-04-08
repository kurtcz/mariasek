using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mariasek.SharedClient.GameComponents
{
	public static class Primitives2D
	{


		#region Private Members

		private static readonly Dictionary<String, List<Vector2>> circleCache = new Dictionary<string, List<Vector2>>();
		private static Texture2D pixel;

		#endregion


		#region Private Methods

		private static void CreateThePixel(SpriteBatch spriteBatch)
		{
			pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
			pixel.SetData(new[]{ Color.White });
		}


		/// <summary>
		/// Draws a list of connecting points
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// /// <param name="position">Where to position the points</param>
		/// <param name="points">The points to connect with lines</param>
		/// <param name="color">The color to use</param>
		/// <param name="thickness">The thickness of the lines</param>
        private static void DrawPoints(SpriteBatch spriteBatch, Vector2 position, List<Vector2> points, Color color, float thickness, float opacity)
		{
			if (points.Count < 2)
				return;

			for (int i = 1; i < points.Count; i++)
			{
                DrawLine(spriteBatch, points[i - 1] + position, points[i] + position, color, thickness, opacity);
			}
		}


		/// <summary>
		/// Creates a list of vectors that represents a circle
		/// </summary>
		/// <param name="radius">The radius of the circle</param>
		/// <param name="sides">The number of sides to generate</param>
		/// <returns>A list of vectors that, if connected, will create a circle</returns>
		private static List<Vector2> CreateCircle(double radius, int sides)
		{
			// Look for a cached version of this circle
			String circleKey = radius + "x" + sides;
			if (circleCache.ContainsKey(circleKey))
			{
				return circleCache[circleKey];
			}

			List<Vector2> vectors = new List<Vector2>();

			const double max = 2.0 * Math.PI;
			double step = max / sides;

			for (double theta = 0.0; theta < max; theta += step)
			{
				vectors.Add(new Vector2((float)(radius * Math.Cos(theta)), (float)(radius * Math.Sin(theta))));
			}

			// then add the first vector again so it's a complete loop
			vectors.Add(new Vector2((float)(radius * Math.Cos(0)), (float)(radius * Math.Sin(0))));

			// Cache this circle so that it can be quickly drawn next time
			circleCache.Add(circleKey, vectors);

			return vectors;
		}


		/// <summary>
		/// Creates a list of vectors that represents an arc
		/// </summary>
		/// <param name="radius">The radius of the arc</param>
		/// <param name="sides">The number of sides to generate in the circle that this will cut out from</param>
		/// <param name="startingAngle">The starting angle of arc, 0 being to the east, increasing as you go clockwise</param>
		/// <param name="radians">The radians to draw, clockwise from the starting angle</param>
		/// <returns>A list of vectors that, if connected, will create an arc</returns>
		private static List<Vector2> CreateArc(float radius, int sides, float startingAngle, float radians)
		{
			List<Vector2> points = new List<Vector2>();
			points.AddRange(CreateCircle(radius, sides));
			points.RemoveAt(points.Count - 1); // remove the last point because it's a duplicate of the first

			// The circle starts at (radius, 0)
			double curAngle = 0.0;
			double anglePerSide = MathHelper.TwoPi / sides;

			// "Rotate" to the starting point
			while ((curAngle + (anglePerSide / 2.0)) < startingAngle)
			{
				curAngle += anglePerSide;

				// move the first point to the end
				points.Add(points[0]);
				points.RemoveAt(0);
			}

			// Add the first point, just in case we make a full circle
			points.Add(points[0]);

			// Now remove the points at the end of the circle to create the arc
			int sidesInArc = (int)((radians / anglePerSide) + 0.5);
			points.RemoveRange(sidesInArc + 1, points.Count - sidesInArc - 1);

			return points;
		}

		#endregion


		#region FillRectangle

		/// <summary>
		/// Draws a filled rectangle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="rect">The rectangle to draw</param>
		/// <param name="color">The color to draw the rectangle in</param>
		public static void FillRectangle(this SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			if (pixel == null)
			{
				CreateThePixel(spriteBatch);
			}

			// Simply use the function already there
			spriteBatch.Draw(pixel, rect, color);
		}


		/// <summary>
		/// Draws a filled rectangle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="rect">The rectangle to draw</param>
		/// <param name="color">The color to draw the rectangle in</param>
		/// <param name="angle">The angle in radians to draw the rectangle at</param>
		public static void FillRectangle(this SpriteBatch spriteBatch, Rectangle rect, Color color, float angle)
		{
			if (pixel == null)
			{
				CreateThePixel(spriteBatch);
			}

			spriteBatch.Draw(pixel, rect, null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
		}


		/// <summary>
		/// Draws a filled rectangle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="location">Where to draw</param>
		/// <param name="size">The size of the rectangle</param>
		/// <param name="color">The color to draw the rectangle in</param>
		public static void FillRectangle(this SpriteBatch spriteBatch, Vector2 location, Vector2 size, Color color)
		{
			FillRectangle(spriteBatch, location, size, color, 0.0f);
		}


		/// <summary>
		/// Draws a filled rectangle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="location">Where to draw</param>
		/// <param name="size">The size of the rectangle</param>
		/// <param name="angle">The angle in radians to draw the rectangle at</param>
		/// <param name="color">The color to draw the rectangle in</param>
		public static void FillRectangle(this SpriteBatch spriteBatch, Vector2 location, Vector2 size, Color color, float angle)
		{
			if (pixel == null)
			{
				CreateThePixel(spriteBatch);
			}

			// stretch the pixel between the two vectors
			spriteBatch.Draw(pixel,
			                 location,
			                 null,
			                 color,
			                 angle,
			                 Vector2.Zero,
			                 size,
			                 SpriteEffects.None,
			                 0);
		}


		/// <summary>
		/// Draws a filled rectangle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="x">The X coord of the left side</param>
		/// <param name="y">The Y coord of the upper side</param>
		/// <param name="w">Width</param>
		/// <param name="h">Height</param>
		/// <param name="color">The color to draw the rectangle in</param>
		public static void FillRectangle(this SpriteBatch spriteBatch, float x, float y, float w, float h, Color color)
		{
			FillRectangle(spriteBatch, new Vector2(x, y), new Vector2(w, h), color, 0.0f);
		}


		/// <summary>
		/// Draws a filled rectangle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="x">The X coord of the left side</param>
		/// <param name="y">The Y coord of the upper side</param>
		/// <param name="w">Width</param>
		/// <param name="h">Height</param>
		/// <param name="color">The color to draw the rectangle in</param>
		/// <param name="angle">The angle of the rectangle in radians</param>
		public static void FillRectangle(this SpriteBatch spriteBatch, float x, float y, float w, float h, Color color, float angle)
		{
			FillRectangle(spriteBatch, new Vector2(x, y), new Vector2(w, h), color, angle);
		}

		#endregion


		#region DrawRectangle

		/// <summary>
		/// Draws a rectangle with the thickness provided
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="rect">The rectangle to draw</param>
		/// <param name="color">The color to draw the rectangle in</param>
		public static void DrawRectangle(this SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			DrawRectangle(spriteBatch, rect, color, 1.0f);
		}


		/// <summary>
		/// Draws a rectangle with the thickness provided
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="rect">The rectangle to draw</param>
		/// <param name="color">The color to draw the rectangle in</param>
		/// <param name="thickness">The thickness of the lines</param>
		public static void DrawRectangle(this SpriteBatch spriteBatch, Rectangle rect, Color color, float thickness)
		{

			// TODO: Handle rotations
			// TODO: Figure out the pattern for the offsets required and then handle it in the line instead of here

			DrawLine(spriteBatch, new Vector2(rect.X, rect.Y), new Vector2(rect.Right, rect.Y), color, thickness); // top
			DrawLine(spriteBatch, new Vector2(rect.X + 1f, rect.Y), new Vector2(rect.X + 1f, rect.Bottom + thickness), color, thickness); // left
			DrawLine(spriteBatch, new Vector2(rect.X, rect.Bottom), new Vector2(rect.Right, rect.Bottom), color, thickness); // bottom
			DrawLine(spriteBatch, new Vector2(rect.Right + 1f, rect.Y), new Vector2(rect.Right + 1f, rect.Bottom + thickness), color, thickness); // right
		}


		/// <summary>
		/// Draws a rectangle with the thickness provided
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="location">Where to draw</param>
		/// <param name="size">The size of the rectangle</param>
		/// <param name="color">The color to draw the rectangle in</param>
		public static void DrawRectangle(this SpriteBatch spriteBatch, Vector2 location, Vector2 size, Color color)
		{
			DrawRectangle(spriteBatch, new Rectangle((int)location.X, (int)location.Y, (int)size.X, (int)size.Y), color, 1.0f);
		}


		/// <summary>
		/// Draws a rectangle with the thickness provided
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="location">Where to draw</param>
		/// <param name="size">The size of the rectangle</param>
		/// <param name="color">The color to draw the rectangle in</param>
		/// <param name="thickness">The thickness of the line</param>
		public static void DrawRectangle(this SpriteBatch spriteBatch, Vector2 location, Vector2 size, Color color, float thickness)
		{
			DrawRectangle(spriteBatch, new Rectangle((int)location.X, (int)location.Y, (int)size.X, (int)size.Y), color, thickness);
		}

		#endregion


		#region DrawLine

		/// <summary>
		/// Draws a line from point1 to point2 with an offset
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="x1">The X coord of the first point</param>
		/// <param name="y1">The Y coord of the first point</param>
		/// <param name="x2">The X coord of the second point</param>
		/// <param name="y2">The Y coord of the second point</param>
		/// <param name="color">The color to use</param>
		public static void DrawLine(this SpriteBatch spriteBatch, float x1, float y1, float x2, float y2, Color color)
		{
			DrawLine(spriteBatch, new Vector2(x1, y1), new Vector2(x2, y2), color, 1.0f);
		}


		/// <summary>
		/// Draws a line from point1 to point2 with an offset
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="x1">The X coord of the first point</param>
		/// <param name="y1">The Y coord of the first point</param>
		/// <param name="x2">The X coord of the second point</param>
		/// <param name="y2">The Y coord of the second point</param>
		/// <param name="color">The color to use</param>
		/// <param name="thickness">The thickness of the line</param>
		public static void DrawLine(this SpriteBatch spriteBatch, float x1, float y1, float x2, float y2, Color color, float thickness)
        {
            DrawLine(spriteBatch, x1, y1, x2, y2, color, thickness, 1.0f);
        }

        public static void DrawLine(this SpriteBatch spriteBatch, float x1, float y1, float x2, float y2, Color color, float thickness, float opacity)
		{
			DrawLine(spriteBatch, new Vector2(x1, y1), new Vector2(x2, y2), color, thickness, opacity);
		}

		/// <summary>
		/// Draws a line from point1 to point2 with an offset
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="point1">The first point</param>
		/// <param name="point2">The second point</param>
		/// <param name="color">The color to use</param>
		public static void DrawLine(this SpriteBatch spriteBatch, Vector2 point1, Vector2 point2, Color color)
		{
			DrawLine(spriteBatch, point1, point2, color, 1.0f);
		}


		/// <summary>
		/// Draws a line from point1 to point2 with an offset
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="point1">The first point</param>
		/// <param name="point2">The second point</param>
		/// <param name="color">The color to use</param>
		/// <param name="thickness">The thickness of the line</param>
        public static void DrawLine(this SpriteBatch spriteBatch, Vector2 point1, Vector2 point2, Color color, float thickness)
        {
            DrawLine(spriteBatch, point1, point2, color, thickness, 1.0f);
        }

        public static void DrawLine(this SpriteBatch spriteBatch, Vector2 point1, Vector2 point2, Color color, float thickness, float opacity)
		{
			// calculate the distance between the two vectors
			float distance = Vector2.Distance(point1, point2);

			// calculate the angle between the two vectors
			float angle = (float)Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);

			DrawLine(spriteBatch, point1, distance, angle, color, thickness, opacity);
		}


		/// <summary>
		/// Draws a line from point1 to point2 with an offset
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="point">The starting point</param>
		/// <param name="length">The length of the line</param>
		/// <param name="angle">The angle of this line from the starting point in radians</param>
		/// <param name="color">The color to use</param>
		public static void DrawLine(this SpriteBatch spriteBatch, Vector2 point, float length, float angle, Color color)
		{
			DrawLine(spriteBatch, point, length, angle, color, 1.0f);
		}


		/// <summary>
		/// Draws a line from point1 to point2 with an offset
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="point">The starting point</param>
		/// <param name="length">The length of the line</param>
		/// <param name="angle">The angle of this line from the starting point</param>
		/// <param name="color">The color to use</param>
		/// <param name="thickness">The thickness of the line</param>
		public static void DrawLine(this SpriteBatch spriteBatch, Vector2 point, float length, float angle, Color color, float thickness)
        {
            DrawLine(spriteBatch, point, length, angle, color, thickness, 1.0f);
        }

        public static void DrawLine(this SpriteBatch spriteBatch, Vector2 point, float length, float angle, Color color, float thickness, float opacity)
		{
			if (pixel == null || pixel.IsDisposed)
			{
				CreateThePixel(spriteBatch);
			}

			// stretch the pixel between the two vectors
			spriteBatch.Draw(pixel,
			                 point,
			                 null,
			                 color * opacity,
			                 angle,
			                 Vector2.Zero,
			                 new Vector2(length, thickness),
			                 SpriteEffects.None,
			                 0);
		}

		#endregion


		#region PutPixel

		public static void PutPixel(this SpriteBatch spriteBatch, float x, float y, Color color)
		{
			PutPixel(spriteBatch, new Vector2(x, y), color);
		}


		public static void PutPixel(this SpriteBatch spriteBatch, Vector2 position, Color color)
		{
			if (pixel == null)
			{
				CreateThePixel(spriteBatch);
			}

			spriteBatch.Draw(pixel, position, color);
		}

		#endregion


		#region DrawCircle

		/// <summary>
		/// Draw a circle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="center">The center of the circle</param>
		/// <param name="radius">The radius of the circle</param>
		/// <param name="sides">The number of sides to generate</param>
		/// <param name="color">The color of the circle</param>
		public static void DrawCircle(this SpriteBatch spriteBatch, Vector2 center, float radius, int sides, Color color)
		{
			DrawPoints(spriteBatch, center, CreateCircle(radius, sides), color, 1.0f, 1.0f);
		}


		/// <summary>
		/// Draw a circle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="center">The center of the circle</param>
		/// <param name="radius">The radius of the circle</param>
		/// <param name="sides">The number of sides to generate</param>
		/// <param name="color">The color of the circle</param>
		/// <param name="thickness">The thickness of the lines used</param>
		public static void DrawCircle(this SpriteBatch spriteBatch, Vector2 center, float radius, int sides, Color color, float thickness)
        {
            DrawPoints(spriteBatch, center, CreateCircle(radius, sides), color, thickness, 1.0f);
        }

        public static void DrawCircle(this SpriteBatch spriteBatch, Vector2 center, float radius, int sides, Color color, float thickness, float opacity)
		{
			DrawPoints(spriteBatch, center, CreateCircle(radius, sides), color, thickness, opacity);
		}


		/// <summary>
		/// Draw a circle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="x">The center X of the circle</param>
		/// <param name="y">The center Y of the circle</param>
		/// <param name="radius">The radius of the circle</param>
		/// <param name="sides">The number of sides to generate</param>
		/// <param name="color">The color of the circle</param>
		public static void DrawCircle(this SpriteBatch spriteBatch, float x, float y, float radius, int sides, Color color)
		{
			DrawPoints(spriteBatch, new Vector2(x, y), CreateCircle(radius, sides), color, 1.0f, 1.0f);
		}


		/// <summary>
		/// Draw a circle
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="x">The center X of the circle</param>
		/// <param name="y">The center Y of the circle</param>
		/// <param name="radius">The radius of the circle</param>
		/// <param name="sides">The number of sides to generate</param>
		/// <param name="color">The color of the circle</param>
		/// <param name="thickness">The thickness of the lines used</param>
		public static void DrawCircle(this SpriteBatch spriteBatch, float x, float y, float radius, int sides, Color color, float thickness)
		{
			DrawPoints(spriteBatch, new Vector2(x, y), CreateCircle(radius, sides), color, thickness, 1.0f);
		}

		#endregion

        #region DrawPolygon

        public static void DrawPolygon(this SpriteBatch spriteBatch, Vector2 center, List<Vector2>vertices, Color color, float thickness)
        {
            DrawPoints(spriteBatch, center, vertices, color, thickness, 1.0f);
        }

        public static void DrawPolygon(this SpriteBatch spriteBatch, Vector2 center, List<Vector2>vertices, Color color, float thickness, float opacity)
        {
            if (vertices.Count > 0)
            {
                vertices.Add(vertices[0]);
            }
            DrawPoints(spriteBatch, center, vertices, color, thickness, opacity);
        }

        #endregion

        #region DrawArc

        /// <summary>
        /// Draw a arc
        /// </summary>
        /// <param name="spriteBatch">The destination drawing surface</param>
        /// <param name="center">The center of the arc</param>
        /// <param name="radius">The radius of the arc</param>
        /// <param name="sides">The number of sides to generate</param>
        /// <param name="startingAngle">The starting angle of arc, 0 being to the east, increasing as you go clockwise</param>
        /// <param name="radians">The number of radians to draw, clockwise from the starting angle</param>
        /// <param name="color">The color of the arc</param>
        public static void DrawArc(this SpriteBatch spriteBatch, Vector2 center, float radius, int sides, float startingAngle, float radians, Color color)
		{
			DrawArc(spriteBatch, center, radius, sides, startingAngle, radians, color, 1.0f);
		}


		/// <summary>
		/// Draw a arc
		/// </summary>
		/// <param name="spriteBatch">The destination drawing surface</param>
		/// <param name="center">The center of the arc</param>
		/// <param name="radius">The radius of the arc</param>
		/// <param name="sides">The number of sides to generate</param>
		/// <param name="startingAngle">The starting angle of arc, 0 being to the east, increasing as you go clockwise</param>
		/// <param name="radians">The number of radians to draw, clockwise from the starting angle</param>
		/// <param name="color">The color of the arc</param>
		/// <param name="thickness">The thickness of the arc</param>
		public static void DrawArc(this SpriteBatch spriteBatch, Vector2 center, float radius, int sides, float startingAngle, float radians, Color color, float thickness)
		{
			List<Vector2> arc = CreateArc(radius, sides, startingAngle, radians);
			//List<Vector2> arc = CreateArc2(radius, sides, startingAngle, degrees);
			DrawPoints(spriteBatch, center, arc, color, thickness, 1.0f);
		}

		#endregion

        #region DrawSpline

        public static void DrawSpline(this SpriteBatch spriteBatch, Vector2[] points, Color color)
        {
            DrawSpline(spriteBatch, points, color, 1.0f);
        }

        public static void DrawSpline(this SpriteBatch spriteBatch, Vector2[] points, Color color, float thickness)
        {
            DrawSpline(spriteBatch, points, color, thickness, 1.0f);
        }

        public static void DrawSpline(this SpriteBatch spriteBatch, Vector2[] points, Color color, float thickness, float opacity)
        {
            var spline = new SplineInterpolator(points);
            var splinePoints = new List<Vector2>();
            var splinePointsLength = (int)points.Last().X - (int)points.First().X;

            for (var i = 0; i < splinePointsLength; i++)
            {
                var x = points.First().X + i;
                var y = (float)spline.GetValue(x);
                splinePoints.Add(new Vector2(x, y));
            }
            DrawPoints(spriteBatch, Vector2.Zero, splinePoints, color, thickness, opacity);
        }

        #endregion

        /// <summary>
        /// Spline interpolation class.
        /// </summary>
        private class SplineInterpolator
        {
            private readonly double[] _keys;

            private readonly double[] _values;

            private readonly double[] _h;

            private readonly double[] _a;

            public SplineInterpolator(Vector2[] nodes)
                : this (nodes.ToDictionary(k => (double)k.X, v => (double)v.Y))
            {
            }

            /// <summary>
            /// Class constructor.
            /// </summary>
            /// <param name="nodes">Collection of known points for further interpolation.
            /// Should contain at least two items.</param>
            public SplineInterpolator(IDictionary<double, double> nodes)
            {
                if (nodes == null)
                {
                    throw new ArgumentNullException("nodes");
                }

                var n = nodes.Count;

                if (n < 2)
                {
                    throw new ArgumentException("At least two point required for interpolation.");
                }

                _keys = nodes.Keys.ToArray();
                _values = nodes.Values.ToArray();
                _a = new double[n];
                _h = new double[n];

                for (int i = 1; i < n; i++)
                {
                    _h[i] = _keys[i] - _keys[i - 1];
                }

                if (n > 2)
                {
                    var sub = new double[n - 1];
                    var diag = new double[n - 1];
                    var sup = new double[n - 1];

                    for (int i = 1; i <= n - 2; i++)
                    {
                        diag[i] = (_h[i] + _h[i + 1]) / 3;
                        sup[i] = _h[i + 1] / 6;
                        sub[i] = _h[i] / 6;
                        _a[i] = (_values[i + 1] - _values[i]) / _h[i + 1] - (_values[i] - _values[i - 1]) / _h[i];
                    }

                    SolveTridiag(sub, diag, sup, ref _a, n - 2);
                }
            }

            /// <summary>
            /// Gets interpolated value for specified argument.
            /// </summary>
            /// <param name="key">Argument value for interpolation. Must be within 
            /// the interval bounded by lowest ang highest <see cref="_keys"/> values.</param>
            public double GetValue(double key)
            {
                int gap = 1;
                var previous = double.MinValue;

                // At the end of this iteration, "gap" will contain the index of the interval
                // between two known values, which contains the unknown z, and "previous" will
                // contain the biggest z value among the known samples, left of the unknown z
                for (int i = 0; i < _keys.Length; i++)
                {
                    if (_keys[i] < key && _keys[i] > previous)
                    {
                        previous = _keys[i];
                        gap = i + 1;
                    }
                }

                var x1 = key - previous;
                var x2 = _h[gap] - x1;

                return ((-_a[gap - 1] / 6 * (x2 + _h[gap]) * x1 + _values[gap - 1]) * x2 +
                    (-_a[gap] / 6 * (x1 + _h[gap]) * x2 + _values[gap]) * x1) / _h[gap];
            }


            /// <summary>
            /// Solve linear system with tridiagonal n*n matrix "a"
            /// using Gaussian elimination without pivoting.
            /// </summary>
            private static void SolveTridiag(double[] sub, double[] diag, double[] sup, ref double[] b, int n)
            {
                int i;

                for (i = 2; i <= n; i++)
                {
                    sub[i] = sub[i] / diag[i - 1];
                    diag[i] = diag[i] - sub[i] * sup[i - 1];
                    b[i] = b[i] - sub[i] * b[i - 1];
                }

                b[n] = b[n] / diag[n];

                for (i = n - 1; i >= 1; i--)
                {
                    b[i] = (b[i] - sup[i] * b[i + 1]) / diag[i];
                }
            }
        }
    }
}