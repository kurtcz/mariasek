using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Mariasek.SharedClient.GameComponents
{
    public class LineChart : GameComponent
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public Vector2[][] Series { get; set; }
        public Color AxisColor { get; set; }
        public Color[] Colors { get; set; }
        public Vector2 MinValue { get; set; }
        public Vector2 MaxValue { get; set; }

        public LineChart(GameComponent parent)
            : base(parent)
        {
            MinValue = Vector2.Zero;
            MaxValue = Vector2.One;
            Series = new []
            {
                new [] { new Vector2(0, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(1, 0.25f) },
                new [] { new Vector2(0, 0.1f), new Vector2(0.5f, 0.2f), new Vector2(1, 0.75f) },
                new [] { new Vector2(0, 0.5f), new Vector2(0.5f, 0.75f), new Vector2(1, 0.5f) }
            };
            Colors = new [] { Color.Red, Color.Green, Color.Blue };
            AxisColor = Color.White;
            Width = 100;
            Height = 100;
        }

        private Vector2 LogicalToPhysical(Vector2 point)
        {
            var logicalWidth = (MaxValue - MinValue).X;
            var logicalHeight = (MaxValue - MinValue).Y;

            if (logicalWidth == 0 || logicalHeight == 0)
            {
                return Position;
            }

            return Position + Vector2.UnitY * Height + new Vector2((point.X - MinValue.X) * Width / logicalWidth, -(point.Y - MinValue.Y) * Height / logicalHeight);
        }

        public override void Draw(GameTime gameTime)
        {
            Primitives2D.DrawLine(Game.SpriteBatch, LogicalToPhysical(new Vector2(MinValue.X, 0)), LogicalToPhysical(new Vector2(MaxValue.X, 0)), AxisColor);
            for (var i = 0; i < Series.Length; i++)
            {
                if (Series[i].Length == 0)
                {
                    continue;
                }

                var points = new Vector2[Series[i].Length + 1];

                for(var j = 0; j < Series[i].Length; j++)
                {
                    points[j + 1] = LogicalToPhysical(Series[i][j]);
                }
                points[0] = new Vector2(points[1].X - 1, points[1].Y);
                Primitives2D.DrawSpline(Game.SpriteBatch, points, Colors[i], 3f);
            }

            base.Draw(gameTime);
        }
    }
}

