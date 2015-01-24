using System;
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

using GameEngine = Mariasek.Engine.New;

namespace Mariasek.SharedClient
{
    public static class Extensions
    {
        public static bool IsPointInPolygon(this Vector2 point, IList<Vector2> polygon)
        {
            bool isInside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                    (point.X < (polygon[j].X - polygon[i].X) * 
                               (point.Y - polygon[i].Y) / 
                               (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    isInside = !isInside;
                }
            }

            return isInside;
        }

        public static Vector2[] ToPolygon(this Rectangle rect)
        {
            return new []
            {
                new Vector2(rect.Left, rect.Top),
                new Vector2(rect.Right, rect.Top),
                new Vector2(rect.Right, rect.Bottom),
                new Vector2(rect.Left, rect.Bottom)
            };
        }

        public static bool IsPointInPolygon(this Vector2 point, Rectangle rect)
        {
            return IsPointInPolygon(point, rect.ToPolygon());
        }

        public static Vector2 Rotate(this Vector2 point, Vector2 rotationOrigin, float angle)
        {
            var relativeVector = Vector2.Subtract(rotationOrigin, point);
            var m = Matrix.CreateRotationZ(angle);

            Vector2.Transform(ref relativeVector, ref m, out relativeVector);

            return Vector2.Add(rotationOrigin, relativeVector);
        }

        public static Vector2[] Rotate(this Rectangle rect, Vector2 rotationOrigin, float angle)
        {
            var polygon = rect.ToPolygon();

            return polygon.Rotate(rotationOrigin, angle);
        }

        public static Vector2[] Rotate(this Vector2[] polygon, Vector2 rotationOrigin, float angle)
        {
            for (var i = 0; i < polygon.Length; i++)
            {
                var rotated = polygon[i].Rotate(rotationOrigin, angle);

                polygon[i].X = rotated.X;
                polygon[i].Y = rotated.Y;
            }

            return polygon;
        }

        private static int TransformSuit(GameEngine.Card c)
        {
            switch (c.Suit)
            {
                case GameEngine.Barva.Cerveny:
                    return 2;
                case GameEngine.Barva.Zeleny:
                    return 1;
                case GameEngine.Barva.Kule:
                    return 0;
                case GameEngine.Barva.Zaludy:
                    return 3;
            }
            return 0;
        }

        private static int TransformValue(GameEngine.Card c)
        {
            switch (c.Value)
            {
                case GameEngine.Hodnota.Sedma:
                    return 0;
                case GameEngine.Hodnota.Osma:
                    return 1;
                case GameEngine.Hodnota.Devitka:
                    return 2;
                case GameEngine.Hodnota.Spodek:
                    return 4;
                case GameEngine.Hodnota.Svrsek:
                    return 5;
                case GameEngine.Hodnota.Kral:
                    return 6;
                case GameEngine.Hodnota.Desitka:
                    return 3;
                case GameEngine.Hodnota.Eso:
                    return 7;
            }
            return 0;
        }

        public static Rectangle ToTextureRect(this GameEngine.Card c)
        {
            const int CardWidth = 65;
            const int CardHeight = 112;
            var num = TransformSuit(c) * 8 + TransformValue(c);
            var rect = new Rectangle(4 + (num % 8) * 74, 5 + (num / 8) * 120, CardWidth, CardHeight);

            return rect;
        }

        public static string ToDescription(this Mariasek.Engine.New.Barva b)
        {
            switch (b)
            {
                case GameEngine.Barva.Cerveny:
                    return "červený";
                case GameEngine.Barva.Zeleny:
                    return "zelený";
                case GameEngine.Barva.Kule:
                    return "kule";
                case GameEngine.Barva.Zaludy:
                    return "žaludy";
            }
            return string.Empty;
        }

        public static string ToDescription(this Mariasek.Engine.New.GameFlavour f)
        {
            switch (f)
            {
                case GameEngine.GameFlavour.Good:
                    return "Dobrá";
                case GameEngine.GameFlavour.Bad:
                    return "Špatná";
            }
            return string.Empty;
        }
    }
}

