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

namespace Mariasek.SharedClient.GameComponents
{
    public class ModalButton : Button
    {
        public object ModalResult { get; set; }
        public ModalButton(GameComponent parent)
            :base (parent)
        {
        }
    }

    public class Dialog : TextBox
    {
        public ModalButton[] Buttons { get; private set; }
        public object ModalResult { get; set; }

        public Dialog(GameComponent parent, string[] buttonTexts, object[] buttonValues)
            :base (parent)
        {
            var len = Math.Min(buttonTexts.Length, buttonValues.Length);
            var buttons = new ModalButton[len];

            for (var i = 0; i < len; i++)
            {
                buttons[i] = new ModalButton(this)
                {
                    Text = buttonTexts[i],
                    ModalResult = buttonValues[i]
                };
                buttons[i].Click += ModalButtonClicked;
            }
        }

        public override Vector2 Position
        {
            get { return base.Position; }
            set
            {
                base.Position = value;
                RecalculateButtonsPosition();
            }
        }

        public override int Width
        {
            get { return base.Width; }
            set
            {
                base.Width = value;
                RecalculateButtonsPosition();
            }
        }

        public override int Height
        {
            get { return base.Height; }
            set
            {
                base.Height = value;
                RecalculateButtonsPosition();
            }
        }

        public override void Show()
        {
            //Game.CurrentScene.ModalDialog = this;
            Game.CurrentScene.ExclusiveControl = this;
            base.Show();
            foreach (var child in Children)
            {
                child.Show();
            }
        }

        public override void Hide()
        {
            //Whoever is interested in reading the modal result should ensure that he afterwards calls Game.CurrentScene.ModalDialog = null;
            base.Hide();
            foreach (var child in Children)
            {
                child.Hide();
            }
        }

        private void RecalculateButtonsPosition()
        {
            const int gapLength = 10;
            const int bottomOffset = 20;
            var buttonsLength = Buttons[0].Width * Buttons.Length + (Buttons.Length - 1) * gapLength;

            for (var i = 0; i < Buttons.Length; i++)
            {
                Buttons[i].Position = new Vector2(
                    Position.X + (Width - buttonsLength) / 2f + i * (Buttons[0].Width + gapLength),
                    Position.Y + Height - Buttons[0].Height - bottomOffset);
            }
        }

        private void ModalButtonClicked(object sender)
        {
            ModalResult = (sender as ModalButton).ModalResult;
            Hide();
        }
    }
}

