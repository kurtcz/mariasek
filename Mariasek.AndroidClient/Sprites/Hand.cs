using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace Mariasek.AndroidClient.Sprites
{
    public class Hand
    {
        public Vector2 Centre
        {
            get { return _centre; }
            set
            {
                _centre = value;
                foreach (var sprite in _sprites)
                {
                    sprite.Position = _centre;
                }
            }
        }
        public bool IsStraight { get; private set; }
        public bool IsMoving { get { return _sprites.Any(i => i.IsVisible && i.IsMoving); }  }
        private Vector2 _centre;
        private Texture2D _cardTexture;
        private Texture2D _reverseTexture;

        private Sprite[] _sprites = new Sprite[12];

        const int CardWidth = 65;
        const int CardHeight = 112;

        public Hand(ContentManager content, IList spriteList)
        {
            _cardTexture = content.Load<Texture2D>("marias");
            _reverseTexture = content.Load<Texture2D>("revers");

            for(var i = 0; i < _sprites.Length; i++)
            {
                var rect = new Rectangle(4 + (i % 8) * 74, 5 + (i / 8) * 120, CardWidth, CardHeight);

                _sprites[i] = new Sprite(_cardTexture, rect);
                spriteList.Add(_sprites[i]);
            }
        }

        public void ShowArc(float arcAngle)
        {
            var hh = _sprites.Where(i => i.IsVisible).ToList();
            float angle0 = (float)Math.PI / 2;
            var r = 300f;

            var cardsToSkip = (12 - hh.Count) / 2;
            for(var i = 0; i < hh.Count; i++)
            {
                var angle = angle0 - arcAngle / hh.Count() * (i + cardsToSkip - hh.Count()/2.0f + 0.5f);
                float rotationAngle = -angle + (float)Math.PI / 2;
                //if(rotationAngle < 0)
                //    rotationAngle += (float)Math.PI * 2;
                var targetPosition = new Vector2(Centre.X + r * (float)Math.Cos(angle),
                                                 Centre.Y + r * 0.75f - r * (float)Math.Sin(angle));

                hh[i].MoveTo(targetPosition, 400);
                hh[i].RotateTo(rotationAngle, 2);
            }
            IsStraight = false;
        }

        public void ShowStraight(int width)
        {
            var hh = _sprites.Where(i => i.IsVisible).ToList();
//            var cardsToSkip = (_sprites.Length - hh.Count) / 2;
//
//            for(var i = 0; i < hh.Count; i++)
//            {
//                var x_offset = (i + cardsToSkip - _sprites.Length/2.0f + 0.5f) * CardWidth;
//                var y_offset = 0;
//
//                var targetPosition = new Vector2(Centre.X + x_offset, Centre.Y + y_offset);
//                var targetAngle = 0f;
//
//                hh[i].MoveTo(targetPosition);
//                hh[i].RotateTo(targetAngle);
//            }
            var cardsPerRow = 8;
            var padding = 10;
            for(var i = 0; i < hh.Count; i++)
            {
                var rowItems = hh.Count <= cardsPerRow || i < cardsPerRow
                    ? cardsPerRow
                    : hh.Count - cardsPerRow;
                var x_offset = ((i % cardsPerRow) - rowItems/2.0f + 0.5f) * (CardWidth + padding);
                var y_offset = hh.Count > cardsPerRow
                    ? ((i / cardsPerRow) - 1) * (CardHeight + padding)
                    : 0;

                var targetPosition = new Vector2(Centre.X + x_offset, Centre.Y + y_offset);
                var targetAngle = 0f;

                hh[i].MoveTo(targetPosition, 400);
                hh[i].RotateTo(targetAngle, 2);
            }
            IsStraight = true;
        }
    }
}

