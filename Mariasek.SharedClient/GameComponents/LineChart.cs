using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

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

    public class LineChart : TouchControlBase
    {
        private RenderTarget2D _verticalAxisTarget;
        private RenderTarget2D _target;
        public int Width { get; set; }
        public int Height { get; set; }
        private Vector2[][] _data;
        public Vector2[][] Data
        { 
            get { return _data; }
            set
            {
                _data = value;
                UpdateSprite();
            }
        }
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
            _data = new []
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

            UpdateSprite();
			Game.Activated += (sender, e) => UpdateSprite();
            Game.GraphicsDevice.DeviceReset += (sender, e) => UpdateSprite();
        }

        private Vector2 LogicalToPhysical(Vector2 point)
        {
            var logicalWidth = (MaxValue - MinValue).X;
            var logicalHeight = (MaxValue - MinValue).Y;

            if (logicalWidth == 0 || logicalHeight == 0)
            {
                return Vector2.Zero;
            }

            var result =  
                Vector2.UnitY * (Height - DataMarkerSize) + new Vector2((point.X - MinValue.X) * (_chartWidth() - DataMarkerSize) / logicalWidth + DataMarkerSize / 2, 
                                                                        -(point.Y - MinValue.Y) * (Height - DataMarkerSize) / logicalHeight + DataMarkerSize / 2);

            return result;
        }

        public bool SizeChartToFit { get; set; }

        private int _chartWidth()
        {
            if (SizeChartToFit)
            {
                return Width;
            }
            else
            {
                return (int)Math.Max(Width, _data.Length > 0 ? _data[0].Length * 25f : 0);
            }
        }

        private void UpdateSprite()
        {
            //_chartWidth = (int)Math.Max(Width, _data.Length > 0 ? _data[0].Length * 25f : 0);
            //Draw onto the target rather than the back buffer
            _target = new RenderTarget2D(Game.SpriteBatch.GraphicsDevice, _chartWidth(), Height);
            _verticalAxisTarget = new RenderTarget2D(Game.SpriteBatch.GraphicsDevice, (int)TickMarkLength, Height);
            HorizontalScrollOffset = Vector2.Zero;

            if (ShowYAxis)
            {
                Game.SpriteBatch.GraphicsDevice.SetRenderTarget(_verticalAxisTarget);
                Game.SpriteBatch.GraphicsDevice.Clear(Color.Transparent);
                Game.SpriteBatch.Begin();

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
                Game.SpriteBatch.End();
            }

            Game.SpriteBatch.GraphicsDevice.SetRenderTarget(_target);
            Game.SpriteBatch.GraphicsDevice.Clear(Color.Transparent);
            Game.SpriteBatch.Begin();
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
            //if (ShowYAxis)
            //{
            //    Primitives2D.DrawLine(Game.SpriteBatch, LogicalToPhysical(new Vector2(YAxisPoint, MinValue.Y)), LogicalToPhysical(new Vector2(YAxisPoint, MaxValue.Y)), AxisColor, AxisThickness, Opacity);
            //    if (ShowYAxisTickMarks)
            //    {                    
            //        var offset = MinValue.Y % GridInterval.Y;

            //        for (int i = 0; MinValue.Y + i * GridInterval.Y < MaxValue.Y; i++)
            //        {                        
            //            var tickCentre = LogicalToPhysical(new Vector2(YAxisPoint, MinValue.Y + i * GridInterval.Y - offset));
            //            var tickStart = new Vector2(tickCentre.X - TickMarkLength / 2, tickCentre.Y);
            //            var tickEnd = new Vector2(tickStart.X + TickMarkLength, tickStart.Y);

            //            Primitives2D.DrawLine(Game.SpriteBatch, tickStart, tickEnd, AxisColor, AxisThickness, Opacity);
            //        }
            //    }
            //}
            for (var i = 0; i < _data.Length; i++)
            {
                if (_data[i].Length == 0)
                {
                    continue;
                }

                var points = new Vector2[_data[i].Length + 1];

                for (var j = 0; j < _data[i].Length; j++)
                {
                    points[j + 1] = LogicalToPhysical(_data[i][j]);
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
            //restore the original backbuffer as the render target
            Game.SpriteBatch.GraphicsDevice.SetRenderTarget(null);
        }

        public override bool CollidesWithPosition(Vector2 position)
        {
            return position.X >= Position.X &&
                position.Y >= Position.Y &&
                position.X <= Position.X + Width &&
                position.Y <= Position.Y + Height;
        }

        private TouchLocation _touchDownLocation;
        private TouchLocation _touchHeldLocation;
        private TouchLocation _previoustouchHeldLocation;
        private Vector2 HorizontalScrollOffset;
        private Vector2 _oldHorizontalScrollOffset;
        private double _scrollingVelocity;
        private int _scrollingDirection;
        private const float decceleration = 0.02f;

        protected override void OnTouchDown(TouchLocation tl)
        {
            _touchDownLocation = tl;
            _touchHeldLocation = tl;
        }

        protected override void OnTouchUp(TouchLocation tl)
        {
            _previoustouchHeldLocation = _touchHeldLocation;
        }

        protected override bool OnTouchHeld(float touchHeldTimeMs, TouchLocation tl)
        {
            _previoustouchHeldLocation = _touchHeldLocation;
            _touchHeldLocation = tl;

            return false;
        }

        protected override void OnClick()
        {
            if (Vector2.Distance(_touchHeldLocation.Position, _touchDownLocation.Position) < 10)
            {
                if (!SizeChartToFit)
                {
                    _oldHorizontalScrollOffset = HorizontalScrollOffset;
                }
                SizeChartToFit = !SizeChartToFit;
                UpdateSprite();
                if (!SizeChartToFit)
                {
                    HorizontalScrollOffset = _oldHorizontalScrollOffset;
                }
            }
        }

        public void ScrollToEnd()
        {
            HorizontalScrollOffset = new Vector2(Width - _chartWidth(), 0);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var distance = _touchHeldLocation.Position.X - _previoustouchHeldLocation.Position.X;
            var dt = gameTime.ElapsedGameTime;

            if ((int)distance != 0)
            {
                _scrollingVelocity = Math.Abs(distance / dt.TotalMilliseconds);
            }
            if (distance > 0)
            {
                _scrollingDirection = 1;
            }
            else if (distance < 0)
            {
                _scrollingDirection = -1;
            }
            if ((int)distance == 0 && _scrollingVelocity > 0)
            {
                distance = (float)(_scrollingDirection * _scrollingVelocity * dt.TotalMilliseconds);
                _scrollingVelocity -= decceleration * dt.TotalMilliseconds;
                if (_scrollingVelocity < 0)
                {
                    _scrollingVelocity = 0;
                }
            }
            HorizontalScrollOffset += new Vector2((int)distance, 0);
            if (HorizontalScrollOffset.X > 0)
            {
                HorizontalScrollOffset = Vector2.Zero;
            }
            else if (HorizontalScrollOffset.X < Width - _chartWidth())
            {
                HorizontalScrollOffset = new Vector2(Width - _chartWidth(), 0);
            }

            if (_target == null || _target.IsDisposed || _target.IsContentLost ||
                _verticalAxisTarget == null || _verticalAxisTarget.IsDisposed || _verticalAxisTarget.IsContentLost)
            {
                UpdateSprite();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (Anchor == Game.CurrentRenderingGroup &&
			    IsVisible)
            {
                Game.SpriteBatch.End();
                var origClippingRectangle = Game.SpriteBatch.GraphicsDevice.ScissorRectangle;
                //we need to create a new sprite batch instance that is going to use a clipping rectangle
                Game.SpriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle((int)(ScaleMatrix.M41 + Position.X * ScaleMatrix.M11),
                                                                     (int)(ScaleMatrix.M42 + Position.Y * ScaleMatrix.M22),
                                                                     (int)(Width * ScaleMatrix.M11),
                                                                     (int)(Height * ScaleMatrix.M22));
                //Game.SpriteBatch.GraphicsDevice.RasterizerState.ScissorTestEnable = true;
                Game.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, new RasterizerState { ScissorTestEnable = true }, null, ScaleMatrix);
                Game.SpriteBatch.Draw(_target, Position + HorizontalScrollOffset);
                Game.SpriteBatch.End();
                //Game.SpriteBatch.GraphicsDevice.RasterizerState.ScissorTestEnable = false;
                Game.SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScaleMatrix);
                Game.SpriteBatch.Draw(_verticalAxisTarget, Position);
            }
            base.Draw(gameTime);
        }
    }
}

