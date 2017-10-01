using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Concurrent;
using Mariasek.SharedClient.GameComponents;

namespace Mariasek.SharedClient
{
    /// <summary>
    /// This is a game component that implements IUpdateable.
    /// </summary>
	public partial class GameComponent : Microsoft.Xna.Framework.GameComponent
    {
        private GameComponent _parent;
        protected new MariasekMonoGame Game;
        protected List<GameComponent> Children = new List<GameComponent>(); //not thread safe!
		public IReadOnlyCollection<GameComponent> ChildElements { get { return Children; } }
        protected ConcurrentQueue<GameComponentOperation> ScheduledOperations = new ConcurrentQueue<GameComponentOperation>();
        protected class GameComponentOperation
        {
            public int OperationType;
            public Action ActionHandler;
            public Func<bool> ConditionFunc;
            public int WaitMs;
            public Vector2 Position { get; set; }
            public float Speed { get; set; }
            public float Opacity { get; set; }
        }
        protected class GameComponentOperationType
        {
            public const int Action = 1;
            public const int Condition = 2;
            public const int Wait = 4;
            public const int Move = 8;
            public const int Fade = 16;
        }
        private DateTime? _waitEnd;

        public string Name { get; set; }
        public virtual bool IsEnabled { get; set; }
		private bool _isVisible;
        public bool IsVisible
		{
			get
			{
				//if any of the parents are not visible then the child is also not visible, but will retain its own state
				var current = this;

				for (; current.Parent != null; current = current.Parent)
				{
					if (current._isVisible == false)
					{
						return false;
					}
				}
				return true;
			}
			private set
			{
				_isVisible = value;
			}
		}
        public virtual float Opacity { get; set; }
        public GameComponent Parent
        { 
            get { return _parent; }
            set 
            {
                if (_parent != null)
                {
                    lock (_parent)
                    {
                        _parent.Children.Remove(this);
                    }
                }
                _parent = value;
                if (_parent != null)
                {
                    lock (_parent)
                    {
                        _parent.Children.Add(this);
                        _parent.Children.Sort((a, b) => a.ZIndex - b.ZIndex);
                    }
				}
            }
        }
        public virtual Vector2 Position
        { 
            get;
            set;
        }
		public virtual AnchorType Anchor { get; set; }
		protected Matrix ScaleMatrix
		{
			get
			{
				switch (Anchor)
				{
					case AnchorType.Left:
						return Game.LeftScaleMatrix;
					case AnchorType.Top:
						return Game.TopScaleMatrix;
					case AnchorType.Right:
						return Game.RightScaleMatrix;
					case AnchorType.Bottom:
						return Game.BottomScaleMatrix;
					default:
						return Game.MainScaleMatrix;
				}
			}
		}
        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Mariasek.SharedClient.GameComponent"/> has scheduled operations pending.
        /// </summary>
        /// <value><c>true</c> if is busy; otherwise, <c>false</c>.</value>
        public virtual bool IsBusy { get { return ScheduledOperations!= null && ScheduledOperations.Count > 0; } }
        public virtual bool IsMoving { get; protected set; }
        private int _zIndex;
        public virtual int ZIndex
        { 
            get { return _zIndex; }
            set
            {
                _zIndex = Math.Max(0, value);
                if (_parent != null)
                {
                    lock (_parent)
                    {
                        _parent.Children.Sort((a, b) => a.ZIndex - b.ZIndex);
                    }
                }
            }
        }
        public object Tag { get; set; }

        /// <summary>
        /// This constructor shall be called only from the Scene constructor.
        /// </summary>
        protected GameComponent(MariasekMonoGame game)
            : base(game)
        {
            Game = game;
            Name = GetType().Name;
            IsEnabled = true;
            IsVisible = true;
        }

        public GameComponent(GameComponent parent)
            : this(parent.Game)
        {
            Parent = parent;
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

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				//dispose unmanaged resources here
			}
			//dispose managed resources here
			DisposeChildren();
			base.Dispose(disposing);
		}

		public void DisposeChildren()
		{
			for (var i = 0; i < Children.Count; i++)
			{
				var child = Children[i];

				if (child != null)
				{
					child.Dispose();
				}
			}
			Children.Clear();
		}

        protected virtual void GameRestarted()
        {
        }

        public virtual void Show()
        {
            IsVisible = true;
        }

        public virtual void Hide()
        {
            IsVisible = false;
        }

        public bool IsChildOf(GameComponent control)
        {
            for (var iterator = Parent; iterator != null; iterator = iterator.Parent)
            {
                if (iterator == control)
                {
                    return true;
                }
            }
            return false;
        }

        public GameComponent WaitUntilImpl(Func<bool> condition)
        {
            ScheduledOperations.Enqueue(new GameComponentOperation
            {
                OperationType = GameComponentOperationType.Condition,
                ConditionFunc = condition
            });

            return this;
        }

        public GameComponent InvokeImpl(Action handler)
        {
            ScheduledOperations.Enqueue(new GameComponentOperation
            {
                OperationType = GameComponentOperationType.Action,
                ActionHandler = handler
            });

            return this;
        }

        public GameComponent WaitImpl(int milliseconds)
        {
            ScheduledOperations.Enqueue(new GameComponentOperation
            {
                OperationType = GameComponentOperationType.Wait,
                WaitMs = milliseconds
            });

            return this;
        }

        public GameComponent MoveToImpl(Vector2 targetPosition, float speed = 100f)
        {
            ScheduledOperations.Enqueue(new GameComponentOperation
            {
                OperationType = GameComponentOperationType.Move,
                Position = targetPosition,
                Speed = speed
            });

            return this;
        }

        public GameComponent FadeImpl(float targetOpacity, float speed = 100f)
        {
            ScheduledOperations.Enqueue(new GameComponentOperation
            {
                OperationType = GameComponentOperationType.Fade,
                Opacity = targetOpacity,
                Speed = speed
            });

            return this;
        }

        public void ClearOperations()
        {
            GameComponentOperation dummy;

            while(ScheduledOperations.Count > 0)
            {
                ScheduledOperations.TryDequeue(out dummy);
            }
        }

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GameComponentOperation operation;

            if (ScheduledOperations.TryPeek(out operation) && operation != null)
            {
                var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

                var moveVector = Vector2.Subtract(operation.Position, Position);
                var normalizedDirection = moveVector.Length() == 0 ? moveVector : Vector2.Normalize(moveVector);
                var fadeDirection = Math.Sign(operation.Opacity - Opacity);
                var positionDiff = operation.Speed * deltaTime * normalizedDirection;
                var fadeDiff = operation.Speed * deltaTime * fadeDirection;

                if (operation.OperationType == GameComponentOperationType.Wait)
                {
                    if (!_waitEnd.HasValue)
                    {
                        _waitEnd = DateTime.Now.AddMilliseconds(operation.WaitMs);
                    }
                    if (DateTime.Now > _waitEnd.Value)
                    {
                        _waitEnd = null;
                        ScheduledOperations.TryDequeue(out operation);
                    }
                }
                else if (operation.OperationType == GameComponentOperationType.Condition)
                {
                    if (operation.ConditionFunc == null || operation.ConditionFunc())
                    {
                        ScheduledOperations.TryDequeue(out operation);
                    }
                }
                else if (operation.OperationType == GameComponentOperationType.Action)
                {
                    if (operation.ActionHandler != null)
                    {
                        operation.ActionHandler();
                    }
                    ScheduledOperations.TryDequeue(out operation);
                }
                else if (operation.OperationType == GameComponentOperationType.Fade)
                {
                    if (fadeDiff != 0)
                    {
                        if ((fadeDiff > 0 && Opacity + fadeDiff > operation.Opacity) ||
                            (fadeDiff < 0 && Opacity + fadeDiff < operation.Opacity))
                        {
                            Opacity = operation.Opacity;
                        }
                        else
                        {
                            Opacity += fadeDiff;
                        }
                    }
                    if (Opacity == operation.Opacity)
                    {
                        ScheduledOperations.TryDequeue(out operation);
                    }
                }
                else if (operation.OperationType == GameComponentOperationType.Move)
                {
                    if (positionDiff != Vector2.Zero)
                    {
                        if (positionDiff.Length() > moveVector.Length())
                        {
                            Position = operation.Position;
                        }
                        else
                        {
                            Position += positionDiff;
                        }
                    }
                }
                var moveFinished = operation != null && operation.OperationType == GameComponentOperationType.Move && positionDiff == Vector2.Zero;
                if (moveFinished)
                {
                    ScheduledOperations.TryDequeue(out operation);
                }
                IsMoving = operation != null && (operation.OperationType & GameComponentOperationType.Move) != 0 && positionDiff != Vector2.Zero;
            }

			for (var i = Children.Count - 1; i >= 0; i--)
            //for (var i = 0; i < Children.Count; i++)
			{
				var child = Children[i] as TouchControlBase;

				if (child != null)
				{
					try
					{
						child.TouchUpdate(gameTime);
					}
					catch (Exception)
					{
					}
				}
			}

            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];

                if (child != null)
                {
                    try
                    {
                        child.Update(gameTime);
                    }
                    catch(Exception)
                    {
                    }
                }
            }
		}

        public virtual void Draw(GameTime gameTime)
        {
			if (IsVisible)
            {
                for (var i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];

                    if (child != null)
                    {
                        try
                        {
                            child.Draw(gameTime);
                        }
                        catch(Exception)
                        {
                        }
                    }
                }
            }
        }
    }
}

