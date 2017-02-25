using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Mariasek.SharedClient.BmFont;

namespace Mariasek.SharedClient.GameComponents
{
    public enum HorizontalAlignment
    {
        Left = 1,
        Center = 2,
        Right = 4
    }
    public enum VerticalAlignment
    {
        Top = 8,
        Middle = 16,
        Bottom = 32
    }
    public enum Alignment
    {
        TopLeft = HorizontalAlignment.Left | VerticalAlignment.Top,
        TopCenter = HorizontalAlignment.Center | VerticalAlignment.Top,
        TopRight = HorizontalAlignment.Right | VerticalAlignment.Top,
        MiddleLeft = HorizontalAlignment.Left | VerticalAlignment.Middle,
        MiddleCenter = HorizontalAlignment.Center | VerticalAlignment.Middle,
        MiddleRight = HorizontalAlignment.Right | VerticalAlignment.Middle,
        BottomLeft = HorizontalAlignment.Left | VerticalAlignment.Bottom,
        BottomCenter = HorizontalAlignment.Center | VerticalAlignment.Bottom,
        BottomRight = HorizontalAlignment.Right | VerticalAlignment.Bottom,
    };

    public class FontRenderer
    {
        private Dictionary<char, FontChar> _characterMap;
        private FontFile _fontFile;
        private const float DefaultLineSpacing = 5f;
        private const float DefaultTabWidthChars = 5f;
        private float _lineSpacing;
        private float _tabWidthChars;
        private Texture2D[] _textures;
        private MariasekMonoGame _game;
        public static FontRenderer[] Fonts { get; private set; }

        public static FontRenderer GetFontRenderer(MariasekMonoGame game, string fontDescriptorFile, params string[] fontBitmapFiles)
        {
            return GetFontRenderer(game, DefaultLineSpacing, DefaultTabWidthChars, fontDescriptorFile, fontBitmapFiles);
        }

        public static FontRenderer GetFontRenderer(MariasekMonoGame game, float lineSpacing, float tabWidthChars, string fontDescriptorFile, params string[] fontBitmapFiles)
        {
            var fontFilePath = Path.Combine(game.Content.RootDirectory, fontDescriptorFile);
            FontRenderer textRenderer;
            using(var stream = TitleContainer.OpenStream(fontFilePath))
            {
                var fontFile = FontLoader.Load(stream);
                var fontTextures = fontBitmapFiles.Select(i => game.Content.Load<Texture2D>(i)).ToArray();

                textRenderer = new FontRenderer(game, fontFile, fontTextures, lineSpacing, tabWidthChars);
                stream.Close();

                return textRenderer;
            }
        }

        public FontRenderer (MariasekMonoGame game, FontFile fontFile, Texture2D[] fontTextures, float lineSpacing = DefaultLineSpacing, float tabWidthChars = DefaultTabWidthChars)
        {
            _game = game;
            _fontFile = fontFile;
            _textures = fontTextures;
            _lineSpacing = lineSpacing;
            _tabWidthChars = tabWidthChars;
            _characterMap = new Dictionary<char, FontChar>();

            foreach(var fontCharacter in _fontFile.Chars)
            {
                char c = (char)fontCharacter.ID;
                _characterMap.Add(c, fontCharacter);
            }
        }

        public Rectangle GetBoundsRect(string[] lines)
        {
            var width = 0f;
            var height = 0f;
            var linewidth = 0;
            var lineheight = 0;
            FontChar fc;

            if (_characterMap.TryGetValue('X', out fc))
            {
                lineheight = fc.Height + fc.YOffset;
            }
            foreach (var line in lines)
            {
                foreach (char c in line)
                {
                    if (_characterMap.TryGetValue(c, out fc))
                    {
                        linewidth += fc.XAdvance;
                    }
                }
                height += lineheight + _lineSpacing;
                width = Math.Max(linewidth, width);
                linewidth = 0;
            }

            return new Rectangle(0, 0, (int)Math.Ceiling(width), (int)Math.Ceiling(height));
        }

        public Rectangle DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color, Alignment alignment = Alignment.TopLeft)
        {
            var lineSeparators = new [] { '\r', '\n' };
            var lines = text.Split(lineSeparators);
            var lineHeight = 0;
            var dx = position.X;
            var dy = position.Y;

            var boundsRect = GetBoundsRect(lines);

            if (((VerticalAlignment)alignment & VerticalAlignment.Bottom) != 0)
            {
                dy -= boundsRect.Height;
            }
            else if (((VerticalAlignment)alignment & VerticalAlignment.Middle) != 0)
            {
                dy -= boundsRect.Height / 2f;
            }

            foreach (var line in lines)
            {
                var lineRect = GetBoundsRect(new [] { line });
                FontChar fc;

                if (((HorizontalAlignment)alignment & HorizontalAlignment.Right) != 0)
                {
                    dx -= lineRect.Width;
                }
                else if (((HorizontalAlignment)alignment & HorizontalAlignment.Center) != 0)
                {
                    dx -= lineRect.Width / 2f;
                }
                //vyska radku bude da nezavisle na textu
                if (_characterMap.TryGetValue('X', out fc))
                {
                    lineHeight = fc.YOffset + fc.Height;
                }
                foreach (char c in line)
                {
                    if (_characterMap.TryGetValue(c, out fc))
                    {
                        var sourceRectangle = new Rectangle(fc.X, fc.Y, fc.Width, fc.Height);
                        var charPosition = new Vector2(dx + fc.XOffset, dy + fc.YOffset);

                        spriteBatch.Draw(_textures[fc.Page], charPosition, sourceRectangle, color);
                        //lineHeight = Math.Max(fc.YOffset + fc.Height, lineHeight);
                        dx += fc.XAdvance;
                    }
                    else if (c == '\t')
                    {
                        _characterMap.TryGetValue('X', out fc);

                        var tabWidth = _tabWidthChars * fc.XAdvance;
                        var remainder = dx % tabWidth;
                        var nextTab = dx + tabWidth - remainder;

                        dx = nextTab;
                    }
                }
                dx = position.X;
                dy += lineHeight + _lineSpacing;
            }

            return boundsRect;
        }
    }
}
