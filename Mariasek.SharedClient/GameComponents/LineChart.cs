using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mariasek.SharedClient.GameComponents
{
    public enum DataMarkerShape
    {
        Triangle = 3,
        Square = 4,
        Pentagon = 5,
        Hexagon = 6,
        Heptagon = 7,
        Octagon = 8,
        Circle = 16
    }

    public class LineChart : GameComponent
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public Vector2[][] Data { get; set; }
        public string[] Series { get; set; }
        public float XAxisPoint { get; set; }
        public float YAxisPoint { get; set; }
        public bool ShowXAxis { get; set; }
        public bool ShowYAxis { get; set; }
        public float TickMarkLength { get; set; }
        public bool ShowXAxisTickMarks { get; set; }
        public bool ShowYAxisTickMarks { get; set; }
        public Vector2 GridInterval { get; set; }
        public bool ShowHorizontalGridLines { get; set; }
        public bool ShowVerticalGridLines { get; set; }
        public Color AxisColor { get; set; }
        public Color HorizontalGridLineColor { get; set; }
        public Color VerticalGridLineColor { get; set; }
        public Color[] Colors { get; set; }
        public Vector2 MinValue { get; set; }
        public Vector2 MaxValue { get; set; }
        public float LineThickness { get; set; }
        public float AxisThickness { get; set; }
        public float Opacity { get; set; }
        public float DataMarkerSize { get; set; }
        public DataMarkerShape DataMarkerShape { get; set; }

        public LineChart(GameComponent parent)
            : base(parent)
        {
            MinValue = Vector2.Zero;
            MaxValue = Vector2.One;
            Data = new []
            {
                new [] { new Vector2(0, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(1, 0.25f) },
                new [] { new Vector2(0, 0.1f), new Vector2(0.5f, 0.2f), new Vector2(1, 0.75f) },
                new [] { new Vector2(0, 0.5f), new Vector2(0.5f, 0.75f), new Vector2(1, 0.5f) }
            };
            Series = new [] { "Set1", "Set2", "Set3" };
            Colors = new [] { Color.Red, Color.Green, Color.Blue };
            AxisColor = Color.White;
            Width = 100;
            Height = 100;
            Opacity = 1f;
            LineThickness = 1f;
            AxisThickness = 1f;
            DataMarkerSize = 6f;
            DataMarkerShape = DataMarkerShape.Circle;
            XAxisPoint = 0f;
            YAxisPoint = 0f;
            ShowXAxis = true;
            ShowYAxis = true;
            TickMarkLength = 5f;
            ShowXAxisTickMarks = true;
            ShowYAxisTickMarks = true;
            ShowHorizontalGridLines = true;
            ShowVerticalGridLines = true;
            GridInterval = new Vector2(0.25f, 0.25f);
            HorizontalGridLineColor = Color.Gray;
            VerticalGridLineColor = Color.Gray;
        }

        private Vector2 LogicalToPhysical(Vector2 point)
        {
            var logicalWidth = (MaxValue - MinValue).X;
            var logicalHeight = (MaxValue - MinValue).Y;

            if (logicalWidth == 0 || logicalHeight == 0)
            {
                return Position;
            }

            var result = Position + Vector2.UnitY * (Height - DataMarkerSize) + new Vector2((point.X - MinValue.X) * (Width - DataMarkerSize) / logicalWidth + DataMarkerSize / 2, 
                                                                                            -(point.Y - MinValue.Y) * (Height - DataMarkerSize) / logicalHeight + DataMarkerSize / 2);

            return result;
        }

        public override void Draw(GameTime gameTime)
        {
            if (IsVisible)
            {
                Game.SpriteBatch.End();
                var origClippingRectangle = Game.GraphicsDevice.ScissorRectangle;
                Game.GraphicsDevice.ScissorRectangle = new Rectangle((int)(Game.ScaleMatrix.M41 + Position.X*Game.ScaleMatrix.M11), 
                                                                     (int)(Game.ScaleMatrix.M42 + Position.Y*Game.ScaleMatrix.M22), 
                                                                     (int)(Width*Game.ScaleMatrix.M11), 
                                                                     (int)(Height*Game.ScaleMatrix.M22));
                Game.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, new RasterizerState { ScissorTestEnable = true }, null, Game.ScaleMatrix);

                if (ShowHorizontalGridLines)
                {
                    var offset = MinValue.Y % GridInterval.Y;

                    for (int i = 0; MinValue.Y + i * GridInterval.Y < MaxValue.Y; i++)
                    {                        
                        var lineStart = LogicalToPhysical(new Vector2(MinValue.X, MinValue.Y + i * GridInterval.Y - offset));
                        var lineEnd = LogicalToPhysical(new Vector2(MaxValue.X, MinValue.Y + i * GridInterval.Y - offset));

                        Primitives2D.DrawLine(Game.SpriteBatch, lineStart, lineEnd, HorizontalGridLineColor, 1f, Opacity);
                    }
                }
                if (ShowVerticalGridLines)
                {
                    var offset = MinValue.X % GridInterval.X;

                    for (int i = 0; MinValue.X + i * GridInterval.X < MaxValue.X; i++)
                    {                        
                        var lineStart = LogicalToPhysical(new Vector2(MinValue.X + i * GridInterval.X - offset, MinValue.Y));
                        var lineEnd = LogicalToPhysical(new Vector2(MinValue.X + i * GridInterval.X - offset, MaxValue.Y));

                        Primitives2D.DrawLine(Game.SpriteBatch, lineStart, lineEnd, VerticalGridLineColor, 1f, Opacity);
                    }
                }
                if (ShowXAxis)
                {
                    Primitives2D.DrawLine(Game.SpriteBatch, LogicalToPhysical(new Vector2(MinValue.X, XAxisPoint)), LogicalToPhysical(new Vector2(MaxValue.X, XAxisPoint)), AxisColor, AxisThickness, Opacity);
                    if (ShowXAxisTickMarks)
                    {                    
                        var offset = MinValue.X % GridInterval.X;

                        for (int i = 0; MinValue.X + i * GridInterval.X < MaxValue.X; i++)
                        {                        
                            var tickCentre = LogicalToPhysical(new Vector2(MinValue.X + i * GridInterval.X - offset, XAxisPoint));
                            var tickStart = new Vector2(tickCentre.X, tickCentre.Y - TickMarkLength / 2);
                            var tickEnd = new Vector2(tickStart.X, tickStart.Y + TickMarkLength);

                            Primitives2D.DrawLine(Game.SpriteBatch, tickStart, tickEnd, AxisColor, AxisThickness, Opacity);
                        }
                    }
                }
                if (ShowYAxis)
                {
                    Primitives2D.DrawLine(Game.SpriteBatch, LogicalToPhysical(new Vector2(YAxisPoint, MinValue.Y)), LogicalToPhysical(new Vector2(YAxisPoint, MaxValue.Y)), AxisColor, AxisThickness, Opacity);
                    if (ShowYAxisTickMarks)
                    {                    
                        var offset = MinValue.Y % GridInterval.Y;

                        for (int i = 0; MinValue.Y + i * GridInterval.Y < MaxValue.Y; i++)
                        {                        
                            var tickCentre = LogicalToPhysical(new Vector2(YAxisPoint, MinValue.Y + i * GridInterval.Y - offset));
                            var tickStart = new Vector2(tickCentre.X - TickMarkLength / 2, tickCentre.Y);
                            var tickEnd = new Vector2(tickStart.X + TickMarkLength, tickStart.Y);

                            Primitives2D.DrawLine(Game.SpriteBatch, tickStart, tickEnd, AxisColor, AxisThickness, Opacity);
                        }
                    }
                }
                for (var i = 0; i < Data.Length; i++)
                {
                    if (Data[i].Length == 0)
                    {
                        continue;
                    }

                    var points = new Vector2[Data[i].Length + 1];

                    for (var j = 0; j < Data[i].Length; j++)
                    {
                        points[j + 1] = LogicalToPhysical(Data[i][j]);
                    }
                    points[0] = new Vector2(points[1].X - 1, points[1].Y);
                    Primitives2D.DrawSpline(Game.SpriteBatch, points, Colors[i], LineThickness, Opacity);
                    if (DataMarkerSize > 0)
                    {
                        for (var j = 0; j < points.Length; j++)
                        {
                            Primitives2D.DrawCircle(Game.SpriteBatch, points[j], DataMarkerSize / 2, (int)DataMarkerShape, Colors[i], DataMarkerSize / 2, Opacity);
                        }
                    }
                }
                Game.SpriteBatch.End();

                Game.GraphicsDevice.ScissorRectangle = origClippingRectangle;
                Game.SpriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, null, Game.ScaleMatrix);
            }
            base.Draw(gameTime);
        }
    }
}

