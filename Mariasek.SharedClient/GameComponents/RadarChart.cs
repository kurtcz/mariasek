using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public class RadarChart : GameComponent
    {
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
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public string[] Series { get; set; }
        public bool ShowAxis { get; set; }
        public bool ShowGridLines { get; set; }
        public float GridInterval { get; set; }
        public Color AxisColor { get; set; }
        public Color[] Colors { get; set; }
        public float LineThickness { get; set; }
        public float AxisThickness { get; set; }
        public float DataMarkerSize { get; set; }
        public DataMarkerShape DataMarkerShape { get; set; }
        public virtual FontRenderer TextRenderer { get; set; }

        public RadarChart(GameComponent parent)
            : base(parent)
        {
            MinValue = 0f;
            MaxValue = 1f;
            Series = new[] { "Set1", "Set2", "Set3" };
            Colors = new[] { Color.Red, Color.Green, Color.Blue };
            AxisColor = Color.White;
            Width = 100;
            Height = 100;
            Opacity = 1f;
            LineThickness = 2f;
            AxisThickness = 1f;
            DataMarkerSize = 20f;
            DataMarkerShape = DataMarkerShape.Square;
            ShowAxis = true;
            ShowGridLines = true;
            GridInterval = 0.25f;
            TextRenderer = Game.FontRenderers["BMFont"];
        }

        private void UpdateSprite()
        {
            //Draw onto the target rather than the back buffer
            _target = new RenderTarget2D(Game.SpriteBatch.GraphicsDevice, Width, Height);

            Game.SpriteBatch.GraphicsDevice.SetRenderTarget(_target);
            Game.SpriteBatch.GraphicsDevice.Clear(Color.Transparent);
            Game.SpriteBatch.Begin();

            if (MaxValue > MinValue)
            {
                var points = new Vector2[Series.Length][];
                var radius = Math.Min(Width, Height) / 2;
                var centre = new Vector2(Width, Height) / 2;

                for (var i = 0; i < Series.Length; i++)
                {
                    if (i == 0 && ShowAxis)
                    {
                        for (var j = 0; j < Data[i].Length; j++)
                        {
                            var angle = Math.PI * 2 / Data[i].Length;
                            var angleVector = - new Vector2((float)Math.Cos(j * angle), (float)Math.Sin(j * angle));

                            Primitives2D.DrawLine(Game.SpriteBatch, centre, centre + radius * angleVector, AxisColor, AxisThickness, Opacity);
                        }
                    }
                    if (i == 0 && ShowGridLines)
                    {
                        for (var j = 0f; j <= 1; j += GridInterval)
                        {
                            if (j * GridInterval >= MinValue && j * GridInterval <= MaxValue)
                            {
                                for (var k = 0; k < Data[i].Length; k++)
                                {
                                    var l = k == Data[i].Length - 1 ? 0 : k + 1;
                                    var angle = Math.PI * 2 / Data[i].Length;
                                    var distance = (j - MinValue) * radius / (MaxValue - MinValue);
                                    var angleVector1 = -new Vector2((float)Math.Cos(k * angle), (float)Math.Sin(k * angle));
                                    var angleVector2 = -new Vector2((float)Math.Cos(l * angle), (float)Math.Sin(l * angle));

                                    if (distance < 0 || distance > radius)
                                    {
                                        break;
                                    }
                                    Primitives2D.DrawLine(Game.SpriteBatch, centre + distance * angleVector1, centre + distance * angleVector2, AxisColor, AxisThickness, Opacity * 0.5f);
                                }
                            }
                        }
                    }
                    if (Series.Length > 0 && Data[i].Length > 0)
                    {
                        var outOfBounds = false;
                        points[i] = new Vector2[Data[i].Length];
                        for (var j = 0; j < Data[i].Length; j++)
                        {
                            var distance = (Data[i][j] - MinValue) * radius / (MaxValue - MinValue);
                            var angle = Math.PI * 2 / Data[i].Length;
                            var angleVector = -new Vector2((float)Math.Cos(j * angle), (float)Math.Sin(j * angle));

                            if (distance < 0 || distance > radius)
                            {
                                outOfBounds = true;
                                break;
                            }
                            points[i][j] = distance * angleVector;
                        }
                        if (outOfBounds)
                        {
                            break;
                        }
                        Primitives2D.DrawPolygon(Game.SpriteBatch, centre, points[i].ToList(), Colors[i], LineThickness, Opacity);
                    }
                    if (DataMarkerSize > 0 && points[i] != null)
                    {
                        for (var j = 0; j < points[i].Length; j++)
                        {
                            Primitives2D.DrawCircle(Game.SpriteBatch, centre + points[i][j], DataMarkerSize / 2, (int)DataMarkerShape, Colors[i], DataMarkerSize / 2, Opacity);
                        }
                    }
                }
            }
            Game.SpriteBatch.End();
            //restore the original backbuffer as the render target
            Game.SpriteBatch.GraphicsDevice.SetRenderTarget(null);
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
                Game.SpriteBatch.Draw(_target, Position, Color.White);
            }
            base.Draw(gameTime);
        }
    }
}
