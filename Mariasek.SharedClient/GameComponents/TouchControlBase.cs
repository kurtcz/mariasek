using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
//using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;


namespace Mariasek.SharedClient.GameComponents
{
    /// <summary>
    /// This is a game component that implements IUpdateable.
    /// </summary>
    public abstract class TouchControlBase : GameComponent
    {
        private const float maxClickTimeMs = 500;
        private float _touchHeldTimeMs;
        private bool _touchHeldConsumed;
		private static TouchControlBase _draggedObject;

        protected int TouchId = -1;

		public bool CanDrag { get; set; }
        public bool IsClicked { get; private set; }
        public SoundEffect ClickSound { get; set; }

        public TouchControlBase(GameComponent parent)
            : base(parent)
        {
            // TODO: Construct any child components here
            ClickSound = Game.ClickSound;

        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            // TODO: Add your initialization code here

            base.Initialize();
        }

        protected override void GameRestarted()
        {
            ClickSound = Game.ClickSound;
        }

        /// <summary>
        /// When overriden in a child object this function checks for a collision between the object and a position
        /// </summary>
        /// <returns><c>true</c>, if collision occured, <c>false</c> otherwise.</returns>
        /// <param name="position">Position.</param>
        public abstract bool CollidesWithPosition(Vector2 position);

        public delegate void TouchDownEventHandler(object sender, TouchLocation tl);
        public event TouchDownEventHandler TouchDown;

        /// <summary>
        /// Raises the touch down event.
        /// </summary>
        protected virtual void OnTouchDown(TouchLocation tl)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} OnTouchDown", this));
            if (TouchDown != null)
            {
                TouchDown(this, tl);
            }
        }

        public delegate bool TouchHeldEventHandler(object sender, float touchHeldTimeMs, TouchLocation tl);
        public event TouchHeldEventHandler TouchHeld;

        /// <summary>
        /// Raises the touch held event.
        /// </summary>
        /// <returns><c>true</c>, if the event has been handled, <c>false</c> othewrise.</returns>
        /// <param name="touchHeldTimeMs">Touch held time ms.</param>
        protected virtual bool OnTouchHeld(float touchHeldTimeMs, TouchLocation tl)
        {
            var handled = false;

            if (TouchHeld != null)
            {
                handled |= TouchHeld(this, touchHeldTimeMs, tl);
            }

            return handled;
        }

        public delegate void TouchUpEventHandler(object sender, TouchLocation tl);
        public event TouchUpEventHandler TouchUp;

        /// <summary>
        /// Raises the touch up event.
        /// </summary>
        protected virtual void OnTouchUp(TouchLocation tl)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} OnTouchUp", this));
            if (TouchUp != null)
            {
                TouchUp(this, tl);
            }
        }

        public delegate void ClickEventHandler(object sender);
        public event ClickEventHandler Click;

        /// <summary>
        /// Raises the click event.
        /// </summary>
        protected virtual void OnClick()
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} OnClick", this));
            if (Click != null)
            {
                Click(this);
            }
        }

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            // TODO: Add your update code here
            if (!IsVisible || 
                !IsEnabled ||
                (Game.CurrentScene.ExclusiveControl != null && 
                 Game.CurrentScene.ExclusiveControl.IsVisible && 
                 Game.CurrentScene.ExclusiveControl != this &&
                 Parent.IsChildOf(Game.CurrentScene.ExclusiveControl)))
            {
                //Ignore input if there is an exclusive control (a modal dialog or an overlay) being shown and we are not that control or one of its children
                base.Update(gameTime);
                return;
            }

			// Transform the touch collection to show position in virtual coordinates
			var currTouches = Game.TouchCollection.Select(i => new TouchLocation(i.Id, i.State, new Vector2((i.Position.X - ScaleMatrix.M41) / ScaleMatrix.M11,
			                                                                                                (i.Position.Y - ScaleMatrix.M42) / ScaleMatrix.M22)));

            IsClicked = false;
            if (TouchId != -1)  //if touch held
            {
                _touchHeldTimeMs += (float)gameTime.ElapsedGameTime.TotalMilliseconds;
            }
            foreach (var tl in currTouches)
            {
                if (CollidesWithPosition(tl.Position))
                {
                    if ((tl.State == TouchLocationState.Pressed || tl.State == TouchLocationState.Moved))
                    {
                        if (_draggedObject == null)// && TouchId == -1)	//first time down
                        {
                            _draggedObject = this;
                            _touchHeldTimeMs = 0;
                            TouchId = tl.Id;
                            OnTouchDown(tl);
                        }
                        if (_draggedObject == this && !_touchHeldConsumed)
                        {
                            _touchHeldConsumed = OnTouchHeld(_touchHeldTimeMs, tl);
                        }
                    }
                    else if (tl.Id == TouchId && tl.State == TouchLocationState.Released)
                    {
                        _draggedObject = null;
                        _touchHeldConsumed = false;
                        TouchId = -1;

                        OnTouchUp(tl);

                        IsClicked = _touchHeldTimeMs <= maxClickTimeMs;
                        if (IsClicked)
                        {
                            OnClick();
                        }
                    }
                }
                else if (tl.Id == TouchId && tl.State == TouchLocationState.Moved)
                {
                    if (!_touchHeldConsumed)
                    {
                        _touchHeldConsumed = OnTouchHeld(_touchHeldTimeMs, tl);
                    }
                }
                else if (tl.Id == TouchId && tl.State == TouchLocationState.Moved)
                {
					if (!_touchHeldConsumed)
					{
						_touchHeldConsumed = OnTouchHeld(_touchHeldTimeMs, tl);
					}
                }
                else if (tl.Id == TouchId && tl.State == TouchLocationState.Released)
                {
					_draggedObject = null;
					_touchHeldConsumed = false;
					TouchId = -1;
					OnTouchUp(tl);
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine(string.Format("{0}: OUT state: {1} id: {2} position: {3}", Name, tl.State, tl.Id, tl.Position));
                }
            }
            base.Update(gameTime);
        } 
    }
}

