using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
//using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace Mariasek.SharedClient.GameComponents
{
    public class ProgressIndicator : GameComponent
    {
        private RectangleShape _backgroundShape;
        private RectangleShape _progressBar;
        private int _progress;
        private int _maxProgressWidth;

        public override Vector2 Position
        {
            get { return _backgroundShape.Position; }
            set 
            { 
                _backgroundShape.Position = value; 
                _progressBar.Position = Vector2.Add(value, new Vector2(_backgroundShape.BorderThickness / 2f, _backgroundShape.BorderThickness / 2f));
            }
        }
        public int Width 
        { 
            get { return _backgroundShape.Width; }
            set 
            { 
                _backgroundShape.Width = value; 
                _maxProgressWidth = value - _backgroundShape.BorderThickness;
                _progressBar.Width = GetProgressBarWidth(_progress);
                //_progressBar.Position = Position;
            } 
        }
        public int Height
        { 
            get { return _backgroundShape.Height; }
            set 
            { 
                _backgroundShape.Height = value; 
                _progressBar.Height = value - _backgroundShape.BorderThickness;
                //_progressBar.Position = Position;
            } 
        }
        public int Min { get; set; }
        public int Max { get; set; }
        public int Progress
        { 
            get { return _progress; }
            set
            {
                if (value < Min)
                {
                    value = Min;
                }
                else if (value > Max)
                {
                    value = Max;
                }
                _progress = value;
                _progressBar.Width = GetProgressBarWidth(value);
            }
        }

        public ProgressIndicator(GameComponent parent)
            :base (parent)
        {
            _backgroundShape = new RectangleShape(this)
            {
                BackgroundColors = new List<Color> { Color.DimGray },
                BorderColors = new List<Color> { Color.Black },
                BorderRadius = 0,
                BorderThickness = 3,
                Opacity = 0.7f
            };
            _progressBar = new RectangleShape(this)
            {
                BackgroundColors = new List<Color> { Color.Yellow },
                BorderColors = new List<Color> { Color.Yellow },
                BorderRadius = 0,
                BorderThickness = 0,
                Opacity = 1f
            };
            Min = 0;
            Max = 100;
        }
            
        private int GetProgressBarWidth(int progress)
        {
            if (Max == 0 || Max - Min == 0)
            {
                return Width / 3;
            }

            return progress * _maxProgressWidth / (Max - Min);
        }
    }
}

