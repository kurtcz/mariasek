﻿using System;
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

    public class Tab
    {
        public float TabPosition { get; set; }
        public HorizontalAlignment TabAlignment { get; set; }
    }

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
                var fontTextures = fontBitmapFiles.Select(i => game.Assets.GetTexture(i) ?? new Texture2D(game.GraphicsDevice, 1, 1)).ToArray();

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

        public Rectangle GetBoundsRect(string[] lines, float scaleFactor, List<int> wordWidths = null)
        {
            var width = 0f;
            var height = 0f;
            var wordWidth = 0;
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
                        if (!char.IsWhiteSpace(c))
                        {
                            wordWidth += fc.XAdvance;
                        }
                        else if (wordWidths != null && wordWidth > 0)
                        {
                            wordWidths.Add((int)Math.Ceiling(wordWidth * scaleFactor));
                            wordWidth = 0;
                        }
                        linewidth += fc.XAdvance;
                    }
                    else if (c == '\t' && wordWidths != null && wordWidth > 0)
                    {
                        wordWidths.Add((int)Math.Ceiling(wordWidth * scaleFactor));
                        wordWidth = 0;
                    }
                }
                if (wordWidths != null && wordWidth > 0)
                {
                    wordWidths.Add((int)Math.Ceiling(wordWidth * scaleFactor));
                    wordWidth = 0;
                }
                height += lineheight + _lineSpacing;
                width = Math.Max(linewidth, width);
                linewidth = 0;
            }

            return new Rectangle(0, 0, (int)Math.Ceiling(width * scaleFactor), (int)Math.Ceiling(height * scaleFactor));
        }

        public float LineHeightAndSpacing
        {
            get
            {
                FontChar fc;

                if (_characterMap.TryGetValue('X', out fc))
                {
                    return fc.YOffset + fc.Height + _lineSpacing;
                }

                return 0f;
            }
        }

        public Rectangle DrawText(SpriteBatch spriteBatch, string text, Vector2 position, float scaleFactor, Color color, Alignment alignment = Alignment.TopLeft, Tab[] tabs = null, bool renderOffscreen = true)
        {
            return DrawText(spriteBatch, text, position, scaleFactor, new[] { color }, alignment, tabs, renderOffscreen);
        }

        public Rectangle DrawText(SpriteBatch spriteBatch, string text, Vector2 position, float scaleFactor, Color[] colors, Alignment alignment = Alignment.TopLeft, Tab[] tabs = null, bool renderOffscreen = true)
        {
            var lineSeparators = new[] { '\r', '\n' };
            var lines = text.Split(lineSeparators);

            return DrawText(spriteBatch, lines, position, scaleFactor, colors, alignment, tabs, renderOffscreen);
        }

        public Rectangle DrawText(SpriteBatch spriteBatch, string[] lines, Vector2 position, float scaleFactor, Color[] colors, Alignment alignment = Alignment.TopLeft, Tab[] tabs = null, bool renderOffscreen = true)
        {
            var lineHeight = 0;
            var dx = (double)position.X;
            var dy = (double)position.Y;

            FontChar fc;
            if (_characterMap.TryGetValue('X', out fc))
            {
                lineHeight = fc.Height + fc.YOffset;
            }
            if (((VerticalAlignment)alignment & VerticalAlignment.Bottom) != 0)
            {
                dy -= (lineHeight * lines.Length + _lineSpacing * (lines.Length - 1)) * scaleFactor;
            }
            else if (((VerticalAlignment)alignment & VerticalAlignment.Middle) != 0)
            {
                dy -= (lineHeight * lines.Length + _lineSpacing * (lines.Length - 1)) * scaleFactor / 2f;
            }

            var screenRect = new Rectangle(0, 0, (int)_game.VirtualScreenWidth, (int)_game.VirtualScreenHeight);
            var lineNumber = 0;
            var maxWidth = 0;

            foreach (var line in lines)
            {
                //nezdrzovat se s vykreslovanim radku mimo obrazovku
                if (renderOffscreen || (dy + lineHeight >= screenRect.Top && dy <= screenRect.Bottom))
                {
                    var wordWidths = new List<int>();
                    var lineRect = GetBoundsRect(new[] { line }, scaleFactor, wordWidths);

                    maxWidth = Math.Max(maxWidth, lineRect.Width);
                    for (var i = 0; i < wordWidths.Count; i++)
                    {
                        wordWidths[i] = (int)wordWidths[i];
                    }
                    if (((HorizontalAlignment)alignment & HorizontalAlignment.Right) != 0)
                    {
                        dx -= lineRect.Width;
                    }
                    else if (((HorizontalAlignment)alignment & HorizontalAlignment.Center) != 0)
                    {
                        dx -= lineRect.Width / 2f;
                    }
                    var wordNumber = 0;
                    var tabNumber = 0;
                    var prev = ' ';

                    //nezdrzovat se s vykreslovanim radku mimo obrazovku
                    if (renderOffscreen ||
                        screenRect.Intersects(
                        new Rectangle((int)dx, (int)dy, lineRect.Width, lineRect.Height)))
                    {
                        foreach (char c in line)
                        {
                            if (_characterMap.TryGetValue(c, out fc))
                            {
                                var sourceRectangle = new Rectangle(fc.X, fc.Y, fc.Width, fc.Height);
                                var charPosition = new Vector2((float)dx + fc.XOffset * scaleFactor, (float)dy + fc.YOffset * scaleFactor);
                                var color = colors != null && colors.Length > 0
                                            ? colors[lineNumber % colors.Length]
                                            : Color.White;

                                //spriteBatch.Draw(_textures[fc.Page], charPosition, sourceRectangle, color);
                                spriteBatch.Draw(_textures[fc.Page], charPosition, sourceRectangle, color, 0f, Vector2.Zero, scaleFactor, SpriteEffects.None, 0);
                                //lineHeight = Math.Max(fc.YOffset + fc.Height, lineHeight);
                                dx += fc.XAdvance * scaleFactor;
                                if (char.IsWhiteSpace(c) && !char.IsWhiteSpace(prev))
                                {
                                    wordNumber++;
                                }
                            }
                            else if (c == '\t')
                            {
                                if (!char.IsWhiteSpace(prev))
                                {
                                    wordNumber++;
                                }
                                _characterMap.TryGetValue('X', out fc);

                                var tabWidth = _tabWidthChars * fc.XAdvance * scaleFactor;

                                if (tabs == null || tabNumber >= tabs.Length)   //simple tabs
                                {
                                    var nextTab = dx + tabWidth - dx % tabWidth;

                                    dx = nextTab;
                                }
                                else                                            //explicit tabs
                                {
                                    var nextTab = tabs[tabNumber].TabPosition;

                                    switch (tabs[tabNumber].TabAlignment)
                                    {
                                        case HorizontalAlignment.Left:
                                            dx = nextTab;
                                            break;
                                        case HorizontalAlignment.Center:
                                            dx = nextTab - wordWidths[wordNumber] / 2f;
                                            break;
                                        case HorizontalAlignment.Right:
                                            dx = nextTab - wordWidths[wordNumber];
                                            break;
                                    }
                                    tabNumber++;
                                }
                            }
                            prev = c;
                        }
                    }
                    dx = position.X;
                }
                var scaledLine = (lineHeight + _lineSpacing) * scaleFactor;

                dy += scaledLine;
                lineNumber++;
            }

            var boundsRect = new Rectangle(0, 0, maxWidth, (int)((lineHeight * lines.Length + _lineSpacing * (lines.Length - 1)) * scaleFactor));

            return boundsRect;
        }
    }
}
