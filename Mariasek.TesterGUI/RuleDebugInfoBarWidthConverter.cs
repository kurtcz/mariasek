using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Mariasek.Engine.New;

namespace Mariasek.TesterGUI
{
    public class RuleDebugInfoBarWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var rdi = value as RuleDebugInfo;
            if (rdi != null)
            {
                var maxWidth = 300;
                int.TryParse((string)parameter, out maxWidth);
                var num = (float)rdi.RuleCount / (float)rdi.TotalRuleCount;
                var scaled = (int)(num  * maxWidth);
                return scaled.ToString();
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
