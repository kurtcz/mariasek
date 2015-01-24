using System.Windows;
using System.Windows.Media;

namespace Mariasek.TesterGUI
{
    public static class DependencyObjectExtensions
    {
        public static T findElementOfType<T>(DependencyObject element) where T : DependencyObject
        {
            T parent = element as T;
            if (parent != null)
                return parent;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                T found = findElementOfType<T>(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
