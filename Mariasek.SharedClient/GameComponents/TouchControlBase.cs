using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private static float _touchHeldTimeMs;
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

        public delegate void DragEndHandler(object sender, DragEndEventArgs e);
		public event DragEndHandler DragEnd;

		protected virtual void OnDragEnd(DragEndEventArgs e)
        {
			System.Diagnostics.Debug.WriteLine(string.Format("{0} OnDragEnd", this));
            if (DragEnd != null)
			{
				DragEnd(this, e);
			}
		}

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public void TouchUpdate(GameTime gameTime)
        {
            if (!IsVisible || 
                !IsEnabled ||
                !IsChildOf(Game.CurrentScene) ||
                (Game.CurrentScene.ExclusiveControl != null && 
                 Game.CurrentScene.ExclusiveControl.IsVisible && 
                 Game.CurrentScene.ExclusiveControl != this &&
                 !Parent.IsChildOf(Game.CurrentScene.ExclusiveControl)))
            {
                if (_draggedObject == this)
                {
                    TouchId = -1;
                    _touchHeldConsumed = false;
                    _touchHeldTimeMs = 0;
                    _draggedObject = null;
                }
                //Ignore input if there is an exclusive control (a modal dialog or an overlay) being shown and we are not that control or one of its children
                return;
            }

            if (_draggedObject != null &&
                (_draggedObject.TouchId == -1 ||
                 !_draggedObject.IsChildOf(Game.CurrentScene)))
            {
                _draggedObject.TouchId = -1;
                _draggedObject._touchHeldConsumed = false;
                _touchHeldTimeMs = 0;
                _draggedObject = null;
            }

            // Transform the touch collection to show position in virtual coordinates
            var currTouches = Game.TouchCollection.Select(i => new TouchLocation(i.Id, i.State, new Vector2((i.Position.X - ScaleMatrix.M41) / ScaleMatrix.M11,
                                                                                                            (i.Position.Y - ScaleMatrix.M42) / ScaleMatrix.M22)));

            if (_draggedObject != null &&
                currTouches.All(i => i.Id != _draggedObject.TouchId))
            {
                _draggedObject.TouchId = -1;
                _draggedObject._touchHeldConsumed = false;
                _touchHeldTimeMs = 0;
                _draggedObject = null;
            }
            IsClicked = false;
            if (TouchId != -1)  //if touch held
            {
                _touchHeldTimeMs += (float)gameTime.ElapsedGameTime.TotalMilliseconds;
                if (currTouches.All(i => i.Id != TouchId))
                {
                    TouchId = -1;
                    _touchHeldTimeMs = 0;
                    _touchHeldConsumed = false;
                    if (_draggedObject == this)
                    {
                        _draggedObject = null;
                    }
                }
            }
            foreach (var tl in currTouches)
            {
                //System.Diagnostics.Debug.WriteLine($"Touch {tl.Id} {tl.State}. TouchId {TouchId} {Name} {GetHashCode()}");
                if (CollidesWithPosition(tl.Position))
                {
                    if ((tl.State == TouchLocationState.Pressed || tl.State == TouchLocationState.Moved))
                    {
                        if (_draggedObject != null &&
                            !(_draggedObject is ClickableArea) &&
                            (TouchId == -1 ||
                             _draggedObject != this))
                        {
                            //pokud uz tahame jiny objekt, tak eventy ignoruj
                            continue;
                        }
                        if (tl.State == TouchLocationState.Pressed)// && _draggedObject != null && TouchId == -1)
						{
                            //prevents orphan dragged objects to cause a deadlock (nothing receives a touch down event)
                            if (_draggedObject != null)
                            {
                                _draggedObject.TouchId = -1;
                                _draggedObject._touchHeldConsumed = false;
                                _touchHeldTimeMs = 0;
                                _draggedObject = null;
                            }
                        }
                        if (_draggedObject == null)// && TouchId == -1)	//first time down
                        {
                            _touchHeldTimeMs = 0;
                            _touchHeldConsumed = false;
                            TouchId = tl.Id;
                            _draggedObject = this;
                            //System.Diagnostics.Debug.WriteLine($"TouchId <- {TouchId} * {Name}");

                            OnTouchDown(tl);
                        }
                        if (_draggedObject == this && !_touchHeldConsumed)
                        {
                            _touchHeldConsumed = OnTouchHeld(_touchHeldTimeMs, tl);
                        }
                    }
                    else if (tl.Id == TouchId && tl.State == TouchLocationState.Released)
                    {
                        IsClicked = _touchHeldTimeMs <= maxClickTimeMs;
                        _touchHeldTimeMs = 0;
                        _draggedObject = null;
                        _touchHeldConsumed = false;
                        TouchId = -1;
                        //System.Diagnostics.Debug.WriteLine($"TouchId <- {TouchId} ** {Name}");

                        OnTouchUp(tl);

                        if (IsClicked)
                        {
                            OnClick();
                        }
                    }
                }
                else if (tl.Id == TouchId && tl.State == TouchLocationState.Moved)
                {
                    if (_draggedObject == this && !_touchHeldConsumed)
                    {
                        _touchHeldConsumed = OnTouchHeld(_touchHeldTimeMs, tl);
                    }
                }
                else if (tl.Id == TouchId && tl.State == TouchLocationState.Released)
                {
					_draggedObject = null;
                    _touchHeldTimeMs = 0;
                    _touchHeldConsumed = false;
					TouchId = -1;
                    //System.Diagnostics.Debug.WriteLine($"TouchId <- {TouchId} *** {Name}");

                    OnTouchUp(tl);
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine(string.Format("{0}: OUT state: {1} id: {2} position: {3}", Name, tl.State, tl.Id, tl.Position));
                }
            }
        }
    }
}

