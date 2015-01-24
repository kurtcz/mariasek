using System;
using System.Windows;

namespace Mariasek.TesterGUI
{
    /// <summary>
    /// Interaction logic for HandWindow.xaml
    /// </summary>
    public partial class HandWindow : Window
    {
        #region ScaleValue Dependency Property

        public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue", typeof(double), typeof(HandWindow), new UIPropertyMetadata(1.0, new PropertyChangedCallback(OnScaleValueChanged), new CoerceValueCallback(OnCoerceScaleValue)));

        private static object OnCoerceScaleValue(DependencyObject o, object value)
        {
            HandWindow handWindow = o as HandWindow;
            if (handWindow != null)
                return handWindow.OnCoerceScaleValue((double)value);
            else
                return value;
        }

        private static void OnScaleValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            HandWindow handWindow = o as HandWindow;
            if (handWindow != null)
                handWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
        }

        protected virtual double OnCoerceScaleValue(double value)
        {
            if (double.IsNaN(value))
                return 1.0d;

            value = Math.Max(0.1, value);
            return value;
        }

        protected virtual void OnScaleValueChanged(double oldValue, double newValue)
        {

        }

        public double ScaleValue
        {
            get
            {
                return (double)GetValue(ScaleValueProperty);
            }
            set
            {
                SetValue(ScaleValueProperty, value);
            }
        }
        #endregion

        private void HandGrid_SizeChanged(object sender, EventArgs e)
        {
            CalculateScale();
        }

        private void CalculateScale()
        {
            double yScale = ActualHeight / 272;
            double xScale = ActualWidth / 728.0d;
            double value = Math.Min(xScale, yScale);
            ScaleValue = (double)OnCoerceScaleValue(HandGrid, value);
        }

        public HandWindow()
        {            
            InitializeComponent();
            var scaleFactor = AppSettings.GetDouble("ScaleFactor", 0.8);
            Width *= scaleFactor;
            Height *= scaleFactor;
        }

        private void MyHandWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
