﻿using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mariasek.SharedClient.GameComponents
{
    public class RectangleShape : GameComponent
    {
        private volatile bool _textureUpdateNeeded;
        private Texture2D _texture;
        private int _width = 100;
        private int _height = 50;
        private int _borderThickness = 1;
        private int _borderRadius = 3;
        private int _borderShadow = 0;
        private List<Color> _backgroundColors = new List<Color>();
        private List<Color> _borderColors = new List<Color>();
        private float _initialShadowIntensity = 0.5f;
        private float _finalShadowIntensity = 0.1f;

        public int Width
        {
            get { return _width; }
            set
            {
                _width = value;
                _textureUpdateNeeded = true;
            }
        }

        public int Height
        {
            get { return _height; }
            set
            {
                _height = value;
                _textureUpdateNeeded = true;
            }
        }

        public int BorderThickness
        {
            get { return _borderThickness; }
            set
            {
                _borderThickness = value;
                _textureUpdateNeeded = true;
            }
        }

        public int BorderRadius
        {
            get { return _borderRadius; }
            set
            {
                _borderRadius = value;
                _textureUpdateNeeded = true;
            }
        }

        public int BorderShadow
        {
            get { return _borderShadow; }
            set
            {
                _borderShadow = value;
                _textureUpdateNeeded = true;
            }
        }

        public List<Color> BackgroundColors
        {
            get { return _backgroundColors; }
            set
            {
                _backgroundColors = value;
                _textureUpdateNeeded = true;
            }
        }

        public List<Color> BorderColors
        {
            get { return _borderColors; }
            set
            {
                _borderColors = value;
                _textureUpdateNeeded = true;
            }
        }

        public float InitialShadowIntensity
        {
            get { return _initialShadowIntensity; }
            set
            {
                _initialShadowIntensity = value;
                _textureUpdateNeeded = true;
            }
        }

        public float FinalShadowIntensity
        {
            get { return _finalShadowIntensity; }
            set
            {
                _finalShadowIntensity = value;
                _textureUpdateNeeded = true;
            }
        }

        public RectangleShape(GameComponent parent)
            :base (parent)
        {
            Game.Activated += (sender, e) => ScheduleTextureUpdate();
            Game.GraphicsDevice.DeviceReset += (sender, e) => ScheduleTextureUpdate();
            //Game.Resumed += () => ScheduleTextureUpdate();
        }

        private void ScheduleTextureUpdate()
        {
            _textureUpdateNeeded = true;
        }

        public void UpdateTexture()
        {
            if (_texture != null && !_texture.IsDisposed)
            {
                _texture.Dispose();
				_texture = null;
			}
            try
            {
                if (_width > 0 && _height > 0)
                {
                    _texture = CreateRoundedRectangleTexture(
                    Game.GraphicsDevice,
                    _width,
                    _height,
                    _borderThickness,
                    _borderRadius,
                    _borderShadow,
                    _backgroundColors,
                    _borderColors,
                    _initialShadowIntensity,
                    _finalShadowIntensity
                    );
                }
                _textureUpdateNeeded = false;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create rounded rectangle texture: {ex.Message}");
                _texture = null;
            }
        }

        private Texture2D CreateRoundedRectangleTexture(GraphicsDevice graphics, int width, int height, int borderThickness, int borderRadius, int borderShadow, List<Color> backgroundColors, List<Color> borderColors, float initialShadowIntensity, float finalShadowIntensity)
        {
            if (backgroundColors == null || backgroundColors.Count == 0) throw new ArgumentException("Must define at least one background color (up to four).");
            if (borderColors == null || borderColors.Count == 0) throw new ArgumentException("Must define at least one border color (up to three).");
            if (borderThickness + borderRadius > height / 2 || borderThickness + borderRadius > width / 2) throw new ArgumentException("Border will be too thick and/or rounded to fit on the texture.");
            if (borderShadow > borderRadius) throw new ArgumentException("Border shadow must be lesser in magnitude than the border radius (suggeted: shadow <= 0.25 * radius).");

            Texture2D texture = new Texture2D(graphics, width, height, false, SurfaceFormat.Color);
            Color[] color = new Color[width * height];

            for (int x = 0; x < texture.Width; x++)
            {
                for (int y = 0; y < texture.Height; y++)
                {
                    switch (backgroundColors.Count)
                    {
                        case 4:
                            Color leftColor0 = Color.Lerp(backgroundColors[0], backgroundColors[1], ((float)y / (width - 1)));
                            Color rightColor0 = Color.Lerp(backgroundColors[2], backgroundColors[3], ((float)y / (height - 1)));
                            color[x + width * y] = Color.Lerp(leftColor0, rightColor0, ((float)x / (width - 1)));
                            break;
                        case 3:
                            Color leftColor1 = Color.Lerp(backgroundColors[0], backgroundColors[1], ((float)y / (width - 1)));
                            Color rightColor1 = Color.Lerp(backgroundColors[1], backgroundColors[2], ((float)y / (height - 1)));
                            color[x + width * y] = Color.Lerp(leftColor1, rightColor1, ((float)x / (width - 1)));
                            break;
                        case 2:
                            color[x + width * y] = Color.Lerp(backgroundColors[0], backgroundColors[1], ((float)x / (width - 1)));
                            break;
                        default:
                            color[x + width * y] = backgroundColors[0];
                            break;
                    }

                    color[x + width * y] = ColorBorder(x, y, width, height, borderThickness, borderRadius, borderShadow, color[x + width * y], borderColors, initialShadowIntensity, finalShadowIntensity);
                }
            }

            texture.SetData<Color>(color);
            return texture;
        }

        private Color ColorBorder(int x, int y, int width, int height, int borderThickness, int borderRadius, int borderShadow, Color initialColor, List<Color> borderColors, float initialShadowIntensity, float finalShadowIntensity)
        {
            Rectangle internalRectangle = new Rectangle((borderThickness + borderRadius), (borderThickness + borderRadius), width - 2 * (borderThickness + borderRadius), height - 2 * (borderThickness + borderRadius));

            if (internalRectangle.Contains(x, y)) return initialColor;

            Vector2 origin = Vector2.Zero;
            Vector2 point = new Vector2(x, y);
            float alpha = 1f;
            var antialiasingWidth = 1f;

            if (x < borderThickness + borderRadius)
            {
                if (y < borderRadius + borderThickness)
                {
                    origin = new Vector2(borderRadius + borderThickness, borderRadius + borderThickness);
                    alpha = 0f;
                }
                else if (y >= height - (borderRadius + borderThickness))
                {
                    origin = new Vector2(borderRadius + borderThickness, height - (borderRadius + borderThickness) - 1);
                    alpha = 0f;
                }
                else
                    origin = new Vector2(borderRadius + borderThickness, y);
            }
            else if (x >= width - (borderRadius + borderThickness))
            {
                if (y < borderRadius + borderThickness)
                {
                    origin = new Vector2(width - (borderRadius + borderThickness) - 1, borderRadius + borderThickness);
                    alpha = 0f;
                }
                else if (y >= height - (borderRadius + borderThickness))
                {
                    origin = new Vector2(width - (borderRadius + borderThickness) - 1, height - (borderRadius + borderThickness) - 1);
                    alpha = 0f;
                }
                else
                    origin = new Vector2(width - (borderRadius + borderThickness) - 1, y);
            }
            else
            {
                if (y < borderRadius + borderThickness)
                    origin = new Vector2(x, borderRadius + borderThickness);
                else if (y >= height - (borderRadius + borderThickness))
                    origin = new Vector2(x, height - (borderRadius + borderThickness) - 1);
            }

            if (!origin.Equals(Vector2.Zero))
            {
                float distance = Vector2.Distance(point, origin);

                if (distance > borderRadius + borderThickness + antialiasingWidth)
                {
                    return Color.Transparent;
                }
                else if (distance > borderRadius)
                {
                    //compute antialiasing for rounded corners
                    if (alpha < 1f)
                    {
                        alpha = distance >= borderRadius + borderThickness ? (antialiasingWidth - (distance - (borderRadius + borderThickness))) / antialiasingWidth : 1;
                    }

                    if (borderColors.Count > 2)
                    {
                        float modNum = distance - borderRadius;

                        if (modNum < borderThickness / 2)
                        {
                            return Color.Lerp(borderColors[2], borderColors[1], (float)((modNum) / (borderThickness / 2.0))) * alpha;
                        }
                        else
                        {
                            return Color.Lerp(borderColors[1], borderColors[0], (float)((modNum - (borderThickness / 2.0)) / (borderThickness / 2.0))) * alpha;
                        }
                    }


                    if (borderColors.Count > 0)
                        return borderColors[0] * alpha;
                }
                else if (distance > borderRadius - borderShadow)
                {
                    float mod = (distance - (borderRadius - borderShadow)) / borderShadow;
                    float shadowDiff = initialShadowIntensity - finalShadowIntensity;
                    return DarkenColor(initialColor, ((shadowDiff * mod) + finalShadowIntensity));
                }
            }

            return initialColor;
        }

        private Color DarkenColor(Color color, float shadowIntensity)
        {
            return Color.Lerp(color, Color.Black, shadowIntensity);
        }

        public override void Update(GameTime gameTime)
        {
            if (_textureUpdateNeeded || _texture == null || _texture.IsDisposed)
            {
                UpdateTexture();
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
			if (IsVisible &&
			    Anchor == Game.CurrentRenderingGroup &&
			    _texture != null)
            {
                Game.SpriteBatch.Draw(_texture, Position, Color.White * Opacity);
            }
            base.Draw(gameTime);
        }
    }
}

