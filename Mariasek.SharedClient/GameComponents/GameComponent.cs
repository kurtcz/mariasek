using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mariasek.SharedClient
{
    /// <summary>
    /// This is a game component that implements IUpdateable.
    /// </summary>
    public class GameComponent : Microsoft.Xna.Framework.GameComponent
    {
        private GameComponent _parent;

        protected new MariasekMonoGame Game;
        protected List<GameComponent> Children = new List<GameComponent>();

        public string Name { get; set; }
        public virtual bool IsEnabled { get; set; }
        public bool IsVisible { get; private set; }
        public GameComponent Parent
        { 
            get { return _parent; }
            set 
            {
                if (_parent != null)
                {
                    _parent.Children.Remove(this);
                }
                _parent = value;
                if (_parent != null)
                {
                    _parent.Children.Add(this);
                }
            }
        }
        public virtual Vector2 Position { get; set; }
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

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            // TODO: Add your update code here

            base.Update(gameTime);

            foreach (var child in Children)
            {
                child.Update(gameTime);
            }
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (IsVisible)
            {
                foreach (var child in Children)
                {
                    child.Draw(gameTime);
                }
            }
        }
    }
}

