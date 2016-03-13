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
    public class RuleDebugInfoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var src = value as RuleDebugInfo;
            if (src != null)
            {
                return String.Format("{0}: {1}%", src.Rule, 100.0 * src.RuleCount / src.TotalRuleCount);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
