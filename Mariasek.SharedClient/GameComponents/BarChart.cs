using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public class BarChart : TouchControlBase
    {
        private RenderTarget2D _verticalAxisTarget;
        private RenderTarget2D _target;
        public int Width { get; set; }
        public int Height { get; set; }
        private float[][] _data;
        public float[][] Data
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
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public float LineThickness { get; set; }
        public float AxisThickness { get; set; }
        //private float _dataMarkerSize;
        //public float DataMarkerSize { get; set; }
        //public DataMarkerShape DataMarkerShape { get; set; }

        public BarChart(GameComponent parent)
            : base(parent)
        {
            MinValue = 0f;
            MaxValue = 1f;
            _data = new[]
            {
                new [] { 0.25f, 0.5f, 0.25f },
                new [] { 0.1f, 0.2f, 0.75f },
                new [] { 0.5f, 0.75f, 0.5f }
            };
            Series = new[] { "Set1", "Set2", "Set3" };
            Colors = new[] { Color.Red, Color.Green, Color.Blue };
            AxisColor = Color.White;
            Width = 100;
            Height = 100;
            Opacity = 1f;
            LineThickness = 1f;
            AxisThickness = 1f;
            //DataMarkerSize = 6f;
            //DataMarkerShape = DataMarkerShape.Circle;
            XAxisPoint = 0f;
            YAxisPoint = 0f;
            ShowXAxis = true;
            ShowYAxis = true;
            TickMarkLength = 5f;
            ShowXAxisTickMarks = true;
            ShowYAxisTickMarks = true;
            ShowHorizontalGridLines = true;
            ShowVerticalGridLines = true;
            GridInterval = new Vector2(1f, 0.25f);
            HorizontalGridLineColor = Color.Gray;
            VerticalGridLineColor = Color.Gray;

            UpdateSprite();
            //Game.Activated += (sender, e) => ScheduleSpriteUpdate();
            Game.GraphicsDevice.DeviceReset += (sender, e) => ScheduleSpriteUpdate();
            Game.Resumed += () => ScheduleSpriteUpdate();
        }

        private void ScheduleSpriteUpdate()
        {
            this.Invoke(() => UpdateSprite());
        }

        private Vector2 LogicalToPhysical(Vector2 point)
        {
            var logicalWidth = Data?.Length * Data?[0]?.Length ?? 0;
            var logicalHeight = MaxValue - MinValue;

            if (logicalWidth == 0 || logicalHeight == 0)
            {
                return Vector2.Zero;
            }
            
            var result =
                Vector2.UnitY * Height +
                new Vector2((point.X - 0) * (Width - TickMarkLength) / logicalWidth + TickMarkLength / 2,
                            -(point.Y - MinValue) * (Height - TickMarkLength) / logicalHeight - TickMarkLength / 2);

            return result;
        }

        private void UpdateSprite()
        {
            //Draw onto the target rather than the back buffer
            _target = new RenderTarget2D(Game.SpriteBatch.GraphicsDevice, Width, Height);
            var physicalWidth = (int)Math.Max(1, LogicalToPhysical(new Vector2(TickMarkLength, 0)).X);
            _verticalAxisTarget = new RenderTarget2D(Game.SpriteBatch.GraphicsDevice, physicalWidth, Height);
            HorizontalScrollOffset = Vector2.Zero;

            var maxx = Data?.Length * Data?[0]?.Length ?? 0;
            if (ShowYAxis)
            {
                Game.SpriteBatch.GraphicsDevice.SetRenderTarget(_verticalAxisTarget);
                Game.SpriteBatch.GraphicsDevice.Clear(Color.Transparent);
                Game.SpriteBatch.Begin();

                Primitives2D.DrawLine(Game.SpriteBatch, LogicalToPhysical(new Vector2(YAxisPoint, MinValue)), LogicalToPhysical(new Vector2(YAxisPoint, MaxValue)), AxisColor, AxisThickness, Opacity);

                if (ShowYAxisTickMarks && LogicalToPhysical(Vector2.Zero).Y - LogicalToPhysical(new Vector2(0, GridInterval.Y)).Y >= 2)
                {
                    var offset = MinValue % GridInterval.Y;

                    for (int i = 0; MinValue + i * GridInterval.Y < MaxValue; i++)
                    {
                        var tickCentre = LogicalToPhysical(new Vector2(YAxisPoint, MinValue + i * GridInterval.Y - offset));
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
                var offset = MinValue % GridInterval.Y;

                for (int i = 0; MinValue + i * GridInterval.Y < MaxValue; i++)
                {
                    var lineStart = LogicalToPhysical(new Vector2(0, MinValue + i * GridInterval.Y - offset));
                    var lineEnd = LogicalToPhysical(new Vector2(maxx, MinValue + i * GridInterval.Y - offset));

                    Primitives2D.DrawLine(Game.SpriteBatch, lineStart, lineEnd, HorizontalGridLineColor, 1f, Opacity);
                }
            }
            if (ShowVerticalGridLines)
            {
                for (int i = 0; i * GridInterval.X <= maxx; i++)
                {
                    var lineStart = LogicalToPhysical(new Vector2(i * GridInterval.X, MinValue));
                    var lineEnd = LogicalToPhysical(new Vector2(i * GridInterval.X, MaxValue));

                    Primitives2D.DrawLine(Game.SpriteBatch, lineStart, lineEnd, VerticalGridLineColor, 1f, Opacity);
                }
            }
            var numSeries = Data.Length;

            for (var i = 0; i < numSeries; i++)
            {
                if (Data[i].Length == 0)
                {
                    continue;
                }
                var dataLength = Data[i].Length;
                var points = new Vector2[Data[i].Length];
                var zeros = new Vector2[Data[i].Length];
                var barWidth = LogicalToPhysical(new Vector2(1, 0)).X;

                for (var j = 0; j < dataLength; j++)
                {
                    if (Data[i][j] > 0)
                    {
                        points[j] = LogicalToPhysical(new Vector2(j * numSeries + i, Data[i][j]));
                        zeros[j] = LogicalToPhysical(new Vector2(j * numSeries + i + 1, 0));

                        Primitives2D.FillRectangle(Game.SpriteBatch, points[j], zeros[j] - points[j], Colors[i] * Opacity);
                    }
                    else
                    {
                        zeros[j] = LogicalToPhysical(new Vector2(j * numSeries + i, 0));
                        points[j] = LogicalToPhysical(new Vector2(j * numSeries + i + 1, Data[i][j]));

                        Primitives2D.FillRectangle(Game.SpriteBatch, zeros[j], points[j] - zeros[j], Colors[i] * Opacity);
                    }
                }
            }
            if (ShowXAxis)
            {
                Primitives2D.DrawLine(Game.SpriteBatch, LogicalToPhysical(new Vector2(0, XAxisPoint)), LogicalToPhysical(new Vector2(maxx, XAxisPoint)), AxisColor, AxisThickness, Opacity);
                if (ShowXAxisTickMarks && LogicalToPhysical(new Vector2(GridInterval.X, 0)).X - LogicalToPhysical(Vector2.Zero).X >= 8)
                {
                    for (int i = 0; i < maxx; i++)
                    {
                        var tickCentre = LogicalToPhysical(new Vector2(i * GridInterval.X + 0.5f, XAxisPoint));
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
        private const float decceleration = 0.002f;

        protected override void OnTouchDown(TouchLocation tl)
        {
            _touchDownLocation = tl;
            _touchHeldLocation = tl;
            base.OnTouchDown(tl);
        }

        protected override void OnTouchUp(TouchLocation tl)
        {
            _previoustouchHeldLocation = _touchHeldLocation;
            base.OnTouchUp(tl);
        }

        protected override bool OnTouchHeld(float touchHeldTimeMs, TouchLocation tl)
        {
            _previoustouchHeldLocation = _touchHeldLocation;
            _touchHeldLocation = tl;
            return base.OnTouchHeld(touchHeldTimeMs, tl);
        }

        public void UpdateMinMaxValues()
        {
            var maxValue = float.MinValue;
            var minValue = float.MaxValue;

            for (var i = 0; i < Data.Length; i++)
            {
                for (var j = 0; j < Data[i].Length; j++)
                {
                    if (maxValue < Data[i][j])
                    {
                        maxValue = Data[i][j];
                    }
                    if (minValue > Data[i][j])
                    {
                        minValue = Data[i][j];
                    }
                }
            }
            if (maxValue < 1)
            {
                maxValue = 1;
            }
            if (minValue > -1)
            {
                minValue = -1;
            }
            MaxValue = maxValue;
            MinValue = minValue;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

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
                Game.SpriteBatch.Draw(_target, Position + HorizontalScrollOffset, Color.White);
                Game.SpriteBatch.End();
                //Game.SpriteBatch.GraphicsDevice.RasterizerState.ScissorTestEnable = false;
                Game.SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScaleMatrix);
                Game.SpriteBatch.Draw(_verticalAxisTarget, Position, Color.White);
            }
            base.Draw(gameTime);
        }
    }
}

