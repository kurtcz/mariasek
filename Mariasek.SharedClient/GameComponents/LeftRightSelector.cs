using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Mariasek.SharedClient.GameComponents
{
	public class SelectorItems : SelectorItems<object>
	{
	}

	public class SelectorItems<T> : List<KeyValuePair<string, T>>
	{
		public void Add(string text, T value)
		{
			Add(new KeyValuePair<string, T>(text, value)); 
		}

		public int FindIndex(T value)
		{
			var i = 0;

			foreach(var kvp in this)
			{
				if (kvp.Value.Equals(value))
				{
					return i;
				}
				i++;
			}

			return -1;
		}
	}

	public class LeftRightSelector : LeftRightSelector<object>
	{
		public LeftRightSelector(GameComponent parent)
			: base(parent)
		{
		}
	}

	public class SelectorButton : Button
	{
		public SelectorButton(GameComponent parent)
			: base(parent)
		{
			_buttonText.VerticalAlign = VerticalAlignment.Top;
			_buttonText.TextRenderer = Game.FontRenderers["SegoeUI40Outl"];

			Width = 50;
			Height = 50;
			TextColor = Color.Yellow;
			BackgroundColor = Color.Transparent;
			BorderColor = Color.Transparent;
		}
	}

	public class LeftRightSelector<TValue> : GameComponent
	{
		private Button _leftButton;
		private Button _rightButton;
		private Button _valueLabel;

		public SelectorItems<TValue> Items;
		public int _selectedIndex;
		public int SelectedIndex
		{ 
			get { return _selectedIndex; }
			set
			{
				_selectedIndex = value;
				if (_selectedIndex < 0 || _selectedIndex >= Items.Count)
				{
					_selectedIndex = -1;
				}
				UpdateValue(); 
			}
		}
		public TValue SelectedValue { get { return Items[SelectedIndex].Value; } }
		public override Vector2 Position
		{
			get
			{
				return base.Position;
			}
			set
			{
				base.Position = value;
				UpdateControls();
			}
		}
		private int _width;
		public int Width
		{
			get { return _width; }
			set { _width = value; UpdateControls(); }
		}
		private int _height;
		public int Height
		{
			get { return _height; }
			set { _height = value; UpdateControls(); }
		}

		public delegate void SelectionChangedEventHandler(object sender);
		public event SelectionChangedEventHandler SelectionChanged;

		/// <summary>
		/// Raises the selection changed event.
		/// </summary>
		protected virtual void OnSelectionChanged()
		{
			System.Diagnostics.Debug.WriteLine(string.Format("{0} SelectionChanged", this));
			if (SelectionChanged != null)
			{
				SelectionChanged(this);
			}
		}

		public LeftRightSelector(GameComponent parent)
			: base(parent)
		{
			_width = 200;
			_leftButton = new SelectorButton(this)
			{
				Name = "LeftButton",
				Text = "««"
			};
			_leftButton.Click += LeftButtonClick;
			_rightButton = new SelectorButton(this)
			{
				Name = "RightButton",
				Text = "»»"
			};
			_rightButton.Click += RightButtonClick;
			_valueLabel = new Button(this)
			{
				Name = "Value",
				Width = 100,
				Height = 50,
				BackgroundColor = Color.Transparent,
				BorderColor = Color.Transparent
			};
			_valueLabel.Click += RightButtonClick;
			SelectedIndex = -1;
			UpdateControls();
		}

		private void UpdateControls()
		{
			_leftButton.Position = base.Position;
			_rightButton.Position = base.Position + new Vector2(Width - _rightButton.Width, 0);
			_valueLabel.Position = base.Position + new Vector2(_leftButton.Width, 0);
			_valueLabel.Width = Width - _leftButton.Width - _rightButton.Width;
		}

		private void UpdateValue()
		{
			if (SelectedIndex == -1)
			{
				_valueLabel.Text = "?";
			}
			else
			{
				_valueLabel.Text = Items[SelectedIndex].Key;
			}
			OnSelectionChanged();
		}

		private void LeftButtonClick(object sender)
		{
			if (SelectedIndex > 0)
			{
				SelectedIndex--;
			}
			else
			{
				SelectedIndex = Items.Count - 1;
			}
			UpdateValue();
		}

		private void RightButtonClick(object sender)
		{
			if (SelectedIndex < Items.Count - 1)
			{
				SelectedIndex++;
			}
			else
			{
				SelectedIndex = 0;
			}
			UpdateValue();
		}
	}
}
